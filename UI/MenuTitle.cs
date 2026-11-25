using BepInEx.Bootstrap;

using PaintBrush.Brush;

using TMPro;

using UnityEngine;

namespace PaintBrush.UI;

/// <summary>
/// The brush's piece table has no tabs. Where the tabs would be, the menu shows a "Paint Brush" title with the line
/// under the tabs as its underline, and the Q / E hint for switching tabs (which do nothing here) is hidden. Both
/// come back when another tool's menu opens; BetterHoe, if installed, sets them for its own tools instead.
/// </summary>
internal static class MenuTitle
{
    private const string TitleName = "PaintBrush_MenuTitle";
    private const string BetterHoeGuid = "javidahmed64592.betterhoe";

    /// <summary>
    /// Vanilla's orange for headings, as in the selected piece's name.
    /// </summary>
    private static readonly Color TitleColor = new(1f, 0.72f, 0.36f);

    private static TextMeshProUGUI _title;
    private static bool _changed;

    internal static void AfterUpdateTagButtons(BuildUi buildUi)
    {
        RectTransform tabs = buildUi.m_tabContainer;
        if (tabs == null || tabs.parent == null)
        {
            return;
        }

        bool brush = BrushPrefabs.IsBrushTable(buildUi.m_currentBuildTool);
        if (brush)
        {
            SetActive(tabs.parent.Find("InputHelp"), false);
            SetActive(tabs.parent.Find("TabBorder"), true);
            _changed = true;
            if (_title == null)
            {
                _title = CreateTitle(tabs);
            }
        }
        else if (_changed)
        {
            _changed = false;
            if (!Chainloader.PluginInfos.ContainsKey(BetterHoeGuid))
            {
                SetActive(tabs.parent.Find("InputHelp"), true);
            }
        }

        if (_title != null && _title.gameObject.activeSelf != brush)
        {
            _title.gameObject.SetActive(brush);
        }
    }

    private static void SetActive(Transform item, bool active)
    {
        if (item != null && item.gameObject.activeSelf != active)
        {
            item.gameObject.SetActive(active);
        }
    }

    /// <summary>
    /// A label covering the tab row, in the tab labels' font.
    /// </summary>
    private static TextMeshProUGUI CreateTitle(RectTransform tabs)
    {
        TextMeshProUGUI template = tabs.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
        if (template == null)
        {
            return null;
        }

        RectTransform rect = new GameObject(TitleName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(tabs.parent, false);
        rect.anchorMin = tabs.anchorMin;
        rect.anchorMax = tabs.anchorMax;
        rect.pivot = tabs.pivot;
        rect.anchoredPosition = tabs.anchoredPosition;
        rect.sizeDelta = tabs.sizeDelta;

        // TextMeshPro is added while the title is inactive so its Awake runs after the font is set. Otherwise it looks
        // for its default font (LiberationSans SDF), which Valheim doesn't ship, and logs a warning.
        rect.gameObject.SetActive(false);
        TextMeshProUGUI title = rect.gameObject.AddComponent<TextMeshProUGUI>();
        title.font = template.font;
        title.fontSharedMaterial = template.fontSharedMaterial;
        rect.gameObject.SetActive(true);
        title.fontSize = template.fontSize * 1.25f;
        title.enableAutoSizing = false;
        title.color = TitleColor;
        title.alignment = TextAlignmentOptions.Center;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.raycastTarget = false;
        title.text = Localization.instance.Localize("$paintbrush_menu_title");
        return title;
    }
}
