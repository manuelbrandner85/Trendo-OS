#!/usr/bin/env python3
"""Erzeugt Logo B „Saphir-Kristall“: trendo-logo-b.svg, trendo.ico (16–256 px) und PNGs (32/256/512).
Einzige Quelle der Geometrie ist src/Trendo.App/TrendoLogo.cs; gezeichnet wird mit Avalonia/Skia über tools/Trendo.Render."""
import os, pathlib, subprocess
root = pathlib.Path(__file__).resolve().parent.parent
d = root / "src/Trendo.App/Assets/logo"
subprocess.run(["dotnet", "run", "--project", str(root / "tools/Trendo.Render"), "-c", "Release"], check=True, env={**os.environ, "TRENDO_LOGO": str(d)})
print("ok", d)
