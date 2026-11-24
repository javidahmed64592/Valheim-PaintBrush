using PaintBrush.Painting;

namespace PaintBrush.Brush;

/// <summary>
/// Shows the piece under the brush as it would look: with the Paint tool in the current colour, with the Clear
/// tool as vanilla. Moving away puts its saved paint back. Also remembers the last piece aimed at, whose texture
/// the menu's preview uses.
/// </summary>
internal static class PaintPreview
{
    private static Piece _target;
    private static BrushTool _tool;

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

        if (hovered != _target)
        {
            if (_target != null)
            {
                PaintedPieces.SetPreview(_target, null);
            }

            _target = hovered;
        }

        _tool = tool;
        if (_target == null)
        {
            return;
        }

        LastAimed = _target;

        // The eyedropper shows the piece as it is.
        PaintedPieces.SetPreview(_target, tool switch
        {
            BrushTool.Paint => BrushState.Paint,
            BrushTool.Clear => PaintColor.None,
            _ => null,
        });
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
