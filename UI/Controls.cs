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
/// A colour swatch: reports left and right clicks and lights its outline while the mouse is over it.
/// </summary>
internal sealed class Swatch : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly Color Normal = new(0f, 0f, 0f, 0.9f);
    private static readonly Color Hover = new(1f, 0.72f, 0.36f, 1f);

    public Action<PointerEventData.InputButton> Clicked;
    public Image Image;
    public Outline Outline;

    public void OnPointerClick(PointerEventData eventData)
    {
        Clicked?.Invoke(eventData.button);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Outline != null)
        {
            Outline.effectColor = Hover;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Outline != null)
        {
            Outline.effectColor = Normal;
        }
    }

    public static void Style(Outline outline)
    {
        outline.effectColor = Normal;
        outline.effectDistance = new Vector2(2f, -2f);
    }
}
