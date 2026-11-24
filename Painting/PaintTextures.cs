using System.Collections.Generic;

using BepInEx.Configuration;

using UnityEngine;

namespace PaintBrush.Painting;

/// <summary>
/// <para>
/// Makes painted copies of piece textures. The piece shader can only multiply its texture by a colour, which
/// can darken but never lighten, so the paint is put into the texture itself: each pixel moves towards the paint
/// colour, scaled by how bright that pixel is compared with the texture's average. The grain survives, dark wood
/// can still be painted light, and the strength says how far each pixel moves.
/// </para>
/// <para>
/// Painted textures are shared by every piece with the same texture and paint, and counted, so each is freed
/// when the last piece using it is cleared or unloaded. Previews use small textures that are rewritten in place.
/// </para>
/// </summary>
internal static class PaintTextures
{
    private const int PreviewSize = 512;
    private const int SourceCacheSize = 8;

    /// <summary>
    /// Paint brightness (0-255) kept as chosen; see <see cref="Tame"/>.
    /// </summary>
    private const float Knee = 110f;

    /// <summary>
    /// How much of a paint's brightness above <see cref="Knee"/> is kept.
    /// </summary>
    private const float AboveKnee = 0.4f;

    /// <summary>
    /// The most a pixel's brightness, relative to the texture's average, scales the paint: keeps the grain's light
    /// streaks from turning into pure white.
    /// </summary>
    private const float MaxRelative = 1.6f;

    /// <summary>
    /// A texture's pixels at a given size, read back from the graphics card once and kept for a while.
    /// </summary>
    private sealed class Source
    {
        public Color32[] Pixels;
        public int Width;
        public int Height;

        /// <summary>
        /// 1 / the average brightness of the pixels: only the visible ones for cutout materials, whose alpha is
        /// see-through. Other materials keep gloss in the alpha, so all their pixels count.
        /// </summary>
        public float InverseAverage;
    }

    private sealed class Painted
    {
        public Texture2D Texture;
        public int Users;
    }

    private sealed class Preview
    {
        public Texture2D Texture;
        public int Paint;
    }

    private static readonly Dictionary<(int Texture, int Paint), Painted> PaintedTextures = [];
    private static readonly Dictionary<int, Preview> PreviewTextures = [];
    private static readonly Dictionary<(int Texture, int Size, bool Cutout), Source> Sources = [];
    private static readonly LinkedList<(int Texture, int Size, bool Cutout)> SourceOrder = new();

    private static ConfigEntry<int> _maxSize;

    private static int MaxSize => _maxSize != null ? _maxSize.Value : 1024;

    public static void BindConfig(ConfigFile config)
    {
        _maxSize = config.Bind("Graphics", "Paint Texture Size", 1024,
            new ConfigDescription(
                "The largest size of a painted texture. Larger textures are scaled down to this, which saves " +
                "memory but makes painted pieces a little blurrier than vanilla ones up close.",
                new AcceptableValueList<int>(512, 1024, 2048)));
    }

    /// <summary>
    /// The painted copy of a texture, made if needed. Every call must be matched by a <see cref="Release"/>.
    /// </summary>
    public static Texture2D Acquire(Texture source, bool cutout, PaintColor paint)
    {
        (int, int) key = (source.GetInstanceID(), paint.Pack());
        if (!PaintedTextures.TryGetValue(key, out Painted painted) || painted.Texture == null)
        {
            Source pixels = GetSource(source, MaxSize, cutout);
            Texture2D texture = new(pixels.Width, pixels.Height, TextureFormat.RGBA32, mipChain: true, linear: false)
            {
                name = source.name + "_painted",
            };
            CopySettings(source, texture);
            texture.SetPixels32(Paint(pixels, paint));
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            texture.Compress(highQuality: false);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            painted = new Painted { Texture = texture };
            PaintedTextures[key] = painted;
        }

        painted.Users++;
        return painted.Texture;
    }

    public static void Release(Texture source, PaintColor paint)
    {
        if (source == null)
        {
            return;
        }

        (int, int) key = (source.GetInstanceID(), paint.Pack());
        if (!PaintedTextures.TryGetValue(key, out Painted painted) || --painted.Users > 0)
        {
            return;
        }

        _ = PaintedTextures.Remove(key);
        if (painted.Texture != null)
        {
            Object.Destroy(painted.Texture);
        }
    }

    /// <summary>
    /// A small painted copy for previews. There's one per source texture, rewritten when the paint changes, so it's
    /// cheap enough to redo every frame while a colour is being picked.
    /// </summary>
    public static Texture2D GetPreview(Texture source, bool cutout, PaintColor paint)
    {
        int id = source.GetInstanceID();
        if (!PreviewTextures.TryGetValue(id, out Preview preview) || preview.Texture == null)
        {
            Source pixels = GetSource(source, PreviewSize, cutout);
            preview = new Preview
            {
                Texture = new Texture2D(pixels.Width, pixels.Height, TextureFormat.RGBA32, mipChain: true, linear: false)
                {
                    name = source.name + "_preview",
                },
                Paint = -1,
            };
            CopySettings(source, preview.Texture);
            PreviewTextures[id] = preview;
        }

        if (preview.Paint != paint.Pack())
        {
            Source pixels = GetSource(source, PreviewSize, cutout);
            preview.Texture.SetPixels32(Paint(pixels, paint));
            preview.Texture.Apply(updateMipmaps: true);
            preview.Paint = paint.Pack();
        }

        return preview.Texture;
    }

