using UnityEngine;

namespace PaintBrush.Palettes;

/// <summary>
/// A named set of saved colours. Built-in palettes (the starters) can't be changed, only duplicated. The player's own
/// palettes can be locked against accidental changes.
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

    /// <summary>
    /// Set by the player on one of their own palettes: nothing can change it until it's unlocked.
    /// </summary>
    public bool Locked { get; set; }

    /// <summary>
    /// Whether the palette can be changed now.
    /// </summary>
    public bool Editable => !IsBuiltIn && !Locked;

    public Color32?[] Colors { get; } = new Color32?[Size];

    public Palette CopyAs(string name)
    {
        Palette copy = new(name);
        Colors.CopyTo(copy.Colors, 0);
        return copy;
    }
}
