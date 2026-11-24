using System.Collections.Generic;

using PaintBrush.Core;
using PaintBrush.Painting;

namespace PaintBrush.Brush;

/// <summary>
/// Shows the piece under the brush as it would look: with the Paint tool in the current colour, with the Clear
/// tool as vanilla; with Alt held, every connected piece too. Moving away puts the saved paint back. Also
/// remembers the last piece aimed at, whose texture the menu's preview uses.
/// </summary>
internal static class PaintPreview
{
    private static readonly List<Piece> Group = [];
    private static Piece _target;
    private static BrushTool _tool;
    private static bool _connected;

    /// <summary>
    /// The last paintable piece the brush was aimed at, or null.
    /// </summary>
    public static Piece LastAimed { get; private set; }

    public static void Update(Player player)
    {
        BrushTool tool = BrushTools.Selected(player);
        Piece hovered = tool != BrushTool.None ? player.GetHoveringPiece() : null;
        if (hovered != null && !PaintedPieces.CanPaint(hovered))
        {
            hovered = null;
        }

        // Holding Alt with Paint or Clear previews every connected piece the click would change.
        bool connected = hovered != null && tool is BrushTool.Paint or BrushTool.Clear && Keys.Alt;
        if (hovered != _target || connected != _connected || tool != _tool)
        {
            ClearGroup();
            _target = hovered;
            _connected = connected;
            if (connected)
            {
                foreach (Piece piece in ConnectedPieces.Find(hovered, out _))
                {
                    if (PaintedPieces.CanPaint(piece) && PrivateArea.CheckAccess(piece.transform.position, 0f, flash: false))
                    {
                        Group.Add(piece);
                    }
                }
            }
            else if (hovered != null)
            {
                Group.Add(hovered);
            }
        }

        _tool = tool;
        if (_target == null)
        {
            return;
        }

        LastAimed = _target;

        // The eyedropper shows the piece as it is.
        PaintColor? preview = tool switch
        {
            BrushTool.Paint => BrushState.Paint,
            BrushTool.Clear => PaintColor.None,
            _ => null,
        };
        foreach (Piece piece in Group)
        {
            PaintedPieces.SetPreview(piece, preview);
        }
    }

    private static void ClearGroup()
    {
        foreach (Piece piece in Group)
        {
            PaintedPieces.SetPreview(piece, null);
        }

        Group.Clear();
    }

    /// <summary>
    /// The game tints the piece under a build tool to show its support. That would hide the paint preview, so
    /// it's skipped for the piece being painted. Clearing keeps it, so the piece still shows which one is aimed at.
    /// </summary>
    public static bool HidesHighlight(WearNTear piece)
    {
        return _tool == BrushTool.Paint && _target != null && piece != null && piece.gameObject == _target.gameObject;
    }
}
