using System;
using System.Collections.Generic;
using System.Globalization;

using Jotunn.Managers;

using PaintBrush.Brush;
using PaintBrush.Painting;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaintBrush.UI;

/// <summary>
/// The colour picker inside the brush menu, above the tools: a saturation/value square and a hue bar, R, G, B and
/// Strength sliders with number fields, a hex field, a preview of the last piece aimed at (vanilla and painted),
/// the saved palette and the recent colours. It opens and closes with the menu, and every change applies at once.
/// </summary>
internal static class PaintPanel
{
    private const float Width = 1004f;
    private const float Height = 272f;

    /// <summary>
    /// Space between the panel and the tools below it.
    /// </summary>
    private const float Gap = 18f;

    /// <summary>
    /// How far the grid of tools is moved down to make room for the panel.
    /// </summary>
    private const float Shift = Height + Gap;

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
    private static readonly Swatch[] PaletteSwatches = new Swatch[BrushState.PaletteSize];
    private static readonly Swatch[] RecentSwatches = new Swatch[BrushState.RecentSize];

    private static RectTransform _root;
    private static bool _failed;
    private static bool _updating;
    private static bool _blocking;

    private static Texture2D _squareTexture;
    private static Texture2D _hueTexture;
    private static Sprite _white;
    private static Sprite _ring;
    private static RectTransform _squareKnob;
    private static RectTransform _hueMarker;
    private static InputField _hex;
    private static Image _current;
    private static RawImage _vanilla;
    private static RawImage _painted;

    private static RectTransform _shiftedView;

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
        SetBlocking(AnyFieldFocused());
    }

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
    /// Moves the grid of tools down by <see cref="Shift"/> while the brush menu is open, and back when it closes
    /// (pass null). The move is relative, so it adds to whatever other mods (BetterHammer's toolbar) did to the grid.
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

        _shiftedView = view;
        if (view != null)
        {
            view.offsetMax -= new Vector2(0f, Shift);
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

            for (int i = 0; i < PaletteSwatches.Length; i++)
            {
                Color32? slot = BrushState.Palette[i];
                PaletteSwatches[i].Image.color = slot ?? (Color32)EmptySlot;
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
        _white ??= Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        _ring ??= MakeRing();

        _root = Box(window, "PaintBrush_Panel", 0f, 0f, Width, Height);

        CreatePicker();
        CreateSliders();
        CreatePreview();
        CreateSwatches();
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
        Image marker = Image(hue.transform, "Marker", -5f, 0f, 32f, 4f, Color.white, _white);
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
        _current = Image(_root, "Current", x + 204f, y, 154f, 32f, Color.white, _white);
        _current.raycastTarget = false;
        Swatch.Style(_current.gameObject.AddComponent<Outline>());

        _ = Label(_root, "$paintbrush_panel_hint", x, 232f, 358f, 24f, TextColor, 14);
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

        Image handle = Image(track.transform, "Handle", 0f, 11f, 6f, 30f, Color.white, _white);
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
        _vanilla = Raw(_root, "Vanilla", x, 0f, 140f, 88f, Texture2D.whiteTexture);
        _painted = Raw(_root, "Painted", x + 150f, 0f, 140f, 88f, Texture2D.whiteTexture);
        Swatch.Style(_vanilla.gameObject.AddComponent<Outline>());
        Swatch.Style(_painted.gameObject.AddComponent<Outline>());
        _vanilla.raycastTarget = false;
        _painted.raycastTarget = false;
        Label(_root, "$paintbrush_panel_vanilla", x, 92f, 140f, 20f, TextColor, 14).alignment = TextAnchor.MiddleCenter;
        Label(_root, "$paintbrush_panel_painted", x + 150f, 92f, 140f, 20f, TextColor, 14).alignment = TextAnchor.MiddleCenter;
    }

    private static void CreateSwatches()
    {
        const float x = 712f;
        _ = Label(_root, "$paintbrush_panel_palette", x, 116f, 290f, 20f, LabelColor, 16);
        for (int i = 0; i < PaletteSwatches.Length; i++)
        {
            int index = i;
            Swatch swatch = MakeSwatch(x + (i % 8 * 36f), 140f + (i / 8 * 36f));
            swatch.Clicked = button => OnPaletteClick(index, button);
            PaletteSwatches[i] = swatch;
        }

        _ = Label(_root, "$paintbrush_panel_recent", x, 214f, 290f, 20f, LabelColor, 16);
        for (int i = 0; i < RecentSwatches.Length; i++)
        {
            int index = i;
            Swatch swatch = MakeSwatch(x + (i * 36f), 238f);
            swatch.Clicked = button =>
            {
                if (button == PointerEventData.InputButton.Left && index < BrushState.Recent.Count)
                {
                    BrushState.SetColor(BrushState.Recent[index]);
                }
            };
            RecentSwatches[i] = swatch;
        }
    }

    /// <summary>
    /// Click uses the slot's colour; Shift+Click saves the current colour into it; right-click empties it.
    /// </summary>
    private static void OnPaletteClick(int index, PointerEventData.InputButton button)
    {
        if (button == PointerEventData.InputButton.Right)
        {
            BrushState.SetPaletteSlot(index, null);
        }
        else if (button == PointerEventData.InputButton.Left)
        {
            Color32? slot = BrushState.Palette[index];
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || !slot.HasValue)
            {
                BrushState.SetPaletteSlot(index, BrushState.Color);
            }
            else
            {
                BrushState.SetColor(slot.Value);
            }
        }
    }

    private static Swatch MakeSwatch(float x, float y)
    {
        Image image = Image(_root, "Swatch", x, y, 30f, 30f, EmptySlot, _white);
        Outline outline = image.gameObject.AddComponent<Outline>();
        Swatch.Style(outline);
        Swatch swatch = image.gameObject.AddComponent<Swatch>();
        swatch.Image = image;
        swatch.Outline = outline;
        return swatch;
    }

    /// <summary>
    /// A rectangle placed from the parent's top left corner, with y growing downwards.
    /// </summary>
    private static RectTransform Box(Transform parent, string name, float x, float y, float width, float height)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RawImage Raw(Transform parent, string name, float x, float y, float width, float height, Texture texture)
    {
        RawImage image = Box(parent, name, x, y, width, height).gameObject.AddComponent<RawImage>();
        image.texture = texture;
        return image;
    }

    private static Image Image(Transform parent, string name, float x, float y, float width, float height, Color color, Sprite sprite)
    {
        Image image = Box(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private static Text Label(Transform parent, string token, float x, float y, float width, float height, Color color, int size)
    {
        Text text = Box(parent, "Label", x, y, width, height).gameObject.AddComponent<Text>();
        GUIManager.Instance.ApplyTextStyle(text, GUIManager.Instance.AveriaSerifBold, color, size, createOutline: true);
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = Localization.instance.Localize(token);
        return text;
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

    private static Sprite MakeRing()
    {
        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, mipChain: false);
        Vector2 centre = new((size - 1) / 2f, (size - 1) / 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), centre);
                float white = Mathf.Clamp01(3f - Mathf.Abs(distance - 11f));
                float black = Mathf.Clamp01(5f - Mathf.Abs(distance - 11f));
                Color color = Color.Lerp(Color.black, Color.white, white);
                color.a = Mathf.Max(white, black * 0.8f);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
