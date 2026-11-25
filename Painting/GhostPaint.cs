using System.Collections.Generic;

using UnityEngine;

namespace PaintBrush.Painting;

/// <summary>
/// Paint on an object that isn't a saved piece, such as a placement ghost or a model rendered for an icon. It
/// has no saved data for <see cref="PaintedPieces"/> to follow, so the paint is held here and taken off (and its
/// materials released) when the object is destroyed.
/// </summary>
internal sealed class GhostPaint : MonoBehaviour
{
    private List<(Renderer Renderer, int Index, Material Original, Material Painted)> _painted;
    private PaintColor _paint;

    /// <summary>
    /// Shows the paint on the object, replacing any it showed before; None takes it off.
    /// </summary>
    public static void Show(GameObject target, PaintColor paint)
    {
        if (target == null)
        {
            return;
        }

        if (!target.TryGetComponent(out GhostPaint ghost))
        {
            if (paint.IsNone)
            {
                return;
            }

            ghost = target.AddComponent<GhostPaint>();
        }

        ghost.Set(paint);
    }

    private void Set(PaintColor paint)
    {
        if (paint == _paint && _painted != null)
        {
            return;
        }

        Clear();
        if (!paint.IsNone)
        {
            _paint = paint;
            _painted = PaintedPieces.PaintObject(gameObject, paint);
        }
    }

    private void Clear()
    {
        if (_painted != null)
        {
            PaintedPieces.UnpaintObject(_painted, _paint);
            _painted = null;
        }

        _paint = PaintColor.None;
    }

    private void OnDestroy()
    {
        Clear();
    }
}