    /// <summary>
    /// The average colour of a texture's visible pixels, as the eye sees the whole piece from a distance.
    /// </summary>
    public static Color32 AverageColor(Texture source, bool cutout)
    {
        Source pixels = GetSource(source, PreviewSize, cutout);
        long r = 0;
        long g = 0;
        long b = 0;
        long count = 0;
        foreach (Color32 pixel in pixels.Pixels)
        {
            if (cutout && pixel.a < 128)
            {
                continue;
            }

            r += pixel.r;
            g += pixel.g;
            b += pixel.b;
            count++;
        }

        return count == 0
            ? new Color32(128, 128, 128, 255)
            : new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
    }

    private static Color32[] Paint(Source source, PaintColor paint)
    {
        Color32[] pixels = source.Pixels;
        Color32[] result = new Color32[pixels.Length];
        if (paint.IsNone)
        {
            System.Array.Copy(pixels, result, pixels.Length);
            return result;
        }

        float strength = paint.Strength / 255f;
        float keep = 1f - strength;
        float tame = Tame(paint);
        float r = paint.R * tame * strength;
        float g = paint.G * tame * strength;
        float b = paint.B * tame * strength;
        float inverse = source.InverseAverage;
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            float relative = Luminance(pixel) * inverse;
            if (relative > MaxRelative)
            {
                relative = MaxRelative;
            }

            result[i] = new Color32(
                ToByte((pixel.r * keep) + (r * relative)),
                ToByte((pixel.g * keep) + (g * relative)),
                ToByte((pixel.b * keep) + (b * relative)),
                pixel.a);
        }

        return result;
    }

    /// <summary>
    /// Scales a paint down when it's lighter than vanilla textures get. Valheim's lighting is tuned for textures
    /// that are fairly dark (wood and stone average around 30-50% brightness), so a white paint taken as it is
    /// glares in sunlight. Brightness up to <see cref="Knee"/> is kept; above it only <see cref="AboveKnee"/> of the
    /// rest counts, so white comes out like a light paint on wood rather than a lamp.
    /// </summary>
    public static float Tame(PaintColor paint)
    {
        float luminance = (0.2126f * paint.R) + (0.7152f * paint.G) + (0.0722f * paint.B);
        return luminance <= Knee ? 1f : (Knee + ((luminance - Knee) * AboveKnee)) / luminance;
    }

    private static byte ToByte(float value)
    {
        return value switch
        {
            >= 255f => 255,
            <= 0f => 0,
            _ => (byte)(value + 0.5f),
        };
    }

    private static float Luminance(Color32 pixel)
    {
        return (0.2126f * pixel.r) + (0.7152f * pixel.g) + (0.0722f * pixel.b);
    }

    private static void CopySettings(Texture source, Texture2D target)
    {
        target.wrapModeU = source.wrapModeU;
        target.wrapModeV = source.wrapModeV;
        target.filterMode = source.filterMode;
        target.anisoLevel = source.anisoLevel;
    }

    private static Source GetSource(Texture texture, int maxSize, bool cutout)
    {
        (int, int, bool) key = (texture.GetInstanceID(), maxSize, cutout);
        if (Sources.TryGetValue(key, out Source source))
        {
            _ = SourceOrder.Remove(key);
            _ = SourceOrder.AddFirst(key);
            return source;
        }

        source = Read(texture, maxSize, cutout);
        Sources[key] = source;
        _ = SourceOrder.AddFirst(key);
        while (SourceOrder.Count > SourceCacheSize)
        {
            _ = Sources.Remove(SourceOrder.Last.Value);
            SourceOrder.RemoveLast();
        }

        return source;
    }

    /// <summary>
    /// Game textures can't be read directly, so they're drawn into a render texture (scaled down to at most
    /// <paramref name="maxSize"/>) and read back from there. The render texture is sRGB, so the bytes come back as
    /// the colours the texture was made with.
    /// </summary>
    private static Source Read(Texture texture, int maxSize, bool cutout)
    {
        int width = Mathf.Max(1, texture.width);
        int height = Mathf.Max(1, texture.height);
        while (width > maxSize || height > maxSize)
        {
            width = Mathf.Max(1, width / 2);
            height = Mathf.Max(1, height / 2);
        }

        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Texture2D readable = new(width, height, TextureFormat.RGBA32, mipChain: false, linear: false);
        try
        {
            Graphics.Blit(texture, target);
            RenderTexture.active = target;
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply(updateMipmaps: false);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }

        Color32[] pixels = readable.GetPixels32();
        Object.Destroy(readable);

        double total = 0;
        int count = 0;
        foreach (Color32 pixel in pixels)
        {
            if (!cutout || pixel.a >= 128)
            {
                total += Luminance(pixel);
                count++;
            }
        }

        float average = count > 0 ? (float)(total / count) : 128f;
        return new Source
        {
            Pixels = pixels,
            Width = width,
            Height = height,
            InverseAverage = 1f / Mathf.Max(average, 8f),
        };
    }
}
