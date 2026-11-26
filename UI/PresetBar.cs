using System;
using System.Collections.Generic;

using PaintBrush.Palettes;

using UnityEngine;
using UnityEngine.UI;

using static PaintBrush.UI.UiParts;

namespace PaintBrush.UI;

/// <summary>
/// <para>
/// The row above the palette: arrows to step through the palettes, the palette's name (click it for a list of every
/// palette), and a ⋯ button with a menu to make, rename, duplicate, share and delete palettes.
/// </para>
/// <para>
/// Built-in palettes are marked as such and can't be renamed, deleted or changed: duplicate one to make it your own.
/// </para>
/// </summary>
internal static class PresetBar
{
    private const float RowHeight = 26f;
    private const float ListRowHeight = 24f;
    private const int ListVisibleRows = 8;
    private const float MenuWidth = 168f;
    private const float Padding = 4f;

    private static readonly Color IconColor = new(0.85f, 0.78f, 0.65f);
    private static readonly Color IconHover = new(1f, 0.72f, 0.36f);
    private static readonly Color Gold = new(1f, 0.72f, 0.36f);
    private static readonly Color RowNormal = new(1f, 1f, 1f, 0f);
    private static readonly Color RowHover = new(1f, 0.72f, 0.36f, 0.3f);
    private static readonly Color PopupBack = new(0.1f, 0.075f, 0.055f, 0.97f);

    private sealed class Entry(string token, Action action, Func<bool> shown = null, bool keepOpen = false)
    {
        public string Token { get; } = token;
        public Action Action { get; } = action;
        public Func<bool> Shown { get; } = shown;
        public bool KeepOpen { get; } = keepOpen;
    }

    private sealed class NameReceiver(string initial, Action<string> onEntered) : TextReceiver
    {
        public string GetText()
        {
            return initial;
        }

        public void SetText(string text)
        {
            onEntered(text);
        }
    }

    private static readonly List<Entry> Entries = [];

    private static Text _name;
    private static Color _labelColor;
    private static HoverButton _previous;
    private static HoverButton _next;
    private static RectTransform _nameButton;
    private static RectTransform _more;
    private static RectTransform _menu;
    private static RectTransform _list;
    private static RectTransform _listContent;
    private static ScrollRect _listScroll;
    private static Text _deleteText;
    private static bool _confirmingDelete;

    public static bool MenuOpen => IsOpen(_menu);

    public static bool ListOpen => IsOpen(_list);

    /// <summary>
    /// Whether the mouse is over an open menu or list, where right-click mustn't close the build menu.
    /// </summary>
    public static bool PopupHovered => (MenuOpen && Contains(_menu)) || (ListOpen && Contains(_list));

    public static void Create(RectTransform root, float x, float y, float width, float height, Color labelColor)
    {
        _labelColor = labelColor;
        Entries.Clear();
        Entries.Add(new Entry("$paintbrush_menu_new", New));
        Entries.Add(new Entry("$paintbrush_menu_rename", () => AskName(PaletteLibrary.Active), () => !PaletteLibrary.Active.IsBuiltIn));
        Entries.Add(new Entry("$paintbrush_menu_duplicate", () => PaletteLibrary.Add(
            PaletteLibrary.Active.CopyAs(Localize("$paintbrush_palette_copy_name", PaletteLibrary.Active.Name)))));
        Entries.Add(new Entry("$paintbrush_menu_copy_code", CopyCode));
        Entries.Add(new Entry("$paintbrush_menu_paste_code", PasteCode));
        Entries.Add(new Entry("$paintbrush_menu_delete", Delete, () => PaletteLibrary.CanDelete, keepOpen: true));

        _previous = Icon(root, "Previous", x, y, height, MakeArrow(right: false), () => PaletteLibrary.Cycle(-1));
        _next = Icon(root, "Next", x + width - (2f * height) - 6f, y, height, MakeArrow(right: true), () => PaletteLibrary.Cycle(1));
        _more = Icon(root, "More", x + width - height, y, height, MakeDots(), ToggleMenu).GetComponent<RectTransform>();

        // The name and a small down arrow, together one button that opens the list.
        float nameX = x + height + 4f;
        float nameWidth = width - (4f * height) - 4f;
        Image nameBack = Image(root, "Name", nameX, y - 2f, nameWidth, height + 4f, RowNormal, White);
        _nameButton = nameBack.rectTransform;
        HoverButton nameButton = nameBack.gameObject.AddComponent<HoverButton>();
        nameButton.Target = nameBack;
        nameButton.Normal = RowNormal;
        nameButton.Hover = RowHover;
        nameButton.Clicked = ToggleList;

        _name = Label(nameBack.transform, "", 4f, 0f, nameWidth - 22f, height + 4f, labelColor, 16);
        _name.alignment = TextAnchor.MiddleCenter;
        _name.resizeTextForBestFit = true;
        _name.resizeTextMinSize = 10;
        _name.resizeTextMaxSize = 16;
        Image arrow = Image(nameBack.transform, "Arrow", nameWidth - 16f, 4f, 12f, 12f, IconColor, MakeArrowDown());
        arrow.raycastTarget = false;

        _menu = Popup(root, "PaletteMenu", x + width - MenuWidth, y + height + 2f, MenuWidth);
        CreateList(root, nameX, y + height + 2f, nameWidth);
    }

