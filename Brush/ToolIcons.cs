using System;
using System.Collections.Generic;

using UnityEngine;

namespace PaintBrush.Brush;

/// <summary>
/// Draws the brush tools' icons: a paint brush with a wet stroke for Paint, and the brush with a "no paint" sign
/// for Clear. Shapes are drawn with 4×4 supersampling for smooth edges, in colours close to vanilla icons.
/// </summary>
internal static class ToolIcons
{
    private const int Size = 128;
    private const int Samples = 4;

    private static readonly Color Handle = new(0.55f, 0.36f, 0.2f);
    private static readonly Color HandleDark = new(0.33f, 0.21f, 0.12f);
    private static readonly Color Ferrule = new(0.62f, 0.62f, 0.6f);
    private static readonly Color PaintRed = new(0.72f, 0.2f, 0.14f);
    private static readonly Color Bristles = new(0.86f, 0.78f, 0.6f);
    private static readonly Color Sign = new(0.85f, 0.75f, 0.55f);

    private readonly struct Shape(Func<Vector2, bool> inside, Color color)
    {
        public readonly Func<Vector2, bool> Inside = inside;
        public readonly Color Color = color;
    }

    public static Sprite Paint()
    {
        List<Shape> shapes = [.. Brush(PaintRed)];

        // A wavy stroke of paint along the bottom.
        shapes.Insert(0, new Shape(p => Mathf.Abs(p.y - (22f + (6f * Mathf.Sin(p.x * 0.09f)))) < 7f && p.x > 14f && p.x < 92f, PaintRed));
        return ToSprite(Draw(shapes), "PaintBrush_PaintIcon");
    }

    public static Sprite Clear()
    {
        List<Shape> shapes = [.. Brush(Bristles)];
        Vector2 centre = new(86f, 36f);
        const float radius = 24f;
        const float width = 5f;
        shapes.Add(new Shape(p => Mathf.Abs(Vector2.Distance(p, centre) - radius) < width, Sign));
        Vector2 a = centre + (new Vector2(-1f, 1f).normalized * radius);
        Vector2 b = centre + (new Vector2(1f, -1f).normalized * radius);
        shapes.Add(new Shape(p => SegmentDistance(p, a, b) < width, Sign));
        return ToSprite(Draw(shapes), "PaintBrush_ClearIcon");
    }

    /// <summary>
    /// A brush lying from bottom left to top right: wooden handle, metal ferrule, then the bristles in the given
    /// colour, widening towards the tip.
    /// </summary>
    private static List<Shape> Brush(Color bristles)
    {
        Vector2 end = new(14f, 50f);
        Vector2 handleTop = new(66f, 88f);
        Vector2 ferruleTop = new(80f, 98f);
        Vector2 tip = new(112f, 120f);
        return
        [
            new Shape(p => SegmentDistance(p, end, handleTop) < 8f, HandleDark),
            new Shape(p => SegmentDistance(p, end, handleTop) < 6f, Handle),
            new Shape(p => SegmentDistance(p, handleTop, ferruleTop) < 9f, Ferrule),
            new Shape(p => Bristle(p, ferruleTop, tip), bristles),
        ];
    }

    /// <summary>
    /// A wedge from the ferrule to a rounded tip: 9 wide at the ferrule, 13 at the tip.
    /// </summary>
    private static bool Bristle(Vector2 p, Vector2 from, Vector2 to)
    {
        Vector2 axis = to - from;
        float length = axis.magnitude;
        Vector2 direction = axis / length;
        float along = Vector2.Dot(p - from, direction);
        if (along < 0f || along > length)
        {
            return Vector2.Distance(p, to) < 13f && along > 0f;
        }

        float across = Mathf.Abs((direction.x * (p - from).y) - (direction.y * (p - from).x));
        return across < Mathf.Lerp(9f, 13f, along / length);
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + (ab * t));
    }

    private static Texture2D Draw(List<Shape> shapes)
    {
        Color[] pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Color pixel = Color.clear;
                foreach (Shape shape in shapes)
                {
                    int hits = 0;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            if (shape.Inside(new Vector2(x + ((sx + 0.5f) / Samples), y + ((sy + 0.5f) / Samples))))
                            {
                                hits++;
                            }
                        }
                    }

                    if (hits == 0)
                    {
                        continue;
                    }

                    // Paints the shape over what's there.
                    float coverage = hits / (float)(Samples * Samples);
                    Color color = shape.Color;
                    float alpha = coverage + (pixel.a * (1f - coverage));
                    pixel = new Color(
                        ((color.r * coverage) + (pixel.r * pixel.a * (1f - coverage))) / alpha,
                        ((color.g * coverage) + (pixel.g * pixel.a * (1f - coverage))) / alpha,
                        ((color.b * coverage) + (pixel.b * pixel.a * (1f - coverage))) / alpha,
                        alpha);
                }

                pixels[(y * Size) + x] = pixel;
            }
        }

        Texture2D texture = new(Size, Size, TextureFormat.RGBA32, mipChain: false);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Sprite ToSprite(Texture2D texture, string name)
    {
        texture.name = name;
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
        sprite.name = name;
        return sprite;
    }
}
