using System.Collections.Generic;

using UnityEngine;

namespace PaintBrush.Painting;

/// <summary>
/// Painted copies of piece materials: the same material with its main texture swapped for the painted one. They're
/// shared by every renderer with the same material and paint, and counted, so each is freed when the last one is
/// cleared. Previews have one copy per material whose texture is rewritten in place.
/// </summary>
internal static class PaintMaterials
{
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");

    private sealed class Entry
    {
        public Material Material;
        public Texture Source;
        public int Users;
    }

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

        _ = Painted.Remove(key);
        PaintTextures.Release(entry.Source, paint);
        if (entry.Material != null)
        {
            _ = Originals.Remove(entry.Material.GetInstanceID());
            Object.Destroy(entry.Material);
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
