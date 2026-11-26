using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PaintBrush.UI;

/// <summary>
/// Reports where the left mouse button is pressed or dragged inside the object, from (0, 0) at the bottom left to
/// (1, 1) at the top right, clamped to the edges while dragging outside.
/// </summary>
internal sealed class DragArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IBeginDragHandler
{
    public Action<Vector2> Changed;

    public void OnPointerDown(PointerEventData eventData)
    {
        Report(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Report(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Report(eventData);
    }

    private void Report(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        RectTransform rect = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            return;
        }

        Rect bounds = rect.rect;
        Changed?.Invoke(new Vector2(
            Mathf.Clamp01((local.x - bounds.xMin) / bounds.width),
            Mathf.Clamp01((local.y - bounds.yMin) / bounds.height)));
    }
}

/// <summary>
/// A colour swatch: reports clicks, lights its outline while the mouse is over it (gold while selected), and can be
/// dragged onto another swatch when it has a colour to give.
/// </summary>
internal sealed class Swatch : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    private static readonly Color Normal = new(0f, 0f, 0f, 0.9f);
    private static readonly Color Hover = new(1f, 0.93f, 0.8f, 1f);
    private static readonly Color Gold = new(1f, 0.72f, 0.36f, 1f);

    private static Swatch _hovered;

    public Action<PointerEventData.InputButton> Clicked;

    /// <summary>
    /// The colour a drag from this swatch carries, or null when there's nothing to drag.
    /// </summary>
    public Func<Color32?> DragColor;

    /// <summary>
    /// Another swatch was dragged and dropped here.
    /// </summary>
    public Action<Swatch> Dropped;

    /// <summary>
    /// A drag from this swatch ended. True if it was dropped on another swatch that took it.
    /// </summary>
    public Action<PointerEventData, bool> DragEnded;

    /// <summary>
    /// A drag from this swatch moved, e.g. to show whether letting go here would do something.
    /// </summary>
    public Action<PointerEventData> DragMoved;

    /// <summary>
    /// The palette slot this is, or -1 for any other swatch.
    /// </summary>
    public int PaletteIndex = -1;

    /// <summary>
    /// Whether right-clicking this swatch does something, so it mustn't also close the build menu.
    /// </summary>
    public bool UsesRightClick;

    public Image Image;
    public Outline Outline;
    private bool _dragged;
    private bool _dropTaken;

    /// <summary>
    /// The swatch being dragged, if any.
    /// </summary>
    public static Swatch Dragging { get; private set; }

    /// <summary>
    /// The swatch under the mouse, if any.
    /// </summary>
    public static Swatch Hovered => _hovered != null && _hovered.isActiveAndEnabled ? _hovered : null;

    /// <summary>
    /// Whether the mouse is over a swatch that uses right-click.
    /// </summary>
    public static bool RightClickHovered => _hovered != null && _hovered.UsesRightClick && _hovered.isActiveAndEnabled;

    public bool Selected
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                UpdateOutline();
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _dragged = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_dragged)
        {
            Clicked?.Invoke(eventData.button);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = this;
        UpdateOutline();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_hovered == this)
        {
            _hovered = null;
        }

        UpdateOutline();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Color32? color = eventData.button == PointerEventData.InputButton.Left ? DragColor?.Invoke() : null;
        if (!color.HasValue)
        {
            // Not draggable: hand the drag to whatever is behind, so it doesn't count as started.
            eventData.pointerDrag = null;
            return;
        }

        _dragged = true;
        _dropTaken = false;
        Dragging = this;
        DragGhost.Show(color.Value, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Dragging == this)
        {
            DragGhost.Move(eventData);
            DragMoved?.Invoke(eventData);
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        Swatch source = Dragging;
        if (source != null && source != this && Dropped != null)
        {
            source._dropTaken = true;
            Dropped(source);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Dragging != this)
        {
            return;
        }

        Dragging = null;
        DragGhost.Hide();
        DragEnded?.Invoke(eventData, _dropTaken);
    }

    private void OnDisable()
    {
        if (Dragging == this)
        {
            Dragging = null;
            DragGhost.Hide();
        }

        if (_hovered == this)
        {
            _hovered = null;
        }

        UpdateOutline();
    }

    private void UpdateOutline()
    {
        if (Outline == null)
        {
            return;
        }

        Color color = Selected ? Gold : Normal;
        Outline.effectColor = _hovered == this ? Hover : color;
        Outline.effectDistance = Selected ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
    }

    public static void Style(Outline outline)
    {
        outline.effectColor = Normal;
        outline.effectDistance = new Vector2(2f, -2f);
    }
}

/// <summary>
/// The colour square that follows the mouse while a swatch is dragged. Faded when letting go would remove the colour.
/// </summary>
internal static class DragGhost
{
    private static Image _image;
    private static Outline _outline;
    private static RectTransform _layer;

    /// <summary>
    /// The ghost is drawn above everything in this rectangle.
    /// </summary>
    public static void Init(RectTransform layer)
    {
        _layer = layer;
        _image = UiParts.Image(layer, "PaintBrush_DragGhost", 0f, 0f, 30f, 30f, Color.white, UiParts.White);
        _image.raycastTarget = false;
        _image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _outline = _image.gameObject.AddComponent<Outline>();
        Swatch.Style(_outline);
        _image.gameObject.SetActive(false);
    }

    public static void Show(Color32 color, PointerEventData eventData)
    {
        if (_image == null)
        {
            return;
        }

        _image.color = color;
        SetRemoving(false);
        _image.gameObject.SetActive(true);
        _image.transform.SetAsLastSibling();
        Move(eventData);
    }

    public static void Move(PointerEventData eventData)
    {
        if (_image != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            // The layer's pivot is its top left corner, which is also where the ghost is anchored.
            _image.rectTransform.anchoredPosition = local + new Vector2(12f, -12f);
        }
    }

    public static void SetRemoving(bool removing)
    {
        if (_image != null)
        {
            Color color = _image.color;
            color.a = removing ? 0.35f : 1f;
            _image.color = color;
        }
    }

    public static void Hide()
    {
        if (_image != null)
        {
            _image.gameObject.SetActive(false);
        }
    }
}

/// <summary>
/// A plain clickable graphic (an icon or a menu row) that brightens while the mouse is over it.
/// </summary>
internal sealed class HoverButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Action Clicked;
    public Graphic Target;
    public Color Normal = Color.white;
    public Color Hover = Color.white;

    private bool _hovered;

    public bool Interactable
    {
        get;
        set
        {
            field = value;
            Refresh();
        }
    } = true;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Interactable && eventData.button == PointerEventData.InputButton.Left)
        {
            Clicked?.Invoke();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        Refresh();
    }

    private void OnDisable()
    {
        _hovered = false;
        Refresh();
    }

    private void Refresh()
    {
        if (Target != null)
        {
            Color color = _hovered && Interactable ? Hover : Normal;
            if (!Interactable)
            {
                color.a *= 0.35f;
            }

            Target.color = color;
        }
    }
}
