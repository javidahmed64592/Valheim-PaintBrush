using System.Collections.Generic;

using PaintBrush.Painting;

using UnityEngine;

namespace PaintBrush.Brush;

/// <summary>
/// <para>
/// The paint brush's model, built from code and put in place of the hammer's mesh in the item copied from it, so
/// it's held and swung exactly like the hammer. It matches the icon: a turned wooden handle, a bronze ferrule and a
/// flat block of bristles with a tip wet with paint.
/// </para>
/// <para>
/// Everything is in the hammer mesh's space: +Y runs from the end of the handle towards the head, the hand holds it
/// at the origin, and the hammer's head is wide along X, so the brush is flat along X too. One small generated
/// texture holds the wood, bronze and bristle colours. The tip is its own submesh on plain white, tinted by its
/// material's colour: the icon's red, or for the local player's brush the colour they're painting with.
/// </para>
/// </summary>
internal static class BrushModel
{
    private const string HammerChild = "attach/hammer";
    private const int TextureSize = 64;
    private const int Sides = 16;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly Color DefaultTip = new(0.78f, 0.27f, 0.23f);

    /// <summary>
    /// Areas of the texture (u from, u to, v from, v to), kept a texel inside each area's edges so bilinear
    /// filtering doesn't bleed between them.
    /// </summary>
    private static readonly Vector4 WoodArea = Area(0, 32, 0, 64);
    private static readonly Vector4 BronzeArea = Area(32, 48, 0, 64);
    private static readonly Vector4 BristleArea = Area(48, 64, 0, 40);
    private static readonly Vector4 TipArea = Area(48, 64, 44, 64);

    private static readonly MaterialPropertyBlock Block = new();
    private static GameObject _tinted;
    private static int _tintedVersion = -1;

    /// <summary>
    /// A cross-section: a superellipse with half-width <c>X</c> and half-depth <c>Z</c> at height <c>Y</c>;
    /// <c>Round</c> = 2 is an ellipse, higher is closer to a rectangle.
    /// </summary>
    private readonly struct Ring(float y, float x, float z, float round)
    {
        public readonly float Y = y;
        public readonly float X = x;
        public readonly float Z = z;
        public readonly float Round = round;
    }

    /// <summary>
    /// Swaps the hammer's model in the brush item's prefab for the brush.
    /// </summary>
    public static void Apply(GameObject itemPrefab)
    {
        Transform child = itemPrefab.transform.Find(HammerChild);
        MeshFilter filter = child != null ? child.GetComponent<MeshFilter>() : null;
        MeshRenderer renderer = child != null ? child.GetComponent<MeshRenderer>() : null;
        if (filter == null || renderer == null)
        {
            Jotunn.Logger.LogWarning("Hammer model not as expected, the paint brush looks like a hammer.");
            return;
        }

        child.name = "brush";
        filter.sharedMesh = BuildMesh();
        Material body = BodyMaterial(renderer.sharedMaterial);
        Material tip = new(body) { name = "PaintBrush_Tip" };
        tip.SetColor(ColorId, DefaultTip);
        renderer.sharedMaterials = [body, tip];
    }

    /// <summary>
    /// Tints the tip of the local player's brush with the colour they're painting with, whenever it changes.
    /// </summary>
    public static void Update(Player player)
    {
        GameObject held = player != null && player.m_visEquipment != null ? player.m_visEquipment.m_rightItemInstance : null;
        if (held == null || !BrushTools.IsEquipped(player))
        {
            _tinted = null;
            return;
        }

        if (held == _tinted && BrushState.Version == _tintedVersion)
        {
            return;
        }

        _tinted = held;
        _tintedVersion = BrushState.Version;
        MeshRenderer renderer = held.GetComponentInChildren<MeshRenderer>();
        if (renderer == null || renderer.sharedMaterials.Length < 2)
        {
            return;
        }

        Color32 color = BrushState.Color;
        float tame = PaintTextures.Tame(new PaintColor(color.r, color.g, color.b, 255));
        Block.Clear();
        Block.SetColor(ColorId, new Color(color.r * tame / 255f, color.g * tame / 255f, color.b * tame / 255f));
        renderer.SetPropertyBlock(Block, 1);
    }

    private static Material BodyMaterial(Material hammer)
    {
        Material material = new(hammer) { name = "PaintBrush_Body" };
        material.SetTexture("_MainTex", BuildTexture());
        material.SetTextureScale("_MainTex", Vector2.one);
        material.SetTextureOffset("_MainTex", Vector2.zero);

        // The hammer's normal and metal maps are laid out for its own texture.
        material.SetTexture("_BumpMap", null);
        material.SetTexture("_MetallicGlossMap", null);
        material.DisableKeyword("_NORMALMAP");
        material.DisableKeyword("_METALLICGLOSSMAP");
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_MetalGloss", 0f);
        material.SetFloat("_Glossiness", 0.15f);
        material.SetColor(ColorId, Color.white);
        return material;
    }

