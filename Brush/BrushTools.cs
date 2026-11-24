using System.Collections.Generic;
using System.Globalization;

using PaintBrush.Core;
using PaintBrush.History;
using PaintBrush.Painting;

using UnityEngine;

namespace PaintBrush.Brush;

internal enum BrushTool
{
    None = 0,
    Paint = 1,
    Clear = 2,
    Pick = 3,
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
                BrushPrefabs.PickToolName => BrushTool.Pick,
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

        if (tool == BrushTool.Pick)
        {
            Pick(player, target);
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
        if (!Keys.Alt && previous == paint)
        {
            return true;
        }

        List<Piece> pieces = Keys.Alt ? Connected(player, target, paint) : [target];
        if (pieces.Count == 0)
        {
            return true;
        }

        List<PaintHistory.Change> changes = [];
        foreach (Piece piece in pieces)
        {
            PaintColor before = PaintedPieces.StoredPaint(piece);
            changes.Add(new PaintHistory.Change(piece.m_nview.GetZDO().m_uid, before, paint));
            PaintStore.Write(piece.m_nview, paint);
            PaintedPieces.OnPaintWritten(piece, paint);
        }

        PaintHistory.Record(changes);
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
    /// For Alt+Click: the aimed piece and every piece connected to it that the brush can paint, isn't behind
    /// someone else's ward and would change. Says so when the search stopped at Max Connected Pieces, or when
    /// warded pieces were left out.
    /// </summary>
    private static List<Piece> Connected(Player player, Piece target, PaintColor paint)
    {
        List<Piece> pieces = [];
        int warded = 0;
        List<Piece> found = ConnectedPieces.Find(target, out bool limited);
        for (int i = 0; i < found.Count; i++)
        {
            Piece piece = found[i];
            if (!PaintedPieces.CanPaint(piece) || PaintedPieces.StoredPaint(piece) == paint)
            {
                continue;
            }

            if (!PrivateArea.CheckAccess(piece.transform.position, 0f, flash: false))
            {
                warded++;
                continue;
            }

            pieces.Add(piece);
        }

        string count = pieces.Count.ToString(CultureInfo.InvariantCulture);
        string message = paint.IsNone ? "$paintbrush_msg_cleared_connected" : "$paintbrush_msg_painted_connected";
        player.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize(message, count));
        if (limited)
        {
            player.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize(
                "$paintbrush_msg_connected_limit", ConnectedPieces.MaxPieces.ToString(CultureInfo.InvariantCulture)));
        }

        if (warded > 0)
        {
            player.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize(
                "$paintbrush_msg_connected_warded", warded.ToString(CultureInfo.InvariantCulture)));
        }

        return pieces;
    }

    /// <summary>
    /// Takes the colour of a piece: its paint (colour and strength) if it's painted, otherwise, or with Shift held,
    /// its vanilla colour (the average of its texture, keeping the strength). Then goes back to the Paint tool.
    /// Only reads, so it works on any piece, wards or not.
    /// </summary>
    private static void Pick(Player player, Piece target)
    {
        PaintColor stored = PaintedPieces.StoredPaint(target);
        bool vanilla = stored.IsNone || Keys.Shift;
        Color32 color;
        if (vanilla)
        {
            if (!PaintedPieces.SampleTexture(target, out Texture texture, out bool cutout))
            {
                player.Message(MessageHud.MessageType.Center, "$paintbrush_msg_cannot_pick");
                return;
            }

            color = PaintTextures.AverageColor(texture, cutout);
            BrushState.SetColor(color);
            player.Message(MessageHud.MessageType.TopLeft,
                Localization.instance.Localize("$paintbrush_msg_picked_vanilla", BrushState.ToHex(color)));
        }
        else
        {
            color = stored.Color;
            BrushState.SetColor(color);
            BrushState.SetStrength(Mathf.RoundToInt(stored.Strength / 2.55f));
            player.Message(MessageHud.MessageType.TopLeft,
                Localization.instance.Localize("$paintbrush_msg_picked", BrushState.ToHex(color), BrushState.Strength.ToString(CultureInfo.InvariantCulture)));
        }

        Piece paint = BrushPrefabs.PaintTool;
        if (paint != null)
        {
            _ = player.SetSelectedPiece(paint);
        }
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
