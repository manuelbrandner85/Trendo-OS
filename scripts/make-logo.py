#!/usr/bin/env python3
"""Erzeugt trendo.ico (16–256 px) und PNGs aus Assets/logo/trendo-logo-a.svg. Benötigt: cairosvg, Pillow."""
import io, pathlib, cairosvg
from PIL import Image
d = pathlib.Path(__file__).resolve().parent.parent / "src/Trendo.App/Assets/logo"
svg = (d / "trendo-logo-a.svg").read_bytes()
def png(s): return Image.open(io.BytesIO(cairosvg.svg2png(bytestring=svg, output_width=s, output_height=s))).convert("RGBA")
sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
png(256).save(d / "trendo.ico", sizes=[(s, s) for s in sizes], append_images=[png(s) for s in sizes[:-1]])
for s in (32, 256, 512): png(s).save(d / f"trendo-{s}.png")
