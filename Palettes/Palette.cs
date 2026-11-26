using UnityEngine;

namespace PaintBrush.Palettes;

/// <summary>
/// A named set of saved colours. Built-in palettes (the starters) can't be changed, only duplicated.
/// </summary>
internal sealed class Palette(string name, string starter = null)
{
    public const int Size = 24;

    public string Name { get; set; } = name;

    /// <summary>
    /// The id of the starter palette this is, or null for one the player made.
    /// </summary>
    public string Starter { get; } = starter;

    public bool IsBuiltIn => Starter != null;

    public Color32?[] Colors { get; } = new Color32?[Size];

    public Palette CopyAs(string name)
    {
        Palette copy = new(name);
        Colors.CopyTo(copy.Colors, 0);
        return copy;
    }
}
