"""Trendo-Hintergründe mit lokaler Bild-KI: ComfyUI + FLUX.2 klein 4B, nur 127.0.0.1, ohne Cloud.

Nutzt den ComfyUI-Client aus GameForge (Pfad über TRENDO_GAMEFORGE oder Standardpfad).
Jede Szene: Anweisung + feste Startwerte → mehrere Kandidaten unter <out>/kandidaten/, Auswahl per --waehle.

Aufruf:
  python tools/design/ki_hintergruende.py erzeugen [--szene NAME] [--anzahl 3]
  python tools/design/ki_hintergruende.py waehlen NAME KANDIDAT.png
"""
import json
import os
import pathlib
import subprocess
import sys
import time

ROOT = pathlib.Path(__file__).resolve().parents[2]
GF = pathlib.Path(os.environ.get("TRENDO_GAMEFORGE", r"C:\Users\Public\GAMEFORGE")) / "gameforge-plugin" / "scripts"
OUT = ROOT / ".trendo-local" / "ki-hintergruende"
ASSETS = ROOT / "src" / "Trendo.App" / "Assets"
MODELL = "FLUX.2 klein 4B (flux-2-klein-4b.safetensors, qwen_3_4b, flux2-vae), 4 Schritte, euler, cfg 1.0"

STIL = ("cinematic wide landscape photograph, 16:9, ultra detailed, photorealistic, natural light, calm balanced composition "
        "with a quiet uncluttered center, pure scenery only, no user interface, no taskbar, no icons, no buttons, no screen, "
        "no people, no animals, no text, no letters, no logo, no watermark, no frame")
SZENEN = {
    "wallpaper": "Majestic alpine mountain lake at blue hour, snow-capped granite peaks, perfectly calm water with a mirror reflection, "
                 "dark pine forest along the shore, deep navy sky with first stars and a faint cyan glow above the ridge",
    "bergsee-nacht": "Clear night over a remote mountain lake, Milky Way above jagged snowy peaks, bright moon behind thin clouds, "
                     "silver moonlight reflecting on still water, silhouettes of spruce trees, deep blue and teal tones",
    "morgen": "Sunrise in a quiet mountain valley, soft golden and pink light touching the summits, morning mist drifting over a "
              "glassy lake, warm and fresh atmosphere, gentle pastel sky",
    "polarlicht": "Green and violet aurora borealis dancing over snow-covered mountains and a frozen lake, starry arctic night, "
                  "the aurora reflected on smooth ice, crisp cold air",
    "wellen": "Abstract flowing translucent glass ribbons and soft light waves, deep navy, electric blue and violet gradients, "
              "subtle glow and depth of field, elegant minimal digital art, dark background",
}
SEEDS = [11, 29, 47, 83, 131]


def comfy():
    sys.path.insert(0, str(GF))
    from gflib import comfy as c  # noqa: E402
    return c


def erzeugen(nur=None, anzahl=3):
    c = comfy()
    client = c.Comfy(GF.parents[1], OUT / "logs")
    if not client.online():
        sys.exit("ComfyUI läuft nicht: zuerst 'gf comfy start'.")
    (OUT / "kandidaten").mkdir(parents=True, exist_ok=True)
    protokoll = []
    for name, motiv in SZENEN.items():
        if nur and name != nur:
            continue
        for seed in SEEDS[:anzahl]:
            prompt = f"{motiv}. {STIL}"
            wf = c.klein_t2i(prompt, seed, 1920, 1088, prefix=f"trendo-{name}-{seed}")
            wf["25"] = {"class_type": "ImageCrop", "inputs": {"image": ["12", 0], "width": 1920, "height": 1080, "x": 0, "y": 4}}
            wf["30"]["inputs"]["images"] = ["25", 0]
            pid = client.queue(wf)
            for _ in range(600):
                h = client.history(pid)
                if h and h.get("outputs"):
                    break
                time.sleep(1)
            else:
                sys.exit(f"Zeitüberschreitung bei {name}/{seed}")
            img = h["outputs"]["30"]["images"][0]
            data = client.fetch_image(img["filename"], img.get("subfolder", ""), img.get("type", "output"))
            ziel = OUT / "kandidaten" / f"{name}-{seed}.png"
            ziel.write_bytes(data)
            protokoll.append({"szene": name, "seed": seed, "datei": ziel.name, "prompt": prompt, "modell": MODELL})
            print("Kandidat:", ziel.name)
    (OUT / "protokoll.json").write_text(json.dumps(protokoll, ensure_ascii=False, indent=2), encoding="utf-8")


def waehlen(name, kandidat):
    """Übernimmt einen Kandidaten als JPG ins Projekt und trägt Herkunft in docs/design/HINTERGRUENDE.json ein."""
    src = OUT / "kandidaten" / kandidat
    ziel = ASSETS / ("wallpaper.jpg" if name == "wallpaper" else f"wallpapers/{name}.jpg")
    ps = (f"Add-Type -AssemblyName System.Drawing; $i=[System.Drawing.Image]::FromFile('{src}');"
          "$c=[System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders()|?{$_.MimeType -eq 'image/jpeg'};"
          "$p=New-Object System.Drawing.Imaging.EncoderParameters 1;$p.Param[0]=New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::Quality,[long]90);"
          f"$i.Save('{ziel}',$c,$p);$i.Dispose()")
    subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", ps], check=True, capture_output=True, timeout=120)
    seed = int(kandidat.rsplit("-", 1)[1].split(".")[0])
    reg = ROOT / "docs" / "design" / "HINTERGRUENDE.json"
    daten = json.loads(reg.read_text(encoding="utf-8")) if reg.is_file() else {}
    daten[name] = {"datei": ziel.relative_to(ROOT).as_posix(), "seed": seed, "prompt": f"{SZENEN[name]}. {STIL}",
                   "modell": MODELL, "erzeugt": time.strftime("%Y-%m-%d"), "lizenz": "eigene KI-Erzeugung für Trendo OS, keine Fremdfotos"}
    reg.write_text(json.dumps(daten, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Übernommen:", ziel.relative_to(ROOT))


if __name__ == "__main__":
    a = sys.argv[1:]
    if a and a[0] == "erzeugen":
        erzeugen(a[a.index("--szene") + 1] if "--szene" in a else None, int(a[a.index("--anzahl") + 1]) if "--anzahl" in a else 3)
    elif len(a) == 3 and a[0] == "waehlen":
        waehlen(a[1], a[2])
    else:
        sys.exit(__doc__)
