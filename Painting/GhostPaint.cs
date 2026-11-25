using System.Collections.Generic;

using UnityEngine;

namespace PaintBrush.Painting;

/// <summary>
/// Paint on an object that isn't a saved piece, such as a placement ghost or a model rendered for an icon. It
/// has no saved data for <see cref="PaintedPieces"/> to follow, so the paint is held here and taken off (and its
/// materials released) when the object is destroyed.
/// </summary>
/// <remarks>
/// Unity only calls OnDestroy on objects that were active at some point, and ghosts are often made inactive and
/// destroyed without ever being shown, so <see cref="Sweep"/> also releases the paint of destroyed objects.
/// </remarks>
internal sealed class GhostPaint : MonoBehaviour
{
    private static readonly List<GhostPaint> Live = [];

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

    /// <summary>
    /// Releases the paint of objects destroyed without OnDestroy running. Called every frame; cheap.
    /// </summary>
    public static void Sweep()
    {
        for (int i = Live.Count - 1; i >= 0; i--)
        {
            GhostPaint ghost = Live[i];
            if (ghost == null)
            {
                // Destroyed: the managed object is still there with its list.
                ghost.Clear();
                Live.RemoveAt(i);
            }
        }
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
            if (!Live.Contains(this))
            {
                Live.Add(this);
            }
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
        _ = Live.Remove(this);
    }
}
