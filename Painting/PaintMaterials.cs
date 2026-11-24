using System.Collections.Generic;

using UnityEngine;

namespace PaintBrush.Painting;

/// <summary>
/// Painted copies of piece materials: the same material with its main texture swapped for the painted one. They're
/// shared by every renderer with the same material and paint, and counted, so each is freed when the last one is
/// cleared. Previews have one copy per material whose texture is rewritten in place.
/// </summary>
/// <remarks>
/// A copy is freed a while after its last user lets go, not straight away: a broken piece's fragments (made by
/// Destructible.CreateFragments) share its materials and fall for 2-4 seconds after the piece itself is gone.
/// </remarks>
internal static class PaintMaterials
{
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");

    /// <summary>
    /// How long an unused copy is kept before it's freed; longer than broken pieces' fragments last.
    /// </summary>
    private const float KeepUnused = 10f;

    private sealed class Entry
    {
        public Material Material;
        public Texture Source;
        public PaintColor Paint;
        public int Users;
        public float UnusedSince;
    }

    private static readonly List<(int Material, int Paint)> Unused = [];

    private static readonly Dictionary<(int Material, int Paint), Entry> Painted = [];
    private static readonly Dictionary<int, Material> Previews = [];

    /// <summary>
    /// Every copy made here, to the material it was copied from.
    /// </summary>
    private static readonly Dictionary<int, Material> Originals = [];

    /// <summary>
    /// The material a painted copy was made from, or the material itself if it isn't a copy.
    /// </summary>
    public static Material Original(Material material)
    {
        return material != null && Originals.TryGetValue(material.GetInstanceID(), out Material original) && original != null
            ? original
            : material;
    }

    public static bool IsCopy(Material material)
    {
        return material != null && Originals.ContainsKey(material.GetInstanceID());
    }

    /// <summary>
    /// The painted copy of a material. Every call must be matched by a <see cref="Release"/>.
    /// </summary>
    public static Material Acquire(Material original, Texture source, bool cutout, PaintColor paint)
    {
        (int, int) key = (original.GetInstanceID(), paint.Pack());
        if (!Painted.TryGetValue(key, out Entry entry) || entry.Material == null)
        {
            entry = new Entry
            {
                Material = Copy(original, PaintTextures.Acquire(source, cutout, paint)),
                Source = source,
                Paint = paint,
            };
            Painted[key] = entry;
        }

        entry.Users++;
        return entry.Material;
    }

    public static void Release(Material original, PaintColor paint)
    {
        if (original == null)
        {
            return;
        }

        (int, int) key = (original.GetInstanceID(), paint.Pack());
        if (!Painted.TryGetValue(key, out Entry entry) || --entry.Users > 0)
        {
            return;
        }

        entry.UnusedSince = Time.time;
        Unused.Add(key);
    }

    /// <summary>
    /// Frees copies that have been unused for a while. Called every frame; cheap when nothing is waiting.
    /// </summary>
    public static void FreeUnused()
    {
        for (int i = Unused.Count - 1; i >= 0; i--)
        {
            (int, int) key = Unused[i];
            if (!Painted.TryGetValue(key, out Entry entry) || entry.Users > 0)
            {
                Unused.RemoveAt(i);
                continue;
            }

            if (Time.time - entry.UnusedSince < KeepUnused)
            {
                continue;
            }

            Unused.RemoveAt(i);
            _ = Painted.Remove(key);
            PaintTextures.Release(entry.Source, entry.Paint);
            if (entry.Material != null)
            {
                _ = Originals.Remove(entry.Material.GetInstanceID());
                Object.Destroy(entry.Material);
            }
        }
    }

    /// <summary>
    /// The preview copy of a material, showing this paint.
    /// </summary>
    public static Material Preview(Material original, Texture source, bool cutout, PaintColor paint)
    {
        Texture2D texture = PaintTextures.GetPreview(source, cutout, paint);
        int id = original.GetInstanceID();
        if (!Previews.TryGetValue(id, out Material preview) || preview == null)
        {
            preview = Copy(original, texture);
            Previews[id] = preview;
        }
        else
        {
            preview.SetTexture(MainTex, texture);
        }

        return preview;
    }

    private static Material Copy(Material original, Texture texture)
    {
        Material copy = new(original) { name = original.name };
        copy.SetTexture(MainTex, texture);
        Originals[copy.GetInstanceID()] = original;
        return copy;
    }
}
