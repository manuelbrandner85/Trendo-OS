"""Bilder für den Desktop nach dem Referenzbild vom 2026-10-25 (T-0224) mit lokaler Bild-KI: ComfyUI + FLUX.2 klein 4B, nur 127.0.0.1.

  erde   -> src/Trendo.App/Assets/desktop/erde.jpg   (Erdkugel bei Nacht für die linke Spalte)
  orbit  -> src/Trendo.App/Assets/wallpapers/orbit.jpg (dunkler Weltall-Hintergrund mit Erdhorizont)
Herkunft in docs/design/DESKTOPBILDER.json.

Aufruf:
  python tools/design/ki_desktopbilder.py [--nur erde] [--seed 41]
"""
import json
import os
import pathlib
import sys
import time

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from ki_studiobilder import GF, MODELL, ROOT, comfy, jpg  # noqa: E402

OUT = ROOT / ".trendo-local" / "ki-desktopbilder"
NEG = "no text, no letters, no numbers, no logo, no watermark, no user interface, no frame"
BILDER = {
    "erde": ("src/Trendo.App/Assets/desktop/erde.jpg", 768, 768,
             "planet earth seen from space at night, the lower left part of the globe visible as a large sphere, glowing city lights on the continents, "
             "thin bright blue atmosphere rim light, deep black navy space background with a few faint stars, photorealistic NASA style photograph, " + NEG),
    "orbit": ("src/Trendo.App/Assets/wallpapers/orbit.jpg", 1536, 864,
              "deep navy blue night sky in space with many small stars and a faint blue nebula, the curved glowing blue horizon of planet earth at the bottom, "
              "subtle snowy mountain silhouettes far right, very dark and calm, lots of empty dark space for user interface, photorealistic, " + NEG),
}


def erzeugen(nur=None, seed=41):
    c = comfy()
    client = c.Comfy(GF.parents[1], OUT / "logs")
    if not client.online():
        sys.exit("ComfyUI läuft nicht: zuerst 'gf comfy start'.")
    OUT.mkdir(parents=True, exist_ok=True)
    reg = ROOT / "docs" / "design" / "DESKTOPBILDER.json"
    daten = json.loads(reg.read_text(encoding="utf-8")) if reg.is_file() else {}
    for key, (ziel_rel, w, h, prompt) in BILDER.items():
        if nur and key != nur:
            continue
        wf = c.klein_t2i(prompt, seed, w, h, prefix=f"trendo-desktop-{key}-{seed}")
        pid = client.queue(wf)
        for _ in range(900):
            hist = client.history(pid)
            if hist and hist.get("outputs"):
                break
            time.sleep(1)
        else:
            sys.exit(f"Zeitüberschreitung bei {key}")
        img = hist["outputs"]["30"]["images"][0]
        roh = OUT / f"{key}-{seed}.png"
        roh.write_bytes(client.fetch_image(img["filename"], img.get("subfolder", ""), img.get("type", "output")))
        ziel = ROOT / ziel_rel
        ziel.parent.mkdir(parents=True, exist_ok=True)
        jpg(roh, ziel)
        daten[key] = {"datei": ziel_rel, "seed": seed, "prompt": prompt, "modell": MODELL, "erzeugt": time.strftime("%Y-%m-%d"),
                      "lizenz": "eigene KI-Erzeugung für Trendo OS, keine Fremdfotos"}
        print("Bild:", ziel_rel, flush=True)
    reg.write_text(json.dumps(daten, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    a = sys.argv[1:]
    erzeugen(a[a.index("--nur") + 1] if "--nur" in a else None, int(a[a.index("--seed") + 1]) if "--seed" in a else 41)
