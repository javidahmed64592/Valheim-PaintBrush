using System.Collections.Generic;
using System.Globalization;

using PaintBrush.Painting;
using PaintBrush.Palettes;

using UnityEngine;

namespace PaintBrush.Brush;

/// <summary>
/// The brush's colour (kept as hue, saturation and value, so the hue survives greys), its strength, which palette
/// is in use and the recently used colours. Saved with the character, so each character keeps its own. The palettes
/// themselves are shared by every character (see <see cref="PaletteLibrary"/>).
/// </summary>
internal static class BrushState
{
    public const int RecentSize = 16;

    /// <summary>
    /// Darker shades, the base colour in the middle, then lighter shades.
    /// </summary>
    public const int ShadeCount = 9;

    private const int BaseShade = ShadeCount / 2;

    private const string ColourKey = "PaintBrush_Colour";

    /// <summary>
    /// Where the palette was saved before palettes were shared. A character that still has it gets it moved into
    /// the shared palettes once.
    /// </summary>
    private const string OldPaletteKey = "PaintBrush_Palette";

    private const string PaletteNameKey = "PaintBrush_PaletteName";
    private const string RecentKey = "PaintBrush_Recent";

    private static Player _loadedFor;

    public static float Hue { get; private set; } = 28f / 360f;
    public static float Saturation { get; private set; } = 0.75f;
    public static float Value { get; private set; } = 0.75f;

    /// <summary>
    /// 0-100%. 0% paints nothing.
    /// </summary>
    public static int Strength { get; private set; } = 80;

    /// <summary>
    /// The slots of the palette in use. Change them through <see cref="PaletteLibrary"/>.
    /// </summary>
    public static Color32?[] Palette => PaletteLibrary.Active.Colors;

    public static List<Color32> Recent { get; } = [];

    /// <summary>
    /// Goes up with every change, so the menu knows when to redraw.
    /// </summary>
    public static int Version { get; private set; }

    public static Color32 Color => UnityEngine.Color.HSVToRGB(Hue, Saturation, Value);

    public static PaintColor Paint => PaintColor.FromPercent(Color, Strength);

    private static Color32? _shadeBase;

    /// <summary>
    /// The colour the shades are made from. Choosing a shade keeps it, so the other shades stay put; any other way
    /// of setting the colour makes the new colour the base.
    /// </summary>
    public static Color32 ShadeBase => _shadeBase ?? Color;

    /// <summary>
    /// The shade being painted with: the base in the middle unless a shade was chosen.
    /// </summary>
    public static int ShadeIndex { get; private set; } = BaseShade;

    /// <summary>
    /// Each step mixes in 15% more black (darker) or white (lighter).
    /// </summary>
    public static Color32 Shade(int index)
    {
        Color32 color = ShadeBase;
        return index < BaseShade
            ? UnityEngine.Color.Lerp(color, UnityEngine.Color.black, (BaseShade - index) * 0.15f)
            : UnityEngine.Color.Lerp(color, UnityEngine.Color.white, (index - BaseShade) * 0.15f);
    }

    public static void UseShade(int index)
    {
        if (index is >= 0 and < ShadeCount)
        {
            Color32 color = Shade(index);
            _shadeBase ??= Color;
            ApplyRgb(color);
            ShadeIndex = index;
            Changed();
        }
    }

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
            Load(player);
        }

        Version++;
    }

    public static void SetHsv(float hue, float saturation, float value)
    {
        Hue = Mathf.Repeat(hue, 1f);
        Saturation = Mathf.Clamp01(saturation);
        Value = Mathf.Clamp01(value);
        ResetShades();
        Changed();
    }

    /// <summary>
    /// Sets the colour from RGB, keeping the hue (and saturation) where RGB has none, so dragging a grey back to
    /// a colour doesn't jump to red.
    /// </summary>
    public static void SetColor(Color32 color)
    {
        ApplyRgb(color);
        ResetShades();
        Changed();
    }

    private static void ApplyRgb(Color32 color)
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
    }

    private static void ResetShades()
    {
        _shadeBase = null;
        ShadeIndex = BaseShade;
    }

    public static void SetStrength(int percent)
    {
        Strength = Mathf.Clamp(percent, 0, 100);
        Changed();
    }

    /// <summary>
    /// Called by <see cref="PaletteLibrary"/> after any change to the palettes or which one is in use.
    /// </summary>
    public static void PalettesChanged()
    {
        Changed();
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

    private static void Load(Player player)
    {
        Dictionary<string, string> data = player.m_customData;
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

        ResetShades();
        string paletteName = data.TryGetValue(PaletteNameKey, out string name) ? name : null;
        PaletteLibrary.Load();
        if (!MoveOldPalette(player))
        {
            _ = PaletteLibrary.Select(paletteName);
        }
    }

    /// <summary>
    /// Turns the palette the character saved before palettes were shared into a shared palette named after them,
    /// and uses it. A palette that was never changed from the old default is dropped, as Banners starts with it.
    /// </summary>
    private static bool MoveOldPalette(Player player)
    {
        if (!player.m_customData.TryGetValue(OldPaletteKey, out string saved))
        {
            return false;
        }

        _ = player.m_customData.Remove(OldPaletteKey);
        string[] hexes = saved.Split(',');
        Palettes.Palette palette = new(Localization.instance.Localize("$paintbrush_palette_migrated", player.GetPlayerName()));
        bool changed = false;
        for (int i = 0; i < Palettes.Palette.Size; i++)
        {
            palette.Colors[i] = i < hexes.Length && TryParseHex(hexes[i], out Color32 color) ? color : null;
            Color32? old = i < StarterPalettes.OldDefault.Length && TryParseHex(StarterPalettes.OldDefault[i], out Color32 o) ? o : null;
            changed |= !PaletteLibrary.Same(palette.Colors[i], old);
        }

        if (!changed)
        {
            return false;
        }

        _ = PaletteLibrary.Add(palette);
        return true;
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

        data[PaletteNameKey] = PaletteLibrary.Active.Name;

        string[] recent = new string[Recent.Count];
        for (int i = 0; i < Recent.Count; i++)
        {
            recent[i] = ToHex(Recent[i]);
        }

        data[RecentKey] = string.Join(",", recent);
    }
}
