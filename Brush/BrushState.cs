using System.Collections.Generic;
using System.Globalization;

using PaintBrush.Painting;

using UnityEngine;

namespace PaintBrush.Brush;

/// <summary>
/// The brush's colour (kept as hue, saturation and value, so the hue survives greys), its strength, the saved
/// palette and the recently used colours. Saved with the character, so each character keeps its own.
/// </summary>
internal static class BrushState
{
    public const int PaletteSize = 24;
    public const int RecentSize = 16;

    private const string ColourKey = "PaintBrush_Colour";
    private const string PaletteKey = "PaintBrush_Palette";
    private const string RecentKey = "PaintBrush_Recent";

    private static readonly string[] DefaultPalette =
    [
        "7A1F1F", "B5651D", "D9B44A", "3E6B3A", "2E4E7A", "5B3A6E", "EDE6D6", "2A2A2A",
    ];

    private static Player _loadedFor;

    public static float Hue { get; private set; } = 28f / 360f;
    public static float Saturation { get; private set; } = 0.75f;
    public static float Value { get; private set; } = 0.75f;

    /// <summary>
    /// 0-100%. 0% paints nothing.
    /// </summary>
    public static int Strength { get; private set; } = 80;

    public static Color32?[] Palette { get; } = new Color32?[PaletteSize];
    public static List<Color32> Recent { get; } = [];

    /// <summary>
    /// Goes up with every change, so the menu knows when to redraw.
    /// </summary>
    public static int Version { get; private set; }

    public static Color32 Color => UnityEngine.Color.HSVToRGB(Hue, Saturation, Value);

    public static PaintColor Paint => PaintColor.FromPercent(Color, Strength);

    /// <summary>
    /// Loads the brush of the character that's playing, once per character.
    /// </summary>
    public static void Sync(Player player)
    {
        if (player == _loadedFor)
        {
            return;
        }

        _loadedFor = player;
        if (player != null)
        {
            Load(player.m_customData);
        }

        Version++;
    }

    public static void SetHsv(float hue, float saturation, float value)
    {
        Hue = Mathf.Repeat(hue, 1f);
        Saturation = Mathf.Clamp01(saturation);
        Value = Mathf.Clamp01(value);
        Changed();
    }

    /// <summary>
    /// Sets the colour from RGB, keeping the hue (and saturation) where RGB has none, so dragging a grey back to
    /// a colour doesn't jump to red.
    /// </summary>
    public static void SetColor(Color32 color)
    {
        UnityEngine.Color.RGBToHSV(color, out float hue, out float saturation, out float value);
        if (value > 0f && saturation > 0f)
        {
            Hue = hue;
        }

        if (value > 0f)
        {
            Saturation = saturation;
        }

        Value = value;
        Changed();
    }

    public static void SetStrength(int percent)
    {
        Strength = Mathf.Clamp(percent, 0, 100);
        Changed();
    }

    public static void SetPaletteSlot(int index, Color32? color)
    {
        if (index is >= 0 and < PaletteSize)
        {
            Palette[index] = color;
            Changed();
        }
    }

    /// <summary>
    /// Puts the current colour at the front of the recent colours.
    /// </summary>
    public static void AddRecent()
    {
        Color32 color = Color;
        _ = Recent.RemoveAll(c => Same(c, color));
        Recent.Insert(0, color);
        if (Recent.Count > RecentSize)
        {
            Recent.RemoveRange(RecentSize, Recent.Count - RecentSize);
        }

        Changed();
    }

    public static string ToHex(Color32 color)
    {
        return color.r.ToString("X2", CultureInfo.InvariantCulture)
            + color.g.ToString("X2", CultureInfo.InvariantCulture)
            + color.b.ToString("X2", CultureInfo.InvariantCulture);
    }

    public static bool TryParseHex(string text, out Color32 color)
    {
        color = default;
        text = text?.Trim().TrimStart('#');
        if (text is not { Length: 6 }
            || !int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
        {
            return false;
        }

        color = new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
        return true;
    }

    private static bool Same(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b;
    }

    private static void Changed()
    {
        Version++;
        Save();
    }

    private static void Load(Dictionary<string, string> data)
    {
        if (data.TryGetValue(ColourKey, out string colour))
        {
            string[] parts = colour.Split(',');
            if (parts.Length == 4
                && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float hue)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float saturation)
                && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int strength))
            {
                Hue = Mathf.Repeat(hue, 1f);
                Saturation = Mathf.Clamp01(saturation);
                Value = Mathf.Clamp01(value);
                Strength = Mathf.Clamp(strength, 0, 100);
            }
        }

        string[] palette = data.TryGetValue(PaletteKey, out string saved) ? saved.Split(',') : DefaultPalette;
        for (int i = 0; i < PaletteSize; i++)
        {
            Palette[i] = i < palette.Length && TryParseHex(palette[i], out Color32 color) ? color : null;
        }

        Recent.Clear();
        if (data.TryGetValue(RecentKey, out string recent))
        {
            foreach (string hex in recent.Split(','))
            {
                if (Recent.Count < RecentSize && TryParseHex(hex, out Color32 color))
                {
                    Recent.Add(color);
                }
            }
        }
    }

    private static void Save()
    {
        if (_loadedFor == null)
        {
            return;
        }

        Dictionary<string, string> data = _loadedFor.m_customData;
        data[ColourKey] = string.Join(",",
            Hue.ToString("R", CultureInfo.InvariantCulture),
            Saturation.ToString("R", CultureInfo.InvariantCulture),
            Value.ToString("R", CultureInfo.InvariantCulture),
            Strength.ToString(CultureInfo.InvariantCulture));

        string[] palette = new string[PaletteSize];
        for (int i = 0; i < PaletteSize; i++)
        {
            palette[i] = Palette[i].HasValue ? ToHex(Palette[i].Value) : "";
        }

        data[PaletteKey] = string.Join(",", palette);

        string[] recent = new string[Recent.Count];
        for (int i = 0; i < Recent.Count; i++)
        {
            recent[i] = ToHex(Recent[i]);
        }

        data[RecentKey] = string.Join(",", recent);
    }
}
