using System.Collections.Generic;

using PaintBrush.Brush;

using UnityEngine;

namespace PaintBrush.Palettes;

/// <summary>
/// The palettes the mod comes with. They're made here each time rather than saved, so they can't be changed. Each
/// fills all three rows, with similar colours next to each other; a duplicate can be changed freely.
/// </summary>
internal static class StarterPalettes
{
    private sealed class Starter(string id, string name, string[] colors)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public string[] Colors { get; } = colors;
    }

    private static readonly Starter[] All =
    [
        new("banners", "Banners",
        [
            "A83232", "D98E32", "E8D27A", "6E9A4E", "4F7FB0", "8C5FA8", "EDE6D6", "CFC4A8",
            "7A1F1F", "B5651D", "D9B44A", "3E6B3A", "2E4E7A", "5B3A6E", "B89A7A", "707070",
            "4E1212", "8A4A14", "B8902F", "2A4A26", "1C3354", "3D2650", "5A4A3A", "2A2A2A",
        ]),
        new("earth", "Earth & wood",
        [
            "E8D7B5", "D4B58A", "C4A27A", "C79A5B", "B88A5A", "A07C55", "9C6B43", "806048",
            "A65E2E", "8A3B22", "7A5134", "6B4E3D", "5C3D26", "4A3628", "3B2A1E", "2E241C",
            "9E8F72", "7D7563", "5E4A3A", "8B7B4F", "6F6A3A", "5C6B3A", "4F5A35", "3A4A2A",
        ]),
        new("stone", "Stone & metal",
        [
            "E6E6E0", "D4AF37", "C9A54A", "B87333", "8C5A2B", "5E3A2A", "7E6B5A", "9A8C7E",
            "D8D8D4", "B4B6B8", "A8ADB4", "9AA0A6", "8F9296", "6E7176", "646A70", "4A4D52",
            "6B7F7A", "4E6E8E", "5B6670", "3E4A52", "3A3F45", "2B2D30", "1E1E1E", "121314",
        ]),
        new("vivid", "Vivid",
        [
            "F59A9A", "F5D29A", "C9F59A", "9AF5C9", "9AC9F5", "C99AF5", "FFFFFF", "000000",
            "EB2323", "EBB923", "87EB23", "23EB55", "28C7C7", "2850C7", "7728C7", "C7289F",
            "EB6E23", "D2EB23", "3CEB23", "23EBA0", "288BC7", "3C28C7", "B328C7", "C72863",
        ]),
    ];

    /// <summary>
    /// The colours the palette had before presets, which a character that never changed it still has saved.
    /// </summary>
    public static readonly string[] OldDefault = ["7A1F1F", "B5651D", "D9B44A", "3E6B3A", "2E4E7A", "5B3A6E", "EDE6D6", "2A2A2A"];

    public static IEnumerable<Palette> Create()
    {
        foreach (Starter starter in All)
        {
            Palette palette = new(starter.Name, starter.Id);
            Fill(palette.Colors, starter);
            yield return palette;
        }
    }

    private static void Fill(Color32?[] colors, Starter starter)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = i < starter.Colors.Length && BrushState.TryParseHex(starter.Colors[i], out Color32 color) ? color : null;
        }
    }
}
