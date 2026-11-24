using PaintBrush.Painting;

using UnityEngine;

namespace PaintBrush.Brush;

internal enum BrushTool
{
    None = 0,
    Paint = 1,
    Clear = 2,
}

/// <summary>
/// What the brush does when it's used on a piece.
/// </summary>
internal static class BrushTools
{
    public static bool IsEquipped(Player player)
    {
        return player != null && BrushPrefabs.IsBrushTable(player.GetRightItem()?.m_shared?.m_buildPieces);
    }

    /// <summary>
    /// The tool selected in the brush menu, or None when the brush isn't out.
    /// </summary>
    public static BrushTool Selected(Player player)
    {
        return IsEquipped(player) && player.InPlaceMode() ? ToolOf(player.m_buildPieces?.GetSelectedPiece()) : BrushTool.None;
    }

    public static BrushTool ToolOf(Piece piece)
    {
        return piece == null
            ? BrushTool.None
            : piece.gameObject.name switch
            {
                BrushPrefabs.PaintToolName => BrushTool.Paint,
                BrushPrefabs.ClearToolName => BrushTool.Clear,
                _ => BrushTool.None,
            };
    }

    /// <summary>
    /// Called instead of vanilla's repair when a brush tool is used. Returns false for anything that isn't a
    /// brush tool, so vanilla repairs as usual.
    /// </summary>
    public static bool TryUse(Player player, ItemDrop.ItemData toolItem, Piece toolPiece)
    {
        BrushTool tool = ToolOf(toolPiece);
        if (tool == BrushTool.None || player != Player.m_localPlayer)
        {
            return false;
        }

        Piece target = player.GetHoveringPiece();
        if (target == null)
        {
            return true;
        }

        if (!PaintedPieces.CanPaint(target))
        {
            player.Message(MessageHud.MessageType.Center, "$paintbrush_msg_cannot_paint");
            return true;
        }

        if (!PrivateArea.CheckAccess(target.transform.position))
        {
            return true;
        }

        PaintColor paint = tool == BrushTool.Paint ? BrushState.Paint : PaintColor.None;
        PaintColor previous = PaintedPieces.StoredPaint(target);
        if (previous == paint)
        {
            return true;
        }

        PaintStore.Write(target.m_nview, paint);
        PaintedPieces.OnPaintWritten(target, paint);
        if (!paint.IsNone)
        {
            BrushState.AddRecent();
        }

        player.FaceLookDirection();
        player.m_zanim.SetTrigger(toolItem.m_shared.m_attack.m_attackAnimation);
        Aim(player, target, out Vector3 point, out Vector3 normal);
        PaintSplash.Play(point, normal, paint.IsNone ? previous : paint);
        return true;
    }

    /// <summary>
    /// Where the player is aiming on the piece, found the way the game finds the piece under a build tool; the
    /// piece's centre, facing the camera, if the ray doesn't hit it.
    /// </summary>
    private static void Aim(Player player, Piece target, out Vector3 point, out Vector3 normal)
    {
        Transform camera = GameCamera.instance != null ? GameCamera.instance.transform : null;
        if (camera != null
            && Physics.Raycast(camera.position, camera.forward, out RaycastHit hit, 50f, player.m_removeRayMask)
            && hit.collider.GetComponentInParent<Piece>() == target)
        {
            point = hit.point;
            normal = hit.normal;
            return;
        }

        point = target.transform.position;
        normal = camera != null ? -camera.forward : Vector3.up;
    }
}
