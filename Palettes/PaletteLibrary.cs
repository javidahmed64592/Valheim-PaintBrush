using System;
using System.Collections.Generic;
using System.IO;

using BepInEx;

using Newtonsoft.Json;

using PaintBrush.Brush;

using UnityEngine;

namespace PaintBrush.Palettes;

/// <summary>
/// <para>
/// Every palette: the player's own first, then the built-in ones. The player's palettes are kept in one file next to
/// the mod's config, so all characters share them and players can edit or send the file. Built-in palettes are made
/// in code and never saved, so they can't be changed, only duplicated.
/// </para>
/// <para>
/// Which palette is in use is saved per character (see <see cref="BrushState"/>). Every change is written at once and
/// bumps <see cref="BrushState.Version"/> so the menu redraws.
/// </para>
/// </summary>
internal static class PaletteLibrary
{
    public const int NameLimit = 24;

    private const string FileName = PaintBrushPlugin.PluginGUID + ".palettes.json";

    private sealed class FileData
    {
        public List<PaletteData> Palettes = [];
    }

    private sealed class PaletteData
    {
        public string Name;
        public List<string> Colors = [];
    }

    private static readonly List<Palette> All = [];
    private static int _active;

    public static string FilePath => Path.Combine(Paths.ConfigPath, FileName);

    public static IReadOnlyList<Palette> Palettes => All;

    public static Palette Active
    {
        get
        {
            if (All.Count == 0)
            {
                Load();
            }

            return All[Mathf.Clamp(_active, 0, All.Count - 1)];
        }
    }

    /// <summary>
    /// The number of the player's own palettes, which come before the built-in ones.
    /// </summary>
    private static int OwnCount
    {
        get
        {
            int count = 0;
            while (count < All.Count && !All[count].IsBuiltIn)
            {
                count++;
            }

            return count;
        }
    }

    /// <summary>
    /// Reads the player's palettes from the file and adds the built-in ones. A file that can't be read is kept as
    /// <c>.bad</c>, so a typo in a hand edit doesn't lose the palettes when they're next saved.
    /// </summary>
    public static void Load()
    {
        string name = All.Count > 0 ? Active.Name : null;
        All.Clear();
        string path = FilePath;
        if (File.Exists(path))
        {
            try
            {
                FileData data = JsonConvert.DeserializeObject<FileData>(File.ReadAllText(path));
                foreach (PaletteData saved in data?.Palettes ?? [])
                {
                    All.Add(FromData(saved));
                }
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"Couldn't read {path}, keeping it as {path}.bad and starting again: {e.Message}");
                try
                {
                    File.Copy(path, path + ".bad", overwrite: true);
                }
                catch (Exception copyError)
                {
                    Jotunn.Logger.LogWarning($"Couldn't keep a copy of {path}: {copyError.Message}");
                }

                All.Clear();
            }
        }

        foreach (Palette starter in StarterPalettes.Create())
        {
            // A saved palette may already have a built-in palette's name.
            if (IndexOf(starter.Name, null) < 0)
            {
                All.Add(starter);
            }
        }

