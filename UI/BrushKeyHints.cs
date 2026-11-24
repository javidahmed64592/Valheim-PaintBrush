using System.Collections.Generic;

using PaintBrush.Brush;

using TMPro;

using UnityEngine;

namespace PaintBrush.UI;

/// <summary>
/// The key hints while the brush is out: vanilla's build hint row, with Place relabelled for the selected tool and
/// the build menu hint renamed, and the hints that do nothing with the brush (remove, snap, copy, rotate, favourite)
/// hidden.
/// Everything is put back when the brush is put away.
/// </summary>
internal static class BrushKeyHints
{
    private sealed class VanillaHint
    {
        public GameObject Item;
        public TextMeshProUGUI Label;
        public string OriginalText;
        public bool Changed;
    }

    private static readonly string[] VanillaNames = ["Place", "Remove", "BuildMenu", "AltPlace", "Snap", "Copy", "rotate", "Favorite"];
    private static readonly Dictionary<string, VanillaHint> Vanilla = [];
    private static Transform _keyboard;

    /// <summary>
    /// Called after vanilla's KeyHints.UpdateHints.
    /// </summary>
    internal static void AfterUpdateHints(KeyHints keyHints)
    {
        // The hints live under the HUD, which is rebuilt with each world load.
        if (!ReferenceEquals(_keyboard, null) && _keyboard == null)
        {
            _keyboard = null;
            Vanilla.Clear();
        }

        BrushTool tool = BrushTools.Selected(Player.m_localPlayer);
        bool brush = tool != BrushTool.None
            && keyHints.m_buildHints != null
            && keyHints.m_buildHints.activeInHierarchy
            && !ZInput.IsGamepadActive();

        if (!brush)
        {
            Restore();
            return;
        }

        if (_keyboard == null && !Find(keyHints))
        {
            return;
        }

        Set("Place", true, tool switch
        {
            BrushTool.Paint => "$paintbrush_hint_paint",
            BrushTool.Clear => "$paintbrush_hint_clear",
            _ => "$paintbrush_hint_pick",
        });
        Set("Remove", false, null);
        Set("BuildMenu", true, "$paintbrush_hint_menu");
        Set("AltPlace", tool == BrushTool.Pick, "$paintbrush_hint_pick_vanilla");
        Set("Snap", false, null);
        Set("Copy", false, null);
        Set("rotate", false, null);
        Set("Favorite", false, null);
    }

    private static bool Find(KeyHints keyHints)
    {
        Transform keyboard = keyHints.m_buildHints.transform.Find("Keyboard");
        if (keyboard == null)
        {
            return false;
        }

        Vanilla.Clear();
        foreach (string name in VanillaNames)
        {
            Transform item = keyboard.Find(name);
            if (item == null)
            {
                continue;
            }

            TextMeshProUGUI label = item.Find("Text")?.GetComponent<TextMeshProUGUI>();
            Vanilla[name] = new VanillaHint { Item = item.gameObject, Label = label, OriginalText = label != null ? label.text : null };
        }

        _keyboard = keyboard;
        return true;
    }

    /// <summary>
    /// Sets a vanilla hint's visibility and, if `label` is given, its text.
    /// </summary>
    private static void Set(string name, bool active, string label)
    {
        if (!Vanilla.TryGetValue(name, out VanillaHint hint) || hint.Item == null)
        {
            return;
        }

        if (hint.Item.activeSelf != active)
        {
            hint.Item.SetActive(active);
            hint.Changed = true;
        }

        if (hint.Label == null || !active || label == null)
        {
            return;
        }

        string text = Localization.instance.Localize(label);
        if (hint.Label.text != text)
        {
            hint.Label.text = text;
            hint.Changed = true;
        }
    }

    private static void Restore()
    {
        foreach (VanillaHint hint in Vanilla.Values)
        {
            if (!hint.Changed || hint.Item == null)
            {
                continue;
            }

            hint.Item.SetActive(true);
            if (hint.Label != null)
            {
                hint.Label.text = hint.OriginalText;
            }

            hint.Changed = false;
        }
    }
}
