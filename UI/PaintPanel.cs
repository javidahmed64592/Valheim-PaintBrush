using System;
using System.Collections.Generic;
using System.Globalization;

using Jotunn.Managers;

using PaintBrush.Brush;
using PaintBrush.Painting;
using PaintBrush.Palettes;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using static PaintBrush.UI.UiParts;

namespace PaintBrush.UI;

/// <summary>
/// The colour picker inside the brush menu, above the tools: a saturation/value square and a hue bar, R, G, B and
/// Strength sliders with number fields, a hex field, a preview of the last piece aimed at (vanilla and painted),
/// the palettes (see <see cref="PresetBar"/>), the recent colours and shades of the current colour. Any colour can
/// be dragged onto the palette, palette colours can be dragged to reorder them or off the palette to remove them. It opens and closes with the menu, and every change applies at once.
/// </summary>
internal static class PaintPanel
{
    private const float Width = 1004f;
    private const float Height = 316f;

    /// <summary>
    /// Space between the panel and the tools below it.
    /// </summary>
    private const float Gap = 18f;

    /// <summary>
    /// How far the grid of tools is moved down to make room for the panel.
    /// </summary>
    private const float Shift = Height + Gap;

    /// <summary>
    /// How much taller the build menu's window gets (downwards) while the brush menu is open, so the tools below the
    /// panel fit without a scroll bar.
    /// </summary>
    private const float Grow = 48f;

    private const float SquareSize = 250f;
    private const int SquareResolution = 64;
    private const int GradientResolution = 64;

    private static readonly Color LabelColor = new(1f, 0.72f, 0.36f);
    private static readonly Color TextColor = new(0.85f, 0.78f, 0.65f);
    private static readonly Color EmptySlot = new(0.12f, 0.09f, 0.07f, 0.9f);
    private static readonly Color Wood = new(0.45f, 0.32f, 0.2f);

    private sealed class SliderRow
    {
        public RawImage Track;
        public Texture2D Gradient;
        public RectTransform Handle;
        public InputField Field;
        public int Max;
        public Func<int> Get;
        public Action<int> Set;
        public Func<float, Color> ColorAt;
    }

    private static readonly List<SliderRow> Rows = [];
    private static readonly List<InputField> Fields = [];
    private static readonly Swatch[] PaletteSwatches = new Swatch[Palette.Size];
    private static readonly Swatch[] ShadeSwatches = new Swatch[BrushState.ShadeCount];
    private static readonly Swatch[] RecentSwatches = new Swatch[BrushState.RecentSize];

    private static RectTransform _root;
    private static bool _failed;
    private static bool _updating;
    private static bool _blocking;

    private static Texture2D _squareTexture;
    private static Texture2D _hueTexture;
    private static Sprite _ring;
    private static RectTransform _squareKnob;
    private static RectTransform _hueMarker;
    private static InputField _hex;
    private static Image _current;
    private static RectTransform _paletteArea;
    private static RawImage _vanilla;
    private static RawImage _painted;

    private static RectTransform _shiftedView;
    private static RectTransform _grownWindow;

    private static int _shownVersion = -1;
    private static float _shownHue = -1f;
    private static Texture _shownSample;
    private static Color32 _sampleAverage;

    public static void Update(Player player)
    {
        BuildUi ui = Hud.instance != null ? Hud.instance.m_buildUi : null;
        bool show = player != null
            && ui != null
            && Hud.IsPieceSelectionVisible()
            && BrushPrefabs.IsBrushTable(ui.m_currentBuildTool);

        if (!show)
        {
            if (_root != null && _root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(false);
            }

            PresetBar.Close();
            ShiftGrid(null);
            SetBlocking(false);
            return;
        }

        if (_root == null && (_failed || !Create(ui)))
        {
            return;
        }

        if (!_root.gameObject.activeSelf)
        {
            _root.gameObject.SetActive(true);
            _shownVersion = -1;
        }

        Position(ui);
        Refresh();
        PresetBar.Update();
        SetBlocking(AnyFieldFocused());
    }

    /// <summary>
    /// Whether the brush menu's colour picker is showing.
    /// </summary>
    public static bool Visible => _root != null && _root.gameObject.activeInHierarchy;

    /// <summary>
    /// Whether a right-click now does something in the panel, so it mustn't also close the build menu.
    /// </summary>
    public static bool UsesRightClick => Visible && (Swatch.RightClickHovered || PresetBar.PopupHovered);

    /// <summary>
    /// Keeps keys typed into the number and hex fields from moving the player or switching tools.
    /// </summary>
    private static void SetBlocking(bool block)
    {
        if (block != _blocking)
        {
            _blocking = block;
            GUIManager.BlockInput(block);
        }
    }

