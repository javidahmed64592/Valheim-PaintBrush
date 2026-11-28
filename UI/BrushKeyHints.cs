using System;
using System.Collections.Generic;

using PaintBrush.Brush;
using PaintBrush.Core;
using PaintBrush.History;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using Object = UnityEngine.Object;

namespace PaintBrush.UI;

/// <summary>
/// <para>
/// The key hints while the brush is out. Vanilla's build hint row stays, with Place relabelled for the selected
/// tool, the build menu hint renamed, Shift relabelled for the eyedropper, and the hints that do nothing with the
/// brush (remove, snap, copy, rotate, favourite) hidden.
/// </para>
/// <para>
/// Above its right end, a stack built from clones of the vanilla hint parts shows the brush's own keys: Alt+Click
/// for connected pieces, undo and redo. A hint that doesn't apply right now is invisible but keeps its row, so
/// nothing moves. Everything vanilla is put back when the brush is put away.
/// </para>
/// </summary>
internal static class BrushKeyHints
{
    private const string ContainerName = "PaintBrush_KeyHints";
    private const string UndoKey = "@undo";
    private const string RedoKey = "@redo";
    private const float LabelFontSize = 16f;
    private const float KeyHeight = 32f;
    private const float RowSpacing = 16f;

    /// <summary>
    /// Space between the stack and the vanilla row under it.
    /// </summary>
    private const float Gap = 16f;

    private sealed class Hint
    {
        public string[] Keys;

        /// <summary>
        /// The hint's label token for the selected tool, or null to hide it.
        /// </summary>
        public Func<BrushTool, string> Label;
        public CanvasGroup Group;
        public TextMeshProUGUI LabelText;
        public string ShownLabel;
    }

    /// <summary>
    /// Top to bottom.
    /// </summary>
    private static readonly List<Hint> Hints =
    [
        new Hint { Keys = [UndoKey], Label = _ => PaintHistory.CanUndo ? "$paintbrush_hint_undo" : null },
        new Hint { Keys = [RedoKey], Label = _ => PaintHistory.CanRedo ? "$paintbrush_hint_redo" : null },
        new Hint
        {
            Keys = ["Left Alt", "$KEY_Attack"],
            Label = tool => tool switch
            {
                BrushTool.Paint => "$paintbrush_hint_paint_connected",
                BrushTool.Clear => "$paintbrush_hint_clear_connected",
                _ => null,
            },
        },
    ];

    private static readonly string[] AllLabels =
    [
        "$paintbrush_hint_undo", "$paintbrush_hint_redo", "$paintbrush_hint_paint_connected", "$paintbrush_hint_clear_connected",
    ];

    private sealed class VanillaHint
    {
        public GameObject Item;
        public TextMeshProUGUI Label;
        public string OriginalText;
        public bool Changed;
    }

    private static readonly string[] VanillaNames = ["Place", "Remove", "BuildMenu", "AltPlace", "Snap", "Copy", "rotate", "Favorite"];
    private static readonly Dictionary<string, VanillaHint> Vanilla = [];
    private static RectTransform _keyboard;
    private static GameObject _container;
    private static RectTransform _stack;
    private static bool _createFailed;
    private static bool _layoutDirty;

