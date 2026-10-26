"""Logo-Vorschläge für Trendo OS (Wunsch Manuel 2026-10-26: „viel moderner und edler“) mit lokaler Bild-KI: ComfyUI + FLUX.2 klein 4B, nur 127.0.0.1.

Erzeugt Vorschläge nach docs/design/logo-vorschlaege/<id>.jpg (1024×1024), Herkunft in docs/design/LOGO_VORSCHLAEGE.json.
Das gewählte Motiv wird danach als Vektor (SVG) nachgebaut – das KI-Bild ist die Vorlage, nicht das fertige Logo.

  python tools/design/ki_logo.py [--nur id] [--seed 7]
"""
import json
import pathlib
import sys
import time

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from ki_studiobilder import GF, MODELL, ROOT, comfy, jpg  # noqa: E402

OUT = ROOT / ".trendo-local" / "ki-logo"
ZIEL = ROOT / "docs" / "design" / "logo-vorschlaege"
GEMEINSAM = ("app icon logo design, a single bold capital letter T as the only symbol, centered, perfectly symmetric, premium luxury tech brand, "
             "clean vector-like shapes, studio lighting, plain dark background, high detail, no other letters, no words, no text, no watermark, no mockup, no frame")
MOTIVE = {
    "obsidian-gold": ("Obsidian & Gold", "a polished black obsidian glass rounded square tile with a letter T made of brushed gold with a thin bright gold edge, subtle reflections, elegant and noble"),
    "saphir-kristall": ("Saphir-Kristall", "a letter T cut from deep blue sapphire crystal with sharp facets and inner light, floating above a dark navy background, soft blue glow"),
    "platin-squircle": ("Platin", "a brushed platinum metal rounded squircle app icon with an engraved minimal letter T, soft gradient from silver to graphite, very minimal, Apple-like refinement"),
    "orbit": ("Orbit", "a minimal letter T formed by glowing light lines with a thin elliptical orbit ring around it like a planet, deep navy space background, cyan and white light, futuristic and calm"),
    "fluessiges-glas": ("Flüssiges Glas", "a letter T made of liquid frosted glass with a smooth gradient from electric blue to violet inside a rounded square, glassmorphism, soft highlights"),
    "monogramm-linie": ("Monogramm", "an elegant thin-line monogram letter T inside a circle, single continuous champagne-gold line, luxury fashion house style, black background"),
}


def erzeugen(nur=None, seed=7):
    c = comfy()
    client = c.Comfy(GF.parents[1], OUT / "logs")
    if not client.online():
        sys.exit("ComfyUI läuft nicht: zuerst 'gf comfy start'.")
    OUT.mkdir(parents=True, exist_ok=True); ZIEL.mkdir(parents=True, exist_ok=True)
    reg = ROOT / "docs" / "design" / "LOGO_VORSCHLAEGE.json"
    daten = json.loads(reg.read_text(encoding="utf-8")) if reg.is_file() else {}
    for key, (name, motiv) in MOTIVE.items():
        if nur and key != nur:
            continue
        prompt = f"{motiv}. {GEMEINSAM}"
        wf = c.klein_t2i(prompt, seed, 1024, 1024, prefix=f"trendo-logo-{key}-{seed}")
        pid = client.queue(wf)
        for _ in range(900):
            h = client.history(pid)
            if h and h.get("outputs"):
                break
            time.sleep(1)
        else:
            sys.exit(f"Zeitüberschreitung bei {key}")
        img = h["outputs"]["30"]["images"][0]
        roh = OUT / f"{key}-{seed}.png"
        roh.write_bytes(client.fetch_image(img["filename"], img.get("subfolder", ""), img.get("type", "output")))
        ziel = ZIEL / f"{key}.jpg"
        jpg(roh, ziel)
        daten[key] = {"name": name, "datei": ziel.relative_to(ROOT).as_posix(), "seed": seed, "prompt": prompt, "modell": MODELL,
                      "erzeugt": time.strftime("%Y-%m-%d"), "lizenz": "eigene KI-Erzeugung für Trendo OS, Vorlage für einen Vektor-Nachbau"}
        print("Vorschlag:", ziel.name, flush=True)
    reg.write_text(json.dumps(daten, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    a = sys.argv[1:]
    erzeugen(a[a.index("--nur") + 1] if "--nur" in a else None, int(a[a.index("--seed") + 1]) if "--seed" in a else 7)
