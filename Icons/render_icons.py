"""Renders the tool icons from their SVGs: uv run --with cairosvg python Icons/render_icons.py"""

from pathlib import Path

import cairosvg

SIZE = 128

here = Path(__file__).parent
for svg in sorted(here.glob("*.svg")):
    png = svg.with_suffix(".png")
    cairosvg.svg2png(url=str(svg), write_to=str(png), output_width=SIZE, output_height=SIZE)
    print(f"rendered {png.name}")
