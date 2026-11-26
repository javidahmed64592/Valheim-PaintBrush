using System;

using PaintBrush.Brush;

using UnityEngine;

namespace PaintBrush.Palettes;

/// <summary>
/// A palette as one line of text for sharing, e.g. <c>PB1:Banners:7A1F1F,B5651D,,2A2A2A</c> (empty slots are blank).
/// </summary>
internal static class PaletteCode
{
    private const string Prefix = "PB1:";

    public static string Encode(Palette palette)
    {
        string[] colors = new string[Palette.Size];
        int last = -1;
        for (int i = 0; i < colors.Length; i++)
        {
            Color32? color = palette.Colors[i];
            colors[i] = color.HasValue ? BrushState.ToHex(color.Value) : "";
            if (color.HasValue)
            {
                last = i;
            }
        }

        return Prefix + palette.Name.Replace(":", "") + ":" + string.Join(",", colors, 0, last + 1);
    }

    /// <summary>
    /// Reads a code, ignoring spaces and the backticks around a code pasted from a chat message.
    /// </summary>
    public static bool TryDecode(string text, out Palette palette)
    {
        palette = null;
        text = text?.Trim().Trim('`').Trim();
        if (string.IsNullOrEmpty(text) || !text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] parts = text.Substring(Prefix.Length).Split([':'], 2);
        if (parts.Length != 2)
        {
            return false;
        }

        string name = parts[0].Trim();
        palette = new Palette(name.Length > 0 ? name : "Palette");
        string[] colors = parts[1].Split(',');
        bool any = false;
        for (int i = 0; i < Palette.Size && i < colors.Length; i++)
        {
            if (BrushState.TryParseHex(colors[i], out Color32 color))
            {
                palette.Colors[i] = color;
                any = true;
            }
            else if (colors[i].Trim().Length > 0)
            {
                return false;
            }
        }

        return any;
    }
}
