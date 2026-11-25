using PaintBrush.Painting;

using UnityEngine;

namespace PaintBrush.Api;

/// <summary>
/// <para>
/// For other mods (BetterHammer's copies and moves): read and write a piece's paint, and show paint on ghosts.
/// It only uses plain types, so a mod can call it through reflection without referencing PaintBrush, and do
/// nothing when PaintBrush isn't installed. BepInEx loads plugins from their files, so find the type through
/// the plugin rather than by assembly name:
/// <c>Chainloader.PluginInfos["javidahmed64592.paintbrush"].Instance.GetType().Assembly.GetType("PaintBrush.Api.PaintApi")</c>.
/// </para>
/// <para>
/// A paint is one int: red, green and blue in the top three bytes and the strength (1-255) in the lowest; 0 means
/// unpainted. Treat it as opaque and hand it back as it was given.
/// </para>
/// </summary>
public static class PaintApi
{
    /// <summary>
    /// The paint saved on a built piece (or on the piece a child object belongs to), or 0.
    /// </summary>
    public static int GetPaint(GameObject piece)
    {
        Piece component = piece != null ? piece.GetComponentInParent<Piece>() : null;
        return PaintedPieces.StoredPaint(component).Pack();
    }

    /// <summary>
    /// Saves a paint on a built piece and shows it, as the brush would; 0 clears it. Does nothing for an object
    /// that isn't a saved piece. Doesn't check wards: it's meant for pieces the caller just built.
    /// </summary>
    public static void SetPaint(GameObject piece, int paint)
    {
        Piece component = piece != null ? piece.GetComponentInParent<Piece>() : null;
        if (component == null || component.m_nview == null || !component.m_nview.IsValid())
        {
            return;
        }

        PaintColor color = PaintColor.Unpack(paint);
        PaintStore.Write(component.m_nview, color);
        PaintedPieces.OnPaintWritten(component, color);
    }

    /// <summary>
    /// Shows a paint on an object that isn't saved, such as a placement ghost or a model rendered for an icon,
    /// until it's destroyed or shown with another paint; 0 takes it off.
    /// </summary>
    public static void ShowOnGhost(GameObject ghost, int paint)
    {
        GhostPaint.Show(ghost, PaintColor.Unpack(paint));
    }
}
