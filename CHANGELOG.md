# Changelog

## 0.5.0

- **Built-in palettes** fill all three rows, with similar colours next to each other.
- **Key combos:** every keybind is now a key plus an optional `Modifier` (`None`, `Shift`, `Ctrl` or `Alt`), both picked from a list in the config, so combos like Shift + B can be bound. Settings from older versions carry over.
- With `debugmode` on, the mod's key combos no longer also trigger the game's debug keys, such as Ctrl+Z toggling flying.
- **Safer after game updates:** if an update breaks part of the mod (the key hints, the brush menu's title or the true-colour preview), only that part turns off and the log says which, in one line; painting still works. If painting itself breaks, the whole mod turns off cleanly instead of throwing errors.
- **Debug setting:** the new `DisabledFeatures` setting under `[Debug]` turns parts of the mod off by name, for testing.
- Updated Jotunn to 2.30.3.

## 0.4.0

- A padlock next to the palette's name locks one of your palettes against accidental changes. Changing a locked palette, including renaming or deleting it, asks you to unlock it first.

## 0.3.0

- Palettes: step through them with the arrows above the palette or pick one from the list under its name, and make, rename, duplicate or delete them from the `⋯` menu. They're shared by all your characters.
- Comes with four built-in palettes (Banners, Earth & wood, Stone & metal and Vivid). Duplicate one to make your own version.
- Share a palette as a code: Copy code puts it in the clipboard, Paste code adds one.
- Drag any colour onto the palette to save it, drag palette colours onto each other to swap them, and off the palette to remove them. Saving a colour the palette already has moves it instead.
- The palette slot matching your current colour has a gold outline.
- A Shades row shows darker and lighter shades of a colour, and stays the same while you switch between them.
- The key hints in the brush menu show what you can do with the swatch under the mouse, instead of the build menu's Favorite and Search.
- A palette saved before this version becomes a palette named after the character.
- The brush menu's window is taller, so the tools fit below the colour picker without a scroll bar.

## 0.2.2

- Right-clicking a palette slot empties it again, instead of closing the brush menu.
- The palette holds 24 colours (3 rows) and the recent colours 16 (2 rows).

## 0.2.1

- No longer logs a warning about a missing LiberationSans SDF font.

## 0.2.0

- Works with [BetterHammer](https://thunderstore.io/c/valheim/p/Aestelwen/BetterHammer/) 1.4.0: saved copies, moved pieces and their ghosts keep their paint, and so do pieces copied with Shift+Middle-Click or brought back by undo.
- Other mods can read and set a piece's paint and show it on ghosts.

## 0.1.0

First release.

- A paint brush, taken out and put away with `O`, whose bristle tip shows the colour you're painting with.
- Paint and Clear paint tools for pieces built by players, with a live preview on the piece you aim at and a splash of paint when you use them.
- A Pick colour tool that takes a piece's paint, or its vanilla colour with Shift, then goes back to painting.
- Alt+Click paints or clears every connected piece at once, with a preview while Alt is held.
- Undo and redo (Ctrl+Z, Ctrl+Y) for painting and clearing.
- A colour picker in the brush menu: colour square, hue bar, R/G/B and strength sliders, hex field, palette and recent colours.
- Only players with the mod see the paint.
