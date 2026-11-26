using System;
using System.Collections.Generic;

using HarmonyLib;

using PaintBrush.Brush;
using PaintBrush.Palettes;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using Object = UnityEngine.Object;

namespace PaintBrush.UI;

/// <summary>
/// The key hints along the bottom while the brush menu is open. Vanilla's Favorite and Search hints don't apply to
/// the brush, so they're hidden and Close Menu stays. In their place, hints for the swatch under the mouse: use,
/// save, empty and move palette colours, or drag any other colour onto the palette. Vanilla is put back when the
/// brush menu closes.
/// </summary>
internal static class MenuKeyHints
{
    private const string Attack = "$KEY_Attack";
    private const string RightClick = "$KEY_BuildMenu";
    private const float FontSize = 18f;

    private enum Target
    {
        None = 0,
        FilledSlot = 1,
        EmptySlot = 2,
        LockedSlot = 3,
        Colour = 4,
        Source = 5,
    }

    private sealed class Hint(string label, string[] keys, Func<Target, bool> shown)
    {
        public string Label { get; } = label;
        public string[] Keys { get; } = keys;
        public Func<Target, bool> Shown { get; } = shown;
        public GameObject Item { get; set; }
    }

    /// <summary>
    /// Left to right, before vanilla's Close Menu.
    /// </summary>
    private static readonly Hint[] Hints =
    [
        new("$paintbrush_hint_use_colour", [Attack], t => t is Target.FilledSlot or Target.LockedSlot or Target.Colour),
        new("$paintbrush_hint_save_colour", [Attack], t => t == Target.EmptySlot),
        new("$paintbrush_hint_save_colour", ["Left Shift", Attack], t => t == Target.FilledSlot),
        new("$paintbrush_hint_empty_slot", [RightClick], t => t == Target.FilledSlot),
        new("$paintbrush_hint_move_colour", ["$paintbrush_key_drag"], t => t == Target.FilledSlot),
        new("$paintbrush_hint_remove_colour", ["$paintbrush_key_drag_off"], t => t == Target.FilledSlot),
        new("$paintbrush_hint_save_to_palette", ["$paintbrush_key_drag"], t => t is Target.Colour or Target.Source),
    ];

    private static readonly string[] HiddenVanilla = ["Favorite", "Search"];

    /// <summary>
    /// Read by reflection, as the game assemblies CI builds against are older than the field.
    /// </summary>
    private static readonly AccessTools.FieldRef<KeyHints, GameObject> BuildUiHints =
        AccessTools.Field(typeof(KeyHints), "m_buildUIHints") != null
            ? AccessTools.FieldRefAccess<KeyHints, GameObject>("m_buildUIHints")
            : null;

    private static readonly List<GameObject> Hidden = [];
    private static Transform _keyboard;
    private static bool _createFailed;
    private static bool _active;

    /// <summary>
    /// Called after vanilla's KeyHints.UpdateHints.
    /// </summary>
    internal static void AfterUpdateHints(KeyHints keyHints)
    {
        // The hints live under the HUD, which is rebuilt with each world load.
        if (!ReferenceEquals(_keyboard, null) && _keyboard == null)
        {
            _keyboard = null;
            _createFailed = false;
            _active = false;
            Hidden.Clear();
        }

        GameObject hints = BuildUiHints?.Invoke(keyHints);
        bool show = PaintPanel.Visible
            && hints != null
            && hints.activeInHierarchy
            && !ZInput.IsGamepadActive();

        if (!show)
        {
            SetActive(false);
            return;
        }

        if (_keyboard == null && (_createFailed || !Create(hints)))
        {
            return;
        }

        SetActive(true);
        Target target = CurrentTarget();
        foreach (Hint hint in Hints)
        {
            bool shown = hint.Shown(target);
            if (hint.Item.activeSelf != shown)
            {
                hint.Item.SetActive(shown);
            }
        }
    }