    public static void Refresh()
    {
        if (_name == null)
        {
            return;
        }

        Palette active = PaletteLibrary.Active;
        _name.text = active.IsBuiltIn ? Localize("$paintbrush_palette_builtin_name", active.Name) : active.Name;
        bool several = PaletteLibrary.Palettes.Count > 1;
        _previous.Interactable = several;
        _next.Interactable = several;
        if (ListOpen)
        {
            FillList();
        }
    }

    /// <summary>
    /// Closes the menu and list when a mouse button goes down anywhere outside them (their buttons toggle them).
    /// </summary>
    public static void Update()
    {
        if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1))
        {
            return;
        }

        if (MenuOpen && !Contains(_menu) && !Contains(_more))
        {
            CloseMenu();
        }

        if (ListOpen && !Contains(_list) && !Contains(_nameButton))
        {
            _list.gameObject.SetActive(false);
        }
    }

    public static void Close()
    {
        CloseMenu();
        if (_list != null)
        {
            _list.gameObject.SetActive(false);
        }
    }

    private static void CloseMenu()
    {
        if (_menu != null)
        {
            _menu.gameObject.SetActive(false);
        }

        _confirmingDelete = false;
    }

    private static void ToggleMenu()
    {
        if (MenuOpen)
        {
            CloseMenu();
        }
        else
        {
            Close();
            OpenMenu();
        }
    }

    private static void ToggleList()
    {
        if (ListOpen)
        {
            _list.gameObject.SetActive(false);
            return;
        }

        Close();
        FillList();
        _list.gameObject.SetActive(true);
        _list.SetAsLastSibling();

        // Scroll so the palette in use shows.
        int index = IndexOfActive();
        float hidden = (_listContent.sizeDelta.y - _list.sizeDelta.y) / ListRowHeight;
        _listScroll.verticalNormalizedPosition = hidden > 0f ? 1f - Mathf.Clamp01((index - (ListVisibleRows / 2f)) / hidden) : 1f;
    }

    /// <summary>
    /// Builds the rows each time, as Rename and Delete only show for the player's own palettes.
    /// </summary>
    private static void OpenMenu()
    {
        Clear(_menu);
        _confirmingDelete = false;
        _deleteText = null;
        float y = Padding;
        foreach (Entry entry in Entries)
        {
            if (entry.Shown != null && !entry.Shown())
            {
                continue;
            }

            Text text = Row(_menu, y, MenuWidth, RowHeight, entry.Token, IconColor, () =>
            {
                entry.Action();
                if (!entry.KeepOpen)
                {
                    CloseMenu();
                }
            });

            if (entry.Token == "$paintbrush_menu_delete")
            {
                _deleteText = text;
            }

            y += RowHeight;
        }

        _menu.sizeDelta = new Vector2(MenuWidth, y + Padding);
        _menu.gameObject.SetActive(true);
        _menu.SetAsLastSibling();
    }

    private static void CreateList(RectTransform root, float x, float y, float width)
    {
        _list = Popup(root, "PaletteList", x, y, width);
        _list.sizeDelta = new Vector2(width, (ListVisibleRows * ListRowHeight) + (2f * Padding));

        RectTransform viewport = Box(_list, "Viewport", 0f, Padding, width, ListVisibleRows * ListRowHeight);
        _ = viewport.gameObject.AddComponent<RectMask2D>();
        _listContent = Box(viewport, "Content", 0f, 0f, width, 0f);

        _listScroll = _list.gameObject.AddComponent<ScrollRect>();
        _listScroll.viewport = viewport;
        _listScroll.content = _listContent;
        _listScroll.horizontal = false;
        _listScroll.movementType = ScrollRect.MovementType.Clamped;
        _listScroll.scrollSensitivity = ListRowHeight;
        _listScroll.inertia = false;
    }

    /// <summary>
    /// The player's palettes, then a heading and the built-in ones. The palette in use is gold.
    /// </summary>
    private static void FillList()
    {
        Clear(_listContent);
        float width = _list.sizeDelta.x;
        float y = 0f;
        bool heading = false;
        IReadOnlyList<Palette> palettes = PaletteLibrary.Palettes;
        for (int i = 0; i < palettes.Count; i++)
        {
            Palette palette = palettes[i];
            if (palette.IsBuiltIn && !heading)
            {
                heading = true;
                Text label = Label(_listContent, "$paintbrush_palette_builtin", Padding + 8f, y, width - 24f, ListRowHeight, _labelColor, 13);
                label.fontStyle = FontStyle.Italic;
                y += ListRowHeight;
            }

            int index = i;
            Color color = palette == PaletteLibrary.Active ? Gold : IconColor;
            Text text = Row(_listContent, y, width, ListRowHeight, "", color, () =>
            {
                PaletteLibrary.Select(index);
                _list.gameObject.SetActive(false);
            });
            text.text = palette.Name;
            y += ListRowHeight;
        }

        _listContent.sizeDelta = new Vector2(width, y);
    }

    private static int IndexOfActive()
    {
        IReadOnlyList<Palette> palettes = PaletteLibrary.Palettes;
        int index = 0;
        for (int i = 0; i < palettes.Count; i++)
        {
            // Count the heading row before the built-in palettes.
            if (palettes[i].IsBuiltIn && (i == 0 || !palettes[i - 1].IsBuiltIn))
            {
                index++;
            }

            if (palettes[i] == PaletteLibrary.Active)
            {
                return index;
            }

            index++;
        }

        return 0;
    }

    private static void New()
    {
        Palette palette = PaletteLibrary.Add(new Palette(Localize("$paintbrush_palette_new_name")));
        AskName(palette);
    }

    private static void AskName(Palette palette)
    {
        if (TextInput.instance != null && !palette.IsBuiltIn)
        {
            TextInput.instance.RequestText(new NameReceiver(palette.Name, name => PaletteLibrary.Rename(palette, name)),
                "$paintbrush_palette_name", PaletteLibrary.NameLimit);
        }
    }

    private static void CopyCode()
    {
        GUIUtility.systemCopyBuffer = PaletteCode.Encode(PaletteLibrary.Active);
        Message(Localize("$paintbrush_msg_code_copied", PaletteLibrary.Active.Name));
    }

    private static void PasteCode()
    {
        if (PaletteCode.TryDecode(GUIUtility.systemCopyBuffer, out Palette palette))
        {
            palette = PaletteLibrary.Add(palette);
            Message(Localize("$paintbrush_msg_code_pasted", palette.Name));
        }
        else
        {
            Message(Localize("$paintbrush_msg_code_invalid"));
        }
    }

    /// <summary>
    /// The first click asks for a second one, as a deleted palette can't be brought back.
    /// </summary>
    private static void Delete()
    {
        if (!_confirmingDelete)
        {
            _confirmingDelete = true;
            if (_deleteText != null)
            {
                _deleteText.text = Localize("$paintbrush_menu_delete_confirm");
            }

            return;
        }

        string name = PaletteLibrary.Active.Name;
        PaletteLibrary.Delete();
        CloseMenu();
        Message(Localize("$paintbrush_msg_palette_deleted", name));
    }

    private static RectTransform Popup(RectTransform root, string name, float x, float y, float width)
    {
        RectTransform popup = Box(root, name, x, y, width, 10f);
        Image back = popup.gameObject.AddComponent<Image>();
        back.sprite = White;
        back.color = PopupBack;
        Outline outline = back.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.72f, 0.36f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);
        popup.gameObject.SetActive(false);
        return popup;
    }

    /// <summary>
    /// A clickable row with a label, highlighted while the mouse is over it.
    /// </summary>
    private static Text Row(RectTransform parent, float y, float width, float height, string token, Color textColor, Action clicked)
    {
        Image row = Image(parent, "Row", Padding, y, width - (2f * Padding), height, RowNormal, White);
        Text text = Label(row.transform, token, 8f, 0f, width - (2f * Padding) - 12f, height, textColor, 15);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        HoverButton button = row.gameObject.AddComponent<HoverButton>();
        button.Target = row;
        button.Normal = RowNormal;
        button.Hover = RowHover;
        button.Clicked = clicked;
        return text;
    }

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }

    private static HoverButton Icon(RectTransform root, string name, float x, float y, float size, Sprite sprite, Action clicked)
    {
        Image image = Image(root, name, x, y, size, size, IconColor, sprite);
        image.preserveAspect = true;
        HoverButton button = image.gameObject.AddComponent<HoverButton>();
        button.Target = image;
        button.Normal = IconColor;
        button.Hover = IconHover;
        button.Clicked = clicked;
        return button;
    }

    private static bool IsOpen(RectTransform popup)
    {
        return popup != null && popup.gameObject.activeSelf;
    }

    private static bool Contains(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy)
        {
            return false;
        }

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, camera);
    }

    private static string Localize(string token, params string[] words)
    {
        return Localization.instance.Localize(token, words);
    }

    private static void Message(string text)
    {
        if (Player.m_localPlayer != null)
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, text);
        }
    }
}
