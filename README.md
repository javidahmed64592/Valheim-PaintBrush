[![CI](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-paintbrush/ci.yml?branch=main&style=flat-square&label=CI&logo=github)](https://github.com/javidahmed64592/valheim-paintbrush/actions/workflows/ci.yml)
[![Package](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-paintbrush/package.yml?branch=main&style=flat-square&label=Package&logo=github)](https://github.com/javidahmed64592/valheim-paintbrush/actions/workflows/package.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# PaintBrush

Paint your buildings: a paint brush, taken out with one key, colours built pieces in any shade and strength, and clears them back to vanilla. Only players with the mod see the paint.

- **Paint brush**: Press `O` to take out the brush, and again to put it away. It doesn't need to be crafted or carried.
- **Paint and clear**: Paint any piece built by a player, or clear it back to vanilla. The piece shows the result while you aim at it, and a splash of paint lands where you click.
- **Colour picker**: Choose any colour and how strongly it covers the piece in the brush menu, with a palette of saved colours.
- **Wood and stone stay wood and stone**: The paint keeps the grain and texture of every piece, and dark pieces can still be painted light.
- **Pick colour**: Take the colour of any piece, painted or not.
- **Connected pieces**: Paint or clear a whole building in one click with `Alt+Left-Click`.
- **Undo and redo**: Undo painting with `Ctrl+Z` and redo it with `Ctrl+Y`.

![Painted Pieces](https://github.com/javidahmed64592/Valheim-PaintBrush/raw/main/images/painted_pieces.png)

## Paint Menu

`Right-Click` with the brush out opens the brush menu. Its tools are along the bottom, and the colour picker fills the top.

- **Paint** colours the piece you click with the colour and strength set in the picker.
- **Clear paint** removes the paint, so the piece looks vanilla again.
- **Pick colour** is an eyedropper: it takes the paint of the piece you click, or its vanilla colour if it isn't painted, then switches back to Paint. `Shift+Left-Click` always takes the vanilla colour.

Choose a colour with the colour square and hue bar, the R, G and B sliders, or by typing its numbers or hex code.
**Strength** blends between the piece's own colour and the paint: low strength tints it, 100% covers it.
The previews show the last piece you aimed at as it is and as it would be painted.
Very light colours are toned down a little so they don't glare in daylight.

| Key | Action |
|---|---|
| `Left-Click` a swatch | Use a palette or recent colour. |
| `Shift+Left-Click` a palette slot | Save the current colour there. |
| `Right-Click` a palette slot | Empty it. |

Your colour, palette and recent colours are saved with your character.

![Paint Menu](https://github.com/javidahmed64592/Valheim-PaintBrush/raw/main/images/paint_menu.png)

## Painting

| Key | Action |
|---|---|
| `Left-Click` | Use the selected tool on the piece you aim at. |
| `Alt+Left-Click` | Paint or clear the piece and every piece connected to it. Hold `Alt` to preview it first. |
| `Ctrl+Z` | Undo the last painting or clearing. |
| `Ctrl+Y` | Redo the last painting or clearing you undid. |

- Painting needs access to the area, like building: pieces inside someone else's ward can't be painted, and `Alt+Left-Click` leaves them out.
- Pieces count as connected when they touch, as in [BetterHammer](https://thunderstore.io/c/valheim/p/Aestelwen/BetterHammer/)'s connected selection. One click paints at most 1000 pieces by default.
- Undoing and redoing swing the brush and splash the pieces they change. A whole connected paint undoes in one step. Pieces that were removed or are too far away are skipped.
- The history holds 50 actions by default and lasts until you log out.

## Keyhints

While the brush is out, the key hints show what each key does with the selected tool, with `Alt+Left-Click`, undo and redo above them.

## Configuration

Settings are in the BepInEx config file `javidahmed64592.paintbrush.cfg`: the keys, the largest size of painted textures (smaller saves memory, larger is sharper up close), the most pieces `Alt+Left-Click` paints at once, and how many actions can be undone.

## Compatibility

- The paint is saved on the pieces, but only players with the mod see it. Players without the mod, and servers, don't need it; they see the pieces as vanilla.
- Pieces from other mods can be painted if they use ordinary textures.
- Works with Valheim Performance Optimizations: painted pieces are drawn normally instead of in its batches, so they show their paint.
- Works alongside [BetterHammer](https://thunderstore.io/c/valheim/p/Aestelwen/BetterHammer/) and [BetterHoe](https://thunderstore.io/c/valheim/p/Aestelwen/BetterHoe/): `B` takes out the hammer, `N` the hoe and `O` the brush.
- This mod has not been tested with a gamepad, only with keyboard and mouse.

## Uninstalling

The paint stays saved on the pieces, but nobody sees it without the mod, so it can be removed at any time. Reinstalling shows the paint again.

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/javidahmed64592/Valheim-PaintBrush/blob/main/LICENSE) file for details.
