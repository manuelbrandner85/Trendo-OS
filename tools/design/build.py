"""Rendert die Design-41-Referenzen (tools/design/views.py) mit Chrome (headless) – reproduzierbar, ohne Netz.

Hintergründe entstehen separat mit lokaler Bild-KI: tools/design/ki_hintergruende.py.
Aufruf:  python tools/design/build.py [--chrome <pfad>]
Ausgabe: docs/spec/design-41/referenzen/*.png, REFERENZEN_MANIFEST.json
"""
import os
import pathlib
import shutil
import subprocess
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import views  # noqa: E402

CHROME_CANDIDATES = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "google-chrome", "chromium", "chromium-browser",
]


def find_chrome(explicit=None):
    for c in ([explicit] if explicit else []) + CHROME_CANDIDATES:
        if c and (os.path.isfile(c) or shutil.which(c)):
            return c
    sys.exit("Chrome/Chromium/Edge nicht gefunden – mit --chrome <pfad> angeben.")


def shoot(chrome, html_file, png_file, w, h):
    """Rendert eine lokale HTML-Datei als PNG (headless, ohne Netz)."""
    with tempfile.TemporaryDirectory(prefix="trendo-design-") as prof:
        cmd = [chrome, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run", "--no-default-browser-check",
               f"--user-data-dir={prof}", "--force-device-scale-factor=1", f"--window-size={w},{h}",
               "--virtual-time-budget=4000", f"--screenshot={png_file}", pathlib.Path(html_file).as_uri()]
        subprocess.run(cmd, check=True, capture_output=True, timeout=180)
    if not pathlib.Path(png_file).is_file():
        sys.exit(f"Rendern fehlgeschlagen: {html_file}")


def main(argv):
    chrome = find_chrome(argv[argv.index("--chrome") + 1] if "--chrome" in argv else None)
    with tempfile.TemporaryDirectory(prefix="trendo-design-out-") as t:
        views.build(chrome, pathlib.Path(t), shoot)


if __name__ == "__main__":
    main(sys.argv[1:])
