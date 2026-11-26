using System;

using PaintBrush.Palettes;

namespace PaintBrush.UI;

/// <summary>
/// Every change the player makes to a palette from the menu goes through here. A built-in palette says why nothing
/// happened; a locked one asks to be unlocked first, then makes the change.
/// </summary>
internal static class PaletteEdit
{
    /// <summary>
    /// Changes the palette in use, asking to unlock it first if it's locked.
    /// </summary>
    public static void Run(Func<bool> edit)
    {
        // Nothing covers the menu behind the unlock question, so clicks there are ignored until it's answered.
        if (UnifiedPopup.IsVisible())
        {
            return;
        }

        Palette palette = PaletteLibrary.Active;
        if (palette.IsBuiltIn)
        {
            Message("$paintbrush_msg_builtin_locked");
        }
        else if (palette.Locked)
        {
            AskUnlock(palette, edit);
        }
        else
        {
            _ = edit();
        }
    }

    /// <summary>
    /// The padlock: locks or unlocks the palette in use.
    /// </summary>
    public static void ToggleLock()
    {
        if (UnifiedPopup.IsVisible())
        {
            return;
        }

        Palette palette = PaletteLibrary.Active;
        if (PaletteLibrary.SetLocked(palette, !palette.Locked))
        {
            Message(Localization.instance.Localize(palette.Locked ? "$paintbrush_msg_palette_locked" : "$paintbrush_msg_palette_unlocked", palette.Name));
        }
        else
        {
            Message("$paintbrush_msg_builtin_locked");
        }
    }

    /// <summary>
    /// Asks whether to unlock the palette. Yes unlocks it and then makes the change, if any, as long as the palette is
    /// still the one in use.
    /// </summary>
    public static void AskUnlock(Palette palette, Func<bool> then)
    {
        if (!UnifiedPopup.IsAvailable() || UnifiedPopup.IsVisible())
        {
            Message(Localization.instance.Localize("$paintbrush_msg_unlock_first", palette.Name));
            return;
        }

        UnifiedPopup.Push(new YesNoPopup(
            Localization.instance.Localize("$paintbrush_unlock_header"),
            Localization.instance.Localize("$paintbrush_unlock_text", palette.Name),
            () =>
            {
                UnifiedPopup.Pop();
                _ = PaletteLibrary.SetLocked(palette, false);
                if (then != null && PaletteLibrary.Active == palette)
                {
                    _ = then();
                }
            },
            UnifiedPopup.Pop,
            localizeText: false));
    }

    private static void Message(string text)
    {
        if (Player.m_localPlayer != null)
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, text);
        }
    }
}
