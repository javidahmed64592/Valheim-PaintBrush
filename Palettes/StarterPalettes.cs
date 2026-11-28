using System.Collections.Generic;

using PaintBrush.Brush;

using UnityEngine;

namespace PaintBrush.Palettes;

/// <summary>
/// The palettes the mod comes with. They're made here each time rather than saved, so they can't be changed. Each
/// fills all three rows; a duplicate can be changed freely.
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
            "7A1F1F", "B5651D", "D9B44A", "3E6B3A", "2E4E7A", "5B3A6E", "EDE6D6", "2A2A2A",
            "A83232", "D98E32", "E8D27A", "6E9A4E", "4F7FB0", "8C5FA8", "B89A7A", "5A4A3A",
            "4E1212", "8A4A14", "B8902F", "2A4A26", "1C3354", "3D2650", "CFC4A8", "707070",
        ]),
        new("earth", "Earth & wood",
        [
            "3B2A1E", "5C3D26", "7A5134", "9C6B43", "B88A5A", "D4B58A", "6B4E3D", "4A3628",
            "8A3B22", "A65E2E", "C79A5B", "6F6A3A", "4F5A35", "7D7563", "9E8F72", "2E241C",
            "E8D7B5", "C4A27A", "A07C55", "806048", "5E4A3A", "8B7B4F", "5C6B3A", "3A4A2A",
        ]),
        new("stone", "Stone & metal",
        [
            "2B2D30", "4A4D52", "6E7176", "8F9296", "B4B6B8", "D8D8D4", "5B6670", "3E4A52",
            "8C5A2B", "B87333", "C9A54A", "A8ADB4", "6B7F7A", "7E6B5A", "9A8C7E", "1E1E1E",
            "121314", "3A3F45", "646A70", "9AA0A6", "E6E6E0", "4E6E8E", "5E3A2A", "D4AF37",
        ]),
        new("vivid", "Vivid",
        [
            "EB2323", "EB6E23", "EBB923", "D2EB23", "87EB23", "3CEB23", "23EB55", "23EBA0",
            "28C7C7", "288BC7", "2850C7", "3C28C7", "7728C7", "B328C7", "C7289F", "C72863",
            "FFFFFF", "000000", "F59A9A", "F5D29A", "C9F59A", "9AF5C9", "9AC9F5", "C99AF5",
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
