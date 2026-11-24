using System.Collections.Generic;
using System.Globalization;

using BepInEx.Configuration;

using PaintBrush.Brush;
using PaintBrush.Core;
using PaintBrush.Painting;

using UnityEngine;

namespace PaintBrush.History;

/// <summary>
/// Undo and redo for painting and clearing. Each action keeps every piece it changed with its paint before and
/// after, so an Alt+Click on a whole building undoes in one step. Pieces that were removed or are too far away to
/// be loaded are left out; if any piece is behind someone else's ward, nothing changes. The history lasts until
/// logging out.
/// </summary>
/// <remarks>
/// Undoing and redoing look and sound like painting: the brush swings, and the pieces splash with the paint they
/// get back (or, when it's taken off, the paint being removed), as BetterHammer and BetterHoe replay their tools.
/// Only the nearest <see cref="MaxSplashes"/> pieces splash, so undoing a whole building doesn't flood the screen.
/// </remarks>
internal static class PaintHistory
{
    internal readonly struct Change(ZDOID piece, PaintColor before, PaintColor after)
    {
        public readonly ZDOID Piece = piece;
        public readonly PaintColor Before = before;
        public readonly PaintColor After = after;
    }

    private const int MaxSplashes = 12;

    private static ConfigEntry<KeyboardShortcut> _undoKey;
    private static ConfigEntry<KeyboardShortcut> _redoKey;
    private static ConfigEntry<int> _size;

    private static readonly LinkedList<List<Change>> Undos = [];
    private static readonly Stack<List<Change>> Redos = [];

    internal static KeyboardShortcut UndoKey => _undoKey.Value;
    internal static KeyboardShortcut RedoKey => _redoKey.Value;
    internal static bool CanUndo => Undos.Count > 0;
    internal static bool CanRedo => Redos.Count > 0;

    public static void BindConfig(ConfigFile config)
    {
        _undoKey = config.Bind("Keybinds", "Undo", new KeyboardShortcut(KeyCode.Z, KeyCode.LeftControl),
            "Undoes the last painting or clearing done with the brush.");
        _redoKey = config.Bind("Keybinds", "Redo", new KeyboardShortcut(KeyCode.Y, KeyCode.LeftControl),
            "Redoes the last painting or clearing you undid.");
        _size = config.Bind("Undo", "History Size", 50,
            new ConfigDescription("How many paint actions can be undone.", new AcceptableValueRange<int>(1, 500)));
    }

    public static void Reset()
    {
        Undos.Clear();
        Redos.Clear();
    }

    /// <summary>
    /// Records an action that was just done. A new action clears the redo list.
    /// </summary>
    public static void Record(List<Change> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        _ = Undos.AddLast(changes);
        Redos.Clear();
        while (Undos.Count > _size.Value)
        {
            Undos.RemoveFirst();
        }
    }

    /// <summary>
    /// Checks the undo and redo keys while the brush is out and nothing else has the keyboard.
    /// </summary>
    public static void UpdateKeys(Player player)
    {
        if (Keys.Blocked() || Hud.IsPieceSelectionVisible())
        {
            return;
        }

        if (Keys.IsDown(UndoKey))
        {
            Step(player, undo: true);
        }
        else if (Keys.IsDown(RedoKey))
        {
            Step(player, undo: false);
        }
    }

    /// <summary>
    /// Swings the brush and splashes the nearest changed pieces, on the side facing the player.
    /// </summary>
    private static void Replay(Player player, List<(Piece Piece, PaintColor Paint, PaintColor Old)> targets)
    {
        ItemDrop.ItemData brush = player.GetRightItem();
        if (brush != null)
        {
            player.m_zanim.SetTrigger(brush.m_shared.m_attack.m_attackAnimation);
        }

        Vector3 eye = player.m_eye != null ? player.m_eye.position : player.transform.position;
        targets.Sort((a, b) => (a.Piece.transform.position - eye).sqrMagnitude.CompareTo((b.Piece.transform.position - eye).sqrMagnitude));
        int splashes = Mathf.Min(MaxSplashes, targets.Count);
        for (int i = 0; i < splashes; i++)
        {
            (Piece piece, PaintColor paint, PaintColor old) = targets[i];
            Collider collider = piece.GetComponentInChildren<Collider>();
            Vector3 point = collider != null ? collider.ClosestPointOnBounds(eye) : piece.transform.position;
            Vector3 normal = eye - point;
            PaintSplash.Play(point, normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up, paint.IsNone ? old : paint, sound: i == 0);
        }
    }

    private static void Step(Player player, bool undo)
    {
        if (undo ? Undos.Count == 0 : Redos.Count == 0)
        {
            player.Message(MessageHud.MessageType.TopLeft, undo ? "$paintbrush_msg_undo_nothing" : "$paintbrush_msg_redo_nothing");
            return;
        }

        List<Change> changes = undo ? Undos.Last.Value : Redos.Peek();

        // Every loaded piece must be accessible before anything changes.
        List<(Piece Piece, PaintColor Paint, PaintColor Old)> targets = [];
        foreach (Change change in changes)
        {
            GameObject instance = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(change.Piece) : null;
            Piece piece = instance != null ? instance.GetComponent<Piece>() : null;
            if (piece == null || piece.m_nview == null || !piece.m_nview.IsValid())
            {
                continue;
            }

            if (!PrivateArea.CheckAccess(piece.transform.position))
            {
                return;
            }

            targets.Add(undo ? (piece, change.Before, change.After) : (piece, change.After, change.Before));
        }

        if (targets.Count == 0)
        {
            player.Message(MessageHud.MessageType.Center, "$paintbrush_msg_undo_too_far");
            return;
        }

        foreach ((Piece piece, PaintColor paint, PaintColor _) in targets)
        {
            PaintStore.Write(piece.m_nview, paint);
            PaintedPieces.OnPaintWritten(piece, paint);
        }

        Replay(player, targets);

        if (undo)
        {
            Undos.RemoveLast();
            Redos.Push(changes);
        }
        else
        {
            _ = Redos.Pop();
            _ = Undos.AddLast(changes);
        }

        player.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize(
            undo ? "$paintbrush_msg_undone" : "$paintbrush_msg_redone",
            targets.Count.ToString(CultureInfo.InvariantCulture)));
    }
}