    private static Target CurrentTarget()
    {
        Swatch swatch = Swatch.Hovered;
        if (swatch == null)
        {
            return Target.None;
        }

        if (swatch.PaletteIndex >= 0)
        {
            bool filled = BrushState.Palette[swatch.PaletteIndex].HasValue;
            return (PaletteLibrary.Active.IsBuiltIn, filled) switch
            {
                (true, true) => Target.LockedSlot,
                (true, false) => Target.None,
                (false, true) => Target.FilledSlot,
                _ => Target.EmptySlot,
            };
        }

        if (swatch.DragColor?.Invoke() == null)
        {
            return Target.None;
        }

        // The current colour bar can only be dragged; the other swatches can also be clicked.
        return swatch.Clicked != null ? Target.Colour : Target.Source;
    }

    /// <summary>
    /// Hides vanilla's hints that don't apply and shows ours, or puts vanilla back.
    /// </summary>
    private static void SetActive(bool active)
    {
        if (active == _active || _keyboard == null)
        {
            return;
        }

        _active = active;
        if (active)
        {
            Hidden.Clear();
            foreach (string name in HiddenVanilla)
            {
                Transform item = _keyboard.Find(name);
                if (item != null && item.gameObject.activeSelf)
                {
                    item.gameObject.SetActive(false);
                    Hidden.Add(item.gameObject);
                }
            }

            return;
        }

        foreach (GameObject item in Hidden)
        {
            if (item != null)
            {
                item.SetActive(true);
            }
        }

        Hidden.Clear();
        foreach (Hint hint in Hints)
        {
            if (hint.Item != null)
            {
                hint.Item.SetActive(false);
            }
        }
    }

    private static bool Create(GameObject hints)
    {
        Transform keyboard = hints.transform.Find("Keyboard");
        Transform search = keyboard != null ? keyboard.Find("Search") : null;
        Transform close = keyboard != null ? keyboard.Find("Close") : null;
        Transform labelTemplate = search != null ? search.Find("Text") : null;
        Transform keyTemplate = search != null ? search.Find("key_bkg") : null;
        if (close == null || labelTemplate == null || keyTemplate == null)
        {
            _createFailed = true;
            Jotunn.Logger.LogWarning("Build menu key hints layout not as expected, not changing them for the brush menu.");
            return false;
        }

        foreach (Hint hint in Hints)
        {
            // A vanilla hint, emptied: a label, then the keys.
            GameObject item = Object.Instantiate(search.gameObject, keyboard, false);
            item.name = "PaintBrush_MenuHint";
            for (int i = item.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(item.transform.GetChild(i).gameObject);
            }

            _ = AddText(labelTemplate, item.transform, Localization.instance.Localize(hint.Label));
            for (int i = 0; i < hint.Keys.Length; i++)
            {
                if (i > 0)
                {
                    _ = AddText(labelTemplate, item.transform, "+");
                }

                GameObject key = Object.Instantiate(keyTemplate.gameObject, item.transform, false);
                key.SetActive(true);
                key.GetComponentInChildren<TextMeshProUGUI>().text = Localization.instance.Localize(hint.Keys[i]);
            }

            item.transform.SetSiblingIndex(close.GetSiblingIndex());
            item.SetActive(false);
            hint.Item = item;
        }

        _keyboard = keyboard;
        _active = false;
        return true;
    }

    private static TextMeshProUGUI AddText(Transform template, Transform parent, string text)
    {
        GameObject clone = Object.Instantiate(template.gameObject, parent, false);
        clone.SetActive(true);

        // Vanilla labels can have a fixed width sized for their own text.
        LayoutElement fixedSize = clone.GetComponent<LayoutElement>();
        if (fixedSize != null)
        {
            Object.DestroyImmediate(fixedSize);
        }

        TextMeshProUGUI label = clone.GetComponent<TextMeshProUGUI>();
        label.enableAutoSizing = false;
        label.fontSize = FontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.text = text;
        return label;
    }
}