    private static Mesh BuildMesh()
    {
        List<Vector3> vertices = [];
        List<Vector2> uvs = [];
        List<int> body = [];
        List<int> tip = [];

        // Handle: a knob at the end, slimming towards the ferrule.
        Ring[] handle =
        [
            new(-0.40f, 0.075f, 0.075f, 2f),
            new(-0.36f, 0.092f, 0.092f, 2f),
            new(-0.30f, 0.078f, 0.078f, 2f),
            new(0.15f, 0.068f, 0.068f, 2f),
            new(0.62f, 0.062f, 0.062f, 2f),
            new(0.72f, 0.075f, 0.075f, 2f),
        ];
        Loft(handle, WoodArea, vertices, uvs, body, capStart: true, capEnd: false);

        // Ferrule: a short neck flaring out into a flat band, wide along X.
        Ring[] ferrule =
        [
            new(0.70f, 0.080f, 0.075f, 2.5f),
            new(0.76f, 0.200f, 0.082f, 4f),
            new(0.80f, 0.215f, 0.085f, 4f),
            new(0.94f, 0.215f, 0.085f, 4f),
            new(0.96f, 0.205f, 0.080f, 4f),
        ];
        Loft(ferrule, BronzeArea, vertices, uvs, body, capStart: false, capEnd: true);

        // Bristles, widening slightly and thinning to a flat edge; the last part is wet with paint.
        Ring[] bristles =
        [
            new(0.95f, 0.198f, 0.070f, 4f),
            new(1.18f, 0.208f, 0.064f, 4f),
        ];
        Loft(bristles, BristleArea, vertices, uvs, body, capStart: false, capEnd: false);

        Ring[] wet =
        [
            new(1.18f, 0.208f, 0.064f, 4f),
            new(1.32f, 0.214f, 0.048f, 4f),
            new(1.40f, 0.210f, 0.020f, 6f),
        ];
        Loft(wet, TipArea, vertices, uvs, tip, capStart: false, capEnd: true);

        Mesh mesh = new() { name = "PaintBrush_Brush" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(body, 0);
        mesh.SetTriangles(tip, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Joins the rings with a band of quads, mapping the band onto an area of the texture (around the ring along u,
    /// along the length along v), with optional flat caps at either end.
    /// </summary>
    private static void Loft(Ring[] rings, Vector4 area, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
        bool capStart, bool capEnd)
    {
        int first = vertices.Count;
        float start = rings[0].Y;
        float length = rings[rings.Length - 1].Y - start;
        foreach (Ring ring in rings)
        {
            float v = Mathf.Lerp(area.z, area.w, (ring.Y - start) / length);
            for (int i = 0; i <= Sides; i++)
            {
                vertices.Add(Point(ring, i));
                uvs.Add(new Vector2(Mathf.Lerp(area.x, area.y, i / (float)Sides), v));
            }
        }

        const int stride = Sides + 1;
        for (int r = 0; r < rings.Length - 1; r++)
        {
            for (int i = 0; i < Sides; i++)
            {
                int low = first + (r * stride) + i;
                int high = low + stride;
                triangles.AddRange([low, high, low + 1, low + 1, high, high + 1]);
            }
        }

        if (capStart)
        {
            Cap(rings[0], area, vertices, uvs, triangles, facingUp: false);
        }

        if (capEnd)
        {
            Cap(rings[rings.Length - 1], area, vertices, uvs, triangles, facingUp: true);
        }
    }

    private static void Cap(Ring ring, Vector4 area, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, bool facingUp)
    {
        Vector2 centreUv = new((area.x + area.y) / 2f, facingUp ? area.w : area.z);
        int centre = vertices.Count;
        vertices.Add(new Vector3(0f, ring.Y, 0f));
        uvs.Add(centreUv);
        for (int i = 0; i <= Sides; i++)
        {
            vertices.Add(Point(ring, i));
            uvs.Add(centreUv);
        }

        for (int i = 0; i < Sides; i++)
        {
            int a = centre + 1 + i;
            int b = a + 1;
            if (facingUp)
            {
                triangles.AddRange([centre, b, a]);
            }
            else
            {
                triangles.AddRange([centre, a, b]);
            }
        }
    }

    private static Vector3 Point(Ring ring, int side)
    {
        float angle = side / (float)Sides * Mathf.PI * 2f;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        float power = 2f / ring.Round;
        return new Vector3(
            ring.X * Mathf.Sign(cos) * Mathf.Pow(Mathf.Abs(cos), power),
            ring.Y,
            ring.Z * Mathf.Sign(sin) * Mathf.Pow(Mathf.Abs(sin), power));
    }

    /// <summary>
    /// Wood with streaks of grain along the handle, bronze with a soft band, cream bristles with darker strands,
    /// and a white area (with faint strands) for the tip, which its material tints.
    /// </summary>
    private static Texture2D BuildTexture()
    {
        Color wood = new(0.45f, 0.29f, 0.15f);
        Color bronze = new(0.69f, 0.48f, 0.24f);
        Color bristle = new(0.85f, 0.76f, 0.58f);
        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float noise = Hash(x, y);
                float strand = Hash(x, 7);
                Color color;
                if (x < 32)
                {
                    float grain = 0.82f + (0.18f * Mathf.Sin((x * 1.3f) + (Mathf.Sin(y * 0.2f) * 2f))) + (0.06f * noise);
                    color = wood * grain;
                }
                else if (x < 48)
                {
                    color = bronze * (0.9f + (0.1f * Mathf.Sin(y * 0.4f)) + (0.05f * noise));
                }
                else
                {
                    float shade = 0.86f + (0.14f * strand) + (0.04f * noise);
                    color = y < 42 ? bristle * shade : Color.white * (0.9f + (0.1f * strand));
                }

                color.a = 1f;
                pixels[(y * TextureSize) + x] = color;
            }
        }

        Texture2D texture = new(TextureSize, TextureSize, TextureFormat.RGBA32, mipChain: true)
        {
            name = "PaintBrush_Brush",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: true);
        return texture;
    }

    private static float Hash(int x, int y)
    {
        float value = Mathf.Sin((x * 12.9898f) + (y * 78.233f)) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    private static Vector4 Area(int fromX, int toX, int fromY, int toY)
    {
        const float texel = 1f / TextureSize;
        return new Vector4((fromX + 1) * texel, (toX - 1) * texel, (fromY + 1) * texel, (toY - 1) * texel);
    }
}
