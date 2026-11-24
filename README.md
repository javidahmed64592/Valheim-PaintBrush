[![CI](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-paintbrush/ci.yml?branch=main&style=flat-square&label=CI&logo=github)](https://github.com/javidahmed64592/valheim-paintbrush/actions/workflows/ci.yml)
[![Package](https://img.shields.io/github/actions/workflow/status/javidahmed64592/valheim-paintbrush/package.yml?branch=main&style=flat-square&label=Package&logo=github)](https://github.com/javidahmed64592/valheim-paintbrush/actions/workflows/package.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# PaintBrush

Paint your buildings: a paint brush, taken out with one key, colours built pieces in any shade and strength, and clears them back to vanilla. Only players with the mod see the paint.

- **Paint brush:** press `O` to take out the brush, and again to put it away. It doesn't need to be crafted or carried.
- **Paint and Clear paint:** aim at a piece you or another player built and click. The piece shows the result while you aim at it, so you see the colour before you commit.
- **Colour picker:** right-click opens the brush menu, with a colour square and hue bar, R, G and B sliders, a hex field, a 16-colour palette and your recent colours.
- **Pick colour:** an eyedropper that takes the colour of any piece: its paint if it's painted, or its vanilla colour.
- **Connected pieces:** Alt+Click paints or clears every piece connected to the one you aim at, and holding Alt previews it.
- **Undo and redo:** Ctrl+Z and Ctrl+Y, with a whole connected paint undone in one step.
- **Strength:** blends between the piece's own colour and the paint. The wood grain or stone texture stays visible at any strength, and dark pieces can still be painted light.

## Controls

| Action | Keys |
|---|---|
| Take out or put away the brush | `O` |
| Paint, or clear paint | Left-Click on a piece |
| Paint, or clear paint, on every connected piece | Alt+Left-Click on a piece |
| Undo, redo | Ctrl+Z, Ctrl+Y |
| Pick a piece's paint (or vanilla colour if unpainted) | Left-Click with Pick colour |
| Pick a piece's vanilla colour | Shift+Left-Click with Pick colour |
| Open the brush menu | Right-Click |
| Use a palette or recent colour | Click the swatch |
| Save the current colour to the palette | Shift+Click a palette slot |
| Empty a palette slot | Right-Click it |

Painting needs access to the area, like building: a piece inside someone else's ward can't be painted, and Alt+Click leaves such pieces out.

## Configuration

Settings are in `BepInEx/config/javidahmed64592.paintbrush.cfg`: the keys, the largest size of painted textures (smaller saves memory, larger is sharper up close), the most pieces Alt+Click paints at once, and how many actions can be undone.

## Compatibility

- The paint is saved on the pieces, but only players with the mod see it. Players without the mod, and servers, don't need it; they see the pieces as vanilla.
- Pieces from other mods can be painted if they use ordinary textures.
- This mod has not been tested with a gamepad, only with keyboard and mouse.

## Uninstalling

The paint stays saved on the pieces but nobody sees it without the mod, so it can be removed at any time. Reinstalling shows the paint again.

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/javidahmed64592/Valheim-PaintBrush/blob/main/LICENSE) file for details.
