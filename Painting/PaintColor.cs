using System;

using UnityEngine;

namespace PaintBrush.Painting;

/// <summary>
/// A piece's paint: a colour and how strongly it covers the piece's own colour (1-255; 0 means no paint).
/// Stored on the piece as one int, so it costs a single entry in the piece's data.
/// </summary>
internal readonly struct PaintColor(byte r, byte g, byte b, byte strength) : IEquatable<PaintColor>
{
    public static PaintColor None => default;

    public readonly byte R = r;
    public readonly byte G = g;
    public readonly byte B = b;
    public readonly byte Strength = strength;

    public bool IsNone => Strength == 0;

    /// <summary>
    /// The colour at full opacity, without the strength.
    /// </summary>
    public Color32 Color => new(R, G, B, 255);

    public int Pack()
    {
        return IsNone ? 0 : (R << 24) | (G << 16) | (B << 8) | Strength;
    }

    public static PaintColor Unpack(int value)
    {
        PaintColor paint = new((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return paint.IsNone ? None : paint;
    }

    /// <summary>
    /// A paint from a colour and a strength in percent; 0% is no paint.
    /// </summary>
    public static PaintColor FromPercent(Color32 color, int percent)
    {
        if (percent <= 0)
        {
            return None;
        }

        int strength = Mathf.Clamp(Mathf.RoundToInt(percent * 2.55f), 1, 255);
        return new PaintColor(color.r, color.g, color.b, (byte)strength);
    }

    public bool Equals(PaintColor other)
    {
        return Pack() == other.Pack();
    }

    public override bool Equals(object obj)
    {
        return obj is PaintColor other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Pack();
    }

    public static bool operator ==(PaintColor left, PaintColor right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PaintColor left, PaintColor right)
    {
        return !left.Equals(right);
    }
}