        // Quietly, as this runs while a character loads, before it has said which palette it uses.
        _active = Mathf.Max(0, IndexOf(name, null));
    }

    /// <summary>
    /// Uses the palette with this name, if there is one.
    /// </summary>
    public static bool Select(string name)
    {
        int index = IndexOf(name, null);
        if (index < 0)
        {
            return false;
        }

        Select(index);
        return true;
    }

    public static void Select(int index)
    {
        if (index >= 0 && index < All.Count)
        {
            _active = index;
            BrushState.PalettesChanged();
        }
    }

    public static void Cycle(int step)
    {
        if (All.Count > 0)
        {
            Select((int)Mathf.Repeat(_active + step, All.Count));
        }
    }

    /// <summary>
    /// Empties a slot. False on a built-in palette.
    /// </summary>
    public static bool Clear(int index)
    {
        if (Active.IsBuiltIn || index is < 0 or >= Palette.Size)
        {
            return false;
        }

        Active.Colors[index] = null;
        Changed();
        return true;
    }

    /// <summary>
    /// Saves a colour into a slot. If the palette already has the colour in another slot, it moves to this slot
    /// instead and swaps with whatever was here, so a colour is never saved twice. False on a built-in palette.
    /// </summary>
    public static bool Place(int index, Color32 color)
    {
        if (Active.IsBuiltIn || index is < 0 or >= Palette.Size)
        {
            return false;
        }

        Color32?[] colors = Active.Colors;
        for (int i = 0; i < colors.Length; i++)
        {
            if (colors[i].HasValue && Near(colors[i].Value, color))
            {
                return Swap(i, index);
            }
        }

        colors[index] = color;
        Changed();
        return true;
    }

    /// <summary>
    /// Swaps two slots. False on a built-in palette.
    /// </summary>
    public static bool Swap(int a, int b)
    {
        if (Active.IsBuiltIn || a is < 0 or >= Palette.Size || b is < 0 or >= Palette.Size)
        {
            return false;
        }

        if (a != b)
        {
            Color32?[] colors = Active.Colors;
            (colors[a], colors[b]) = (colors[b], colors[a]);
            Changed();
        }

        return true;
    }

    /// <summary>
    /// Adds one of the player's palettes (renamed if its name is taken) after the one in use, or after the player's
    /// other palettes when a built-in one is in use, and uses it.
    /// </summary>
    public static Palette Add(Palette palette)
    {
        palette.Name = UniqueName(palette.Name, null);
        int index = All.Count > 0 && !Active.IsBuiltIn ? _active + 1 : OwnCount;
        All.Insert(index, palette);
        _active = index;
        Changed();
        return palette;
    }

    public static void Rename(Palette palette, string name)
    {
        name = Clean(name);
        if (!palette.IsBuiltIn && name.Length > 0 && name != palette.Name && All.Contains(palette))
        {
            palette.Name = UniqueName(name, palette);
            Changed();
        }
    }

    public static bool CanDelete => !Active.IsBuiltIn;

    public static void Delete()
    {
        if (CanDelete)
        {
            All.RemoveAt(_active);
            _active = Mathf.Clamp(_active, 0, All.Count - 1);
            Changed();
        }
    }

    /// <summary>
    /// The name, or the name with the lowest free number after it if another palette already has it.
    /// </summary>
    public static string UniqueName(string name, Palette except)
    {
        name = Clean(name);
        if (name.Length == 0)
        {
            name = "Palette";
        }

        if (IndexOf(name, except) < 0)
        {
            return name;
        }

        for (int number = 2; ; number++)
        {
            string suffix = " " + number;
            string candidate = (name.Length + suffix.Length > NameLimit ? name.Substring(0, NameLimit - suffix.Length).TrimEnd() : name) + suffix;
            if (IndexOf(candidate, except) < 0)
            {
                return candidate;
            }
        }
    }

    public static bool Same(Color32? a, Color32? b)
    {
        return a.HasValue == b.HasValue && (!a.HasValue || Near(a.Value, b.Value, 0));
    }

    /// <summary>
    /// Colours set from a slot go through hue, saturation and value and back, so by default allow a step of rounding.
    /// </summary>
    public static bool Near(Color32 a, Color32 b, int tolerance = 1)
    {
        return Mathf.Abs(a.r - b.r) <= tolerance && Mathf.Abs(a.g - b.g) <= tolerance && Mathf.Abs(a.b - b.b) <= tolerance;
    }

    private static string Clean(string name)
    {
        name = (name ?? "").Replace("\n", " ").Replace("\r", " ").Trim();
        return name.Length > NameLimit ? name.Substring(0, NameLimit).TrimEnd() : name;
    }

    private static int IndexOf(string name, Palette except)
    {
        if (name == null)
        {
            return -1;
        }

        for (int i = 0; i < All.Count; i++)
        {
            if (All[i] != except && string.Equals(All[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static void Changed()
    {
        Save();
        BrushState.PalettesChanged();
    }

    private static Palette FromData(PaletteData data)
    {
        Palette palette = new(UniqueName(data.Name, null));
        for (int i = 0; i < Palette.Size && data.Colors != null && i < data.Colors.Count; i++)
        {
            palette.Colors[i] = BrushState.TryParseHex(data.Colors[i], out Color32 color) ? color : null;
        }

        return palette;
    }

    private static void Save()
    {
        FileData data = new();
        foreach (Palette palette in All)
        {
            if (palette.IsBuiltIn)
            {
                continue;
            }

            PaletteData saved = new() { Name = palette.Name };
            foreach (Color32? color in palette.Colors)
            {
                saved.Colors.Add(color.HasValue ? BrushState.ToHex(color.Value) : "");
            }

            data.Palettes.Add(saved);
        }

        string path = FilePath;
        string temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonConvert.SerializeObject(data, Formatting.Indented));
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temp, path);
        }
        catch (Exception e)
        {
            Jotunn.Logger.LogWarning($"Couldn't save the palettes to {path}: {e.Message}");
        }
    }
}