    /// <summary>
    /// Called after vanilla's KeyHints.UpdateHints.
    /// </summary>
    internal static void AfterUpdateHints(KeyHints keyHints)
    {
        // The hints live under the HUD, which is rebuilt with each world load.
        if (!ReferenceEquals(_keyboard, null) && _keyboard == null)
        {
            Reset();
        }

        BrushTool tool = BrushTools.Selected(Player.m_localPlayer);
        bool brush = tool != BrushTool.None
            && keyHints.m_buildHints != null
            && keyHints.m_buildHints.activeInHierarchy
            && !ZInput.IsGamepadActive();

        if (!brush)
        {
            Restore();
            SetContainerActive(false);
            return;
        }

        if (_keyboard == null && (_createFailed || !Create(keyHints)))
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

        // The stack is for the world; the menu has its own hints.
        bool showStack = !Hud.IsPieceSelectionVisible();
        SetContainerActive(showStack);
        if (!showStack)
        {
            return;
        }

        foreach (Hint hint in Hints)
        {
            string label = hint.Label(tool);
            hint.Group.alpha = label != null ? 1f : 0f;
            if (label != null && label != hint.ShownLabel)
            {
                hint.ShownLabel = label;
                hint.LabelText.text = Localization.instance.Localize(label);
            }
        }

        if (_layoutDirty)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_stack);
            _layoutDirty = false;
        }

        Align();
    }

    private static bool Create(KeyHints keyHints)
    {
        Transform keyboard = keyHints.m_buildHints.transform.Find("Keyboard");
        Transform copy = keyboard != null ? keyboard.Find("Copy") : null;
        Transform labelTemplate = copy != null ? copy.Find("Text") : null;
        Transform plusTemplate = copy != null ? copy.Find("Text (1)") : null;
        Transform keyTemplate = copy != null ? copy.Find("key_bkg") : null;
        HorizontalLayoutGroup rowLayout = copy != null ? copy.GetComponent<HorizontalLayoutGroup>() : null;
        if (labelTemplate == null || plusTemplate == null || keyTemplate == null || rowLayout == null)
        {
            _createFailed = true;
            Jotunn.Logger.LogWarning("Vanilla build key hints layout not as expected, not changing the brush's key hints.");
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

        RectTransform container = new GameObject(ContainerName, typeof(RectTransform)).GetComponent<RectTransform>();
        container.SetParent(keyHints.m_buildHints.transform, false);
        RectTransform stack = CreateStack(container);
        string longest = LongestLabel();

        foreach (Hint hint in Hints)
        {
            // A vanilla multi-key hint (for its row settings), emptied: a label, then a group with the keys.
            GameObject item = Object.Instantiate(copy.gameObject, stack, false);
            item.name = "PaintBrush_Hint";
            item.SetActive(true);
            for (int i = item.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(item.transform.GetChild(i).gameObject);
            }

            // Sized with the longest label first, so the column fits every label.
            hint.LabelText = AddText(labelTemplate, item.transform, longest);
            hint.LabelText.alignment = TextAlignmentOptions.Right;
            hint.ShownLabel = null;

            Transform keys = CreateKeyGroup(item.transform, rowLayout);
            string[] keyNames = ExpandShortcuts(hint.Keys);
            for (int i = 0; i < keyNames.Length; i++)
            {
                if (i > 0)
                {
                    _ = AddText(plusTemplate, keys, "+");
                }

                GameObject key = Object.Instantiate(keyTemplate.gameObject, keys, false);
                key.SetActive(true);
                key.GetComponentInChildren<TextMeshProUGUI>().text = Localization.instance.Localize(keyNames[i]);
            }

            hint.Group = item.GetComponent<CanvasGroup>() ?? item.AddComponent<CanvasGroup>();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(stack);
        EqualiseWidths();
        foreach (Hint hint in Hints)
        {
            hint.LabelText.text = "";
        }

        _container = container.gameObject;
        _stack = stack;
        _keyboard = (RectTransform)keyboard;
        _layoutDirty = true;
        return true;
    }

    /// <summary>
    /// The stack's bottom right corner sits just above the right end of the vanilla row.
    /// </summary>
    private static void Align()
    {
        Vector3[] corners = new Vector3[4];
        _keyboard.GetWorldCorners(corners); // 0 = bottom left, 1 = top left, 2 = top right, 3 = bottom right
        HorizontalLayoutGroup layout = _keyboard.GetComponent<HorizontalLayoutGroup>();
        float right = layout != null ? layout.padding.right * _keyboard.lossyScale.x : 0f;
        _stack.position = new Vector3(corners[2].x - right, corners[2].y + (Gap * _keyboard.lossyScale.y), corners[2].z);
    }

    private static RectTransform CreateStack(RectTransform parent)
    {
        RectTransform stack = new GameObject(ContainerName + "_Stack", typeof(RectTransform)).GetComponent<RectTransform>();
        stack.SetParent(parent, false);
        stack.pivot = new Vector2(1f, 0f);

        VerticalLayoutGroup layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = RowSpacing;
        layout.childAlignment = TextAnchor.LowerRight;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = stack.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return stack;
    }

    /// <summary>
    /// The keys of a row in their own group, laid out like a vanilla hint, starting from the left.
    /// </summary>
    private static Transform CreateKeyGroup(Transform row, HorizontalLayoutGroup template)
    {
        GameObject group = new("Keys", typeof(RectTransform));
        group.transform.SetParent(row, false);
        HorizontalLayoutGroup layout = group.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = template.spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = template.childControlWidth;
        layout.childControlHeight = template.childControlHeight;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ((RectTransform)group.transform).sizeDelta = new Vector2(0f, KeyHeight);
        LayoutElement size = group.AddComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = KeyHeight;
        return group.transform;
    }

    /// <summary>
    /// Gives every row's label and key group the same widths, so labels line up on the right and keys on the left.
    /// </summary>
    private static void EqualiseWidths()
    {
        for (int part = 0; part < 2; part++)
        {
            float width = 0f;
            foreach (Hint hint in Hints)
            {
                width = Mathf.Max(width, LayoutUtility.GetPreferredWidth((RectTransform)hint.Group.transform.GetChild(part)));
            }

            // The label with the most letters isn't always the widest, so measure every label.
            if (part == 0 && Hints.Count > 0)
            {
                TextMeshProUGUI measure = Hints[0].LabelText;
                foreach (string token in AllLabels)
                {
                    width = Mathf.Max(width, measure.GetPreferredValues(Localization.instance.Localize(token)).x);
                }
            }

            foreach (Hint hint in Hints)
            {
                GameObject child = hint.Group.transform.GetChild(part).gameObject;
                LayoutElement layout = child.GetComponent<LayoutElement>() ?? child.AddComponent<LayoutElement>();
                layout.minWidth = width;
                layout.preferredWidth = width;
            }
        }
    }

    private static string LongestLabel()
    {
        string longest = "";
        foreach (string token in AllLabels)
        {
            string text = Localization.instance.Localize(token);
            if (text.Length > longest.Length)
            {
                longest = text;
            }
        }

        return longest;
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
        label.fontSize = LabelFontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.text = text;
        return label;
    }

    /// <summary>
    /// The undo and redo keybinds as their modifier and key, named as the vanilla hints name them.
    /// </summary>
    private static string[] ExpandShortcuts(string[] keys)
    {
        List<string> expanded = [];
        foreach (string key in keys)
        {
            KeyBinding binding = key switch
            {
                UndoKey => PaintHistory.UndoKey,
                RedoKey => PaintHistory.RedoKey,
                _ => null,
            };
            if (binding == null)
            {
                expanded.Add(key);
            }
            else
            {
                expanded.AddRange(binding.HintKeys());
            }
        }

        return [.. expanded];
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

    private static void SetContainerActive(bool active)
    {
        if (_container != null && _container.activeSelf != active)
        {
            _container.SetActive(active);
            _layoutDirty |= active;
        }
    }

    private static void Reset()
    {
        _keyboard = null;
        _container = null;
        _stack = null;
        _createFailed = false;
        Vanilla.Clear();
        foreach (Hint hint in Hints)
        {
            hint.Group = null;
            hint.LabelText = null;
            hint.ShownLabel = null;
        }
    }
}