    private static bool AnyFieldFocused()
    {
        foreach (InputField field in Fields)
        {
            if (field != null && field.isFocused)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Moves the grid of tools down by <see cref="Shift"/> and makes the window <see cref="Grow"/> taller while the
    /// brush menu is open, and undoes both when it closes (pass null). The changes are relative, so they add to
    /// whatever other mods (BetterHammer's toolbar) did.
    /// </summary>
    private static void ShiftGrid(RectTransform view)
    {
        if (_shiftedView == view)
        {
            return;
        }

        if (_shiftedView != null)
        {
            _shiftedView.offsetMax += new Vector2(0f, Shift);
        }

        if (_grownWindow != null)
        {
            _grownWindow.sizeDelta -= new Vector2(0f, Grow);
            _grownWindow.anchoredPosition += new Vector2(0f, Grow / 2f);
            _grownWindow = null;
        }

        _shiftedView = view;
        if (view != null)
        {
            view.offsetMax -= new Vector2(0f, Shift);

            // The window is centred, so moving it down by half keeps its top where it was.
            _grownWindow = view.GetComponentInParent<BuildUi>()?.transform.Find("bar") as RectTransform;
            if (_grownWindow != null)
            {
                _grownWindow.sizeDelta += new Vector2(0f, Grow);
                _grownWindow.anchoredPosition -= new Vector2(0f, Grow / 2f);
            }
        }
    }

    /// <summary>
    /// Puts the panel where the top of the grid was, before it moved down.
    /// </summary>
    private static void Position(BuildUi ui)
    {
        RectTransform view = ui.m_pieceView;
        if (view == null)
        {
            return;
        }

        ShiftGrid(view);
        RectTransform parent = (RectTransform)_root.parent;
        Vector3[] corners = new Vector3[4];
        view.GetWorldCorners(corners);
        Vector3 topLeft = parent.InverseTransformPoint(corners[1]);
        _root.anchoredPosition = new Vector2(topLeft.x - parent.rect.xMin, topLeft.y - parent.rect.yMax + Shift);
    }

    private static void Refresh()
    {
        Texture sample = null;
        bool cutout = false;
        Piece aimed = PaintPreview.LastAimed;
        if (aimed != null)
        {
            _ = PaintedPieces.SampleTexture(aimed, out sample, out cutout);
        }

        if (BrushState.Version == _shownVersion && sample == _shownSample)
        {
            return;
        }

        if (sample != _shownSample)
        {
            _shownSample = sample;
            _sampleAverage = sample != null ? PaintTextures.AverageColor(sample, cutout) : (Color32)Wood;
        }

        _shownVersion = BrushState.Version;
        _updating = true;
        try
        {
            Color32 color = BrushState.Color;
            if (!Mathf.Approximately(_shownHue, BrushState.Hue))
            {
                _shownHue = BrushState.Hue;
                FillSquare(_shownHue);
            }

            _squareKnob.anchoredPosition = new Vector2(BrushState.Saturation * SquareSize, -(1f - BrushState.Value) * SquareSize);
            _hueMarker.anchoredPosition = new Vector2(_hueMarker.anchoredPosition.x, -(1f - BrushState.Hue) * SquareSize);

            foreach (SliderRow row in Rows)
            {
                for (int i = 0; i < GradientResolution; i++)
                {
                    row.Gradient.SetPixel(i, 0, row.ColorAt(i / (float)(GradientResolution - 1)));
                }

                row.Gradient.Apply();
                float width = row.Track.rectTransform.rect.width;
                row.Handle.anchoredPosition = new Vector2(row.Get() / (float)row.Max * width, row.Handle.anchoredPosition.y);
                if (!row.Field.isFocused)
                {
                    row.Field.text = row.Get().ToString(CultureInfo.InvariantCulture);
                }
            }

            if (!_hex.isFocused)
            {
                _hex.text = BrushState.ToHex(color);
            }

            _current.color = color;

            if (sample != null)
            {
                _vanilla.texture = sample;
                _vanilla.color = Color.white;
                _painted.texture = PaintTextures.GetPreview(sample, cutout, BrushState.Paint);
                _painted.color = Color.white;
            }
            else
            {
                _vanilla.texture = Texture2D.whiteTexture;
                _vanilla.color = Wood;
                _painted.texture = Texture2D.whiteTexture;
                _painted.color = Color.Lerp(Wood, color, BrushState.Strength / 100f);
            }

            PresetBar.Refresh();
            for (int i = 0; i < PaletteSwatches.Length; i++)
            {
                Color32? slot = BrushState.Palette[i];
                PaletteSwatches[i].Image.color = slot ?? (Color32)EmptySlot;
                PaletteSwatches[i].Selected = slot.HasValue && PaletteLibrary.Near(slot.Value, color);
            }

            for (int i = 0; i < ShadeSwatches.Length; i++)
            {
                ShadeSwatches[i].Image.color = BrushState.Shade(i);
                ShadeSwatches[i].Selected = i == BrushState.ShadeIndex;
            }

            for (int i = 0; i < RecentSwatches.Length; i++)
            {
                bool used = i < BrushState.Recent.Count;
                RecentSwatches[i].Image.color = used ? BrushState.Recent[i] : EmptySlot;
            }
        }
        finally
        {
            _updating = false;
        }
    }

    private static void FillSquare(float hue)
    {
        for (int y = 0; y < SquareResolution; y++)
        {
            for (int x = 0; x < SquareResolution; x++)
            {
                _squareTexture.SetPixel(x, y, Color.HSVToRGB(hue, x / (float)(SquareResolution - 1), y / (float)(SquareResolution - 1)));
            }
        }

        _squareTexture.Apply();
    }

    private static bool Create(BuildUi ui)
    {
        Transform window = ui.transform.Find("bar/SelectionWindow");
        if (window == null)
        {
            _failed = true;
            Jotunn.Logger.LogWarning("Build menu layout not as expected, the paint brush colour picker is unavailable.");
            return false;
        }

        Rows.Clear();
        Fields.Clear();
        _shownVersion = -1;
        _shownHue = -1f;
        _shownSample = null;
        _ring ??= MakeRing();

        _root = Box(window, "PaintBrush_Panel", 0f, 0f, Width, Height);

        CreatePicker();
        CreateSliders();
        CreatePreview();
        CreateSwatches();
        CreateShades();
        DragGhost.Init(_root);
        return true;
    }

    private static void CreatePicker()
    {
        _squareTexture = new Texture2D(SquareResolution, SquareResolution, TextureFormat.RGBA32, mipChain: false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        RawImage square = Raw(_root, "Square", 0f, 0f, SquareSize, SquareSize, _squareTexture);
        square.gameObject.AddComponent<DragArea>().Changed = p => BrushState.SetHsv(BrushState.Hue, p.x, p.y);
        _squareKnob = Image(square.transform, "Knob", 0f, 0f, 16f, 16f, Color.white, _ring).rectTransform;
        _squareKnob.pivot = new Vector2(0.5f, 0.5f);
        _squareKnob.GetComponent<Image>().raycastTarget = false;

        _hueTexture = new Texture2D(1, 180, TextureFormat.RGBA32, mipChain: false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        for (int y = 0; y < _hueTexture.height; y++)
        {
            _hueTexture.SetPixel(0, y, Color.HSVToRGB(y / (float)(_hueTexture.height - 1), 1f, 1f));
        }

        _hueTexture.Apply();
        RawImage hue = Raw(_root, "Hue", SquareSize + 12f, 0f, 22f, SquareSize, _hueTexture);
        hue.gameObject.AddComponent<DragArea>().Changed = p => BrushState.SetHsv(Mathf.Min(p.y, 0.999f), BrushState.Saturation, BrushState.Value);
        Image marker = Image(hue.transform, "Marker", -5f, 0f, 32f, 4f, Color.white, White);
        marker.raycastTarget = false;
        marker.rectTransform.pivot = new Vector2(0f, 0.5f);
        Outline outline = marker.gameObject.AddComponent<Outline>();
        Swatch.Style(outline);
        _hueMarker = marker.rectTransform;
    }

    private static void CreateSliders()
    {
        AddRow("R", 0f, 255, () => BrushState.Color.r, v => SetChannel(0, v), t => WithChannel(0, t));
        AddRow("G", 44f, 255, () => BrushState.Color.g, v => SetChannel(1, v), t => WithChannel(1, t));
        AddRow("B", 88f, 255, () => BrushState.Color.b, v => SetChannel(2, v), t => WithChannel(2, t));
        AddRow("$paintbrush_panel_strength", 132f, 100, () => BrushState.Strength, BrushState.SetStrength,
            t => Color.Lerp(_sampleAverage, BrushState.Color, t));

        const float x = 318f;
        const float y = 184f;
        _ = Label(_root, "$paintbrush_panel_hex", x, y, 78f, 32f, LabelColor, 18);
        _hex = Field(_root, x + 82f, y, 110f, 32f, InputField.ContentType.Alphanumeric, 7);
        _hex.onEndEdit.AddListener(text =>
        {
            if (!_updating && BrushState.TryParseHex(text, out Color32 parsed))
            {
                BrushState.SetColor(parsed);
            }
            else
            {
                _shownVersion = -1;
            }
        });
        _current = Image(_root, "Current", x + 204f, y, 154f, 32f, Color.white, White);
        Outline currentOutline = _current.gameObject.AddComponent<Outline>();
        Swatch.Style(currentOutline);
        Swatch current = _current.gameObject.AddComponent<Swatch>();
        current.Outline = currentOutline;
        current.DragColor = () => BrushState.Color;

        _ = Label(_root, "$paintbrush_panel_hint", x, 224f, 358f, 40f, TextColor, 14);
    }

    private static void AddRow(string label, float y, int max, Func<int> get, Action<int> set, Func<float, Color> colorAt)
    {
        const float x = 318f;
        _ = Label(_root, label, x, y, 78f, 32f, LabelColor, 18);

        Texture2D gradient = new(GradientResolution, 1, TextureFormat.RGBA32, mipChain: false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        RawImage track = Raw(_root, "Track", x + 82f, y + 5f, 200f, 22f, gradient);
        Swatch.Style(track.gameObject.AddComponent<Outline>());
        track.gameObject.AddComponent<DragArea>().Changed = p => set(Mathf.RoundToInt(p.x * max));

        Image handle = Image(track.transform, "Handle", 0f, 11f, 6f, 30f, Color.white, White);
        handle.raycastTarget = false;
        handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        Swatch.Style(handle.gameObject.AddComponent<Outline>());

        InputField field = Field(_root, x + 294f, y, 64f, 32f, InputField.ContentType.IntegerNumber, 3);
        field.onValueChanged.AddListener(text =>
        {
            if (!_updating && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                set(Mathf.Clamp(value, 0, max));
            }
        });
        field.onEndEdit.AddListener(_ => _shownVersion = -1);

        Rows.Add(new SliderRow
        {
            Track = track,
            Gradient = gradient,
            Handle = handle.rectTransform,
            Field = field,
            Max = max,
            Get = get,
            Set = set,
            ColorAt = colorAt,
        });
    }

    private static void SetChannel(int channel, int value)
    {
        Color32 color = BrushState.Color;
        byte b = (byte)Mathf.Clamp(value, 0, 255);
        switch (channel)
        {
            case 0:
                color.r = b;
                break;
            case 1:
                color.g = b;
                break;
            default:
                color.b = b;
                break;
        }

        BrushState.SetColor(color);
    }

    private static Color WithChannel(int channel, float t)
    {
        Color color = BrushState.Color;
        color[channel] = t;
        return color;
    }

    private static void CreatePreview()
    {
        const float x = 712f;
        _vanilla = Raw(_root, "Vanilla", x, 0f, 140f, 64f, Texture2D.whiteTexture);
        _painted = Raw(_root, "Painted", x + 150f, 0f, 140f, 64f, Texture2D.whiteTexture);
        Swatch.Style(_vanilla.gameObject.AddComponent<Outline>());
        Swatch.Style(_painted.gameObject.AddComponent<Outline>());
        _vanilla.raycastTarget = false;
        _painted.raycastTarget = false;
        Label(_root, "$paintbrush_panel_vanilla", x, 68f, 140f, 20f, TextColor, 14).alignment = TextAnchor.MiddleCenter;
        Label(_root, "$paintbrush_panel_painted", x + 150f, 68f, 140f, 20f, TextColor, 14).alignment = TextAnchor.MiddleCenter;
    }

    private static void CreateSwatches()
    {
        const float x = 712f;
        PresetBar.Create(_root, x, 92f, 290f, 20f, LabelColor);

        // Letting go of a palette colour outside this area removes it; inside (between slots) does nothing.
        _paletteArea = Box(_root, "PaletteArea", x - 4f, 112f, 290f + 8f, 110f);
        for (int i = 0; i < PaletteSwatches.Length; i++)
        {
            int index = i;
            Swatch swatch = MakeSwatch(x + (i % 8 * 36f), 116f + (i / 8 * 36f));
            swatch.PaletteIndex = index;
            swatch.UsesRightClick = true;
            swatch.Clicked = button => OnPaletteClick(index, button);
            swatch.DragColor = () => BrushState.Palette[index];
            swatch.Dropped = source => OnPaletteDrop(index, source);
            swatch.DragMoved = eventData => DragGhost.SetRemoving(!InPaletteArea(eventData));
            swatch.DragEnded = (eventData, taken) =>
            {
                if (!taken && !InPaletteArea(eventData))
                {
                    PaletteEdit.Run(() => PaletteLibrary.Clear(index));
                }
            };
            PaletteSwatches[i] = swatch;
        }

        _ = Label(_root, "$paintbrush_panel_recent", x, 226f, 290f, 20f, LabelColor, 16);
        for (int i = 0; i < RecentSwatches.Length; i++)
        {
            int index = i;
            Swatch swatch = MakeSwatch(x + (i % 8 * 36f), 250f + (i / 8 * 36f));
            swatch.Clicked = button =>
            {
                if (button == PointerEventData.InputButton.Left && index < BrushState.Recent.Count)
                {
                    BrushState.SetColor(BrushState.Recent[index]);
                }
            };
            swatch.DragColor = () => index < BrushState.Recent.Count ? BrushState.Recent[index] : null;
            RecentSwatches[i] = swatch;
        }
    }

    /// <summary>
    /// Darker and lighter shades of a base colour, under the hint in the middle column. Choosing one keeps the base,
    /// so the row stays the same while switching between shades.
    /// </summary>
    private static void CreateShades()
    {
        const float x = 318f;
        const float y = 274f;
        const float size = 28f;
        _ = Label(_root, "$paintbrush_panel_shades", x, y - 2f, 78f, 32f, LabelColor, 18);
        for (int i = 0; i < ShadeSwatches.Length; i++)
        {
            int index = i;
            Swatch swatch = MakeSwatch(x + 82f + (i * (size + 3f)), y, size);
            swatch.Clicked = button =>
            {
                if (button == PointerEventData.InputButton.Left)
                {
                    BrushState.UseShade(index);
                }
            };
            swatch.DragColor = () => BrushState.Shade(index);
            ShadeSwatches[i] = swatch;
        }
    }

    private static bool InPaletteArea(PointerEventData eventData)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(_paletteArea, eventData.position, eventData.pressEventCamera);
    }

    /// <summary>
    /// A palette colour dropped on another slot swaps with it; any other colour is saved into the slot (or moved
    /// there, if the palette already has it).
    /// </summary>
    private static void OnPaletteDrop(int index, Swatch source)
    {
        if (source.PaletteIndex == index)
        {
            return;
        }

        if (source.PaletteIndex >= 0)
        {
            PaletteEdit.Run(() => PaletteLibrary.Swap(source.PaletteIndex, index));
        }
        else if (source.DragColor?.Invoke() is Color32 color)
        {
            PaletteEdit.Run(() => PaletteLibrary.Place(index, color));
        }
    }

    /// <summary>
    /// Click uses the slot's colour; Shift+Click saves the current colour into it; right-click empties it.
    /// </summary>
    private static void OnPaletteClick(int index, PointerEventData.InputButton button)
    {
        if (button == PointerEventData.InputButton.Right)
        {
            PaletteEdit.Run(() => PaletteLibrary.Clear(index));
        }
        else if (button == PointerEventData.InputButton.Left)
        {
            Color32? slot = BrushState.Palette[index];
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || !slot.HasValue)
            {
                PaletteEdit.Run(() => PaletteLibrary.Place(index, BrushState.Color));
            }
            else
            {
                BrushState.SetColor(slot.Value);
            }
        }
    }

    private static Swatch MakeSwatch(float x, float y, float size = 30f)
    {
        Image image = Image(_root, "Swatch", x, y, size, size, EmptySlot, White);
        Outline outline = image.gameObject.AddComponent<Outline>();
        Swatch.Style(outline);
        Swatch swatch = image.gameObject.AddComponent<Swatch>();
        swatch.Image = image;
        swatch.Outline = outline;
        return swatch;
    }

    private static InputField Field(Transform parent, float x, float y, float width, float height, InputField.ContentType type, int limit)
    {
        GameObject created = GUIManager.Instance.CreateInputField(parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
            type, "", 16, width, height);
        RectTransform rect = (RectTransform)created.transform;
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);

        InputField field = created.GetComponent<InputField>();
        field.characterLimit = limit;

        // Unity's input field insets its text a few pixels top and bottom, which leaves no room for a line in a
        // short box, so the text is stretched over the box instead.
        foreach (Graphic graphic in new Graphic[] { field.textComponent, field.placeholder })
        {
            if (graphic == null)
            {
                continue;
            }

            RectTransform textRect = graphic.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 1f);
            textRect.offsetMax = new Vector2(-6f, -1f);
            if (graphic is Text text)
            {
                text.alignment = TextAnchor.MiddleCenter;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        Fields.Add(field);
        return field;
    }
}
