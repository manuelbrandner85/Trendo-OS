#!/usr/bin/env python3
"""Erzeugt trendo.ico (16–256 px) und PNGs (256/512) aus trendo-logo-a.svg. Reproduzierbar: pip install cairosvg pillow."""
import io, pathlib, cairosvg
from PIL import Image
d = pathlib.Path(__file__).resolve().parent.parent / "src/Trendo.App/Assets/logo"
svg = (d / "trendo-logo-a.svg").read_bytes()
def png(px): return Image.open(io.BytesIO(cairosvg.svg2png(bytestring=svg, output_width=px, output_height=px))).convert("RGBA")
for px in (256, 512): png(px).save(d / f"trendo-{px}.png", optimize=True)
sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
imgs = [png(s) for s in sizes]
imgs[-1].save(d / "trendo.ico", format="ICO", sizes=[(s, s) for s in sizes], append_images=imgs[:-1])
print("ok", d)
