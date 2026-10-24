"""Kartenbilder für „Alle Studios“ (T-0218, Referenzbild 2) mit lokaler Bild-KI: ComfyUI + FLUX.2 klein 4B, nur 127.0.0.1.

Ein Motiv je Studio-ID aus StudioCatalog.cs, gleicher Stil, ohne Schrift. Ergebnis: src/Trendo.App/Assets/studios/<ID>.jpg (768×432),
Herkunft in docs/design/STUDIOBILDER.json.

Aufruf:
  python tools/design/ki_studiobilder.py [--nur ID] [--seed 29]
"""
import json
import os
import pathlib
import subprocess
import sys
import time

ROOT = pathlib.Path(__file__).resolve().parents[2]
GF = pathlib.Path(os.environ.get("TRENDO_GAMEFORGE", r"C:\Users\Public\GAMEFORGE")) / "gameforge-plugin" / "scripts"
OUT = ROOT / ".trendo-local" / "ki-studiobilder"
ZIEL = ROOT / "src" / "Trendo.App" / "Assets" / "studios"
MODELL = "FLUX.2 klein 4B (flux-2-klein-4b.safetensors, qwen_3_4b, flux2-vae), 4 Schritte, euler, cfg 1.0"

STIL = ("cinematic photograph, one clear hero subject centered, deep navy blue and teal color grading with soft cyan rim light and a warm accent, "
        "subtle glow, shallow depth of field, photorealistic, high detail, clean composition, "
        "no text, no letters, no numbers, no logo, no watermark, no user interface, no screen content, no frame")

MOTIVE = {
    "STUDIO-LETTER-01": "an elegant fountain pen resting on a sealed paper letter with a wax seal on a dark wooden desk",
    "SYS-10": "a neat stack of printed documents and spreadsheet printouts with a pair of glasses on a dark desk",
    "SYS-09": "a glowing envelope icon sculpture beside a paper desk calendar on a dark glass desk",
    "X-11": "a single open cardboard box with soft light coming out of it on a dark studio floor",
    "SYS-02": "a vintage calculator, sticky notes and small headphones arranged on a dark desk",
    "STUDIO-ONLINE-BUSINESS-01": "a small parcel box with a shopping bag and a rocket figurine on a dark display table",
    "SPEC-11": "a stack of gold coins next to a small calculator and paper receipts on a dark desk",
    "CORE-08": "a professional camera with a lens and a film reel on a dark studio table",
    "SPEC-15": "a broadcast microphone on an arm with a red on-air lamp glowing in a dark studio",
    "SPEC-08": "a fashion mannequin wearing an elegant dark dress in a moody design studio",
    "X-07": "a vinyl record, a paint brush and a photo print arranged on a dark table",
    "CORE-12": "a glowing quill pen writing light trails above a dark notebook",
    "CORE-05": "a translucent glowing glass cube floating above a dark reflective floor",
    "CORE-06": "a modern game controller with soft blue light on a dark reflective surface",
    "SPEC-04": "a virtual reality headset on a dark pedestal with soft blue glow",
    "SPEC-09": "a handheld 3D scanner projecting a blue light grid onto a small statue",
    "SPEC-01": "a modern apartment interior architectural model on a dark table, warm interior lights",
    "SPEC-02": "a green printed circuit board with glowing traces and electronic components, macro shot",
    "SPEC-03": "a steel I-beam and engineering calipers on a dark workbench",
    "SPEC-10": "an industrial sensor device with small status LEDs on a dark metal surface",
    "SPEC-13": "a folded paper map with a compass and a GPS device on a dark wooden table",
    "SPEC-14": "a CNC milling machine cutter carving aluminum with flying sparks, close-up",
    "SPEC-16": "a modern car dashboard at night with glowing instruments, seen from the driver seat",
    "CORE-09": "a white industrial robot arm with a small camera in a dark lab",
    "SPEC-05": "a laboratory oscilloscope with a glowing waveform in a dark lab",
    "SPEC-06": "a stack of paper flash cards and a pencil on a dark desk",
    "SPEC-07": "two open books in different scripts lying side by side under a soft desk lamp",
    "CORE-10": "a glowing glass brain sculpture on a dark pedestal",
    "SYS-08": "a magnifying glass over a glowing globe on a dark desk",
    "CORE-02": "a glowing blue sphere of light with swirling energy floating in darkness",
    "CORE-03": "several small white robot figurines standing together in a circle on a dark table",
    "X-05": "a glowing open treasure chest with soft blue light on a dark floor",
    "SYS-05": "interlocking metal gears turning with soft blue light in darkness",
    "X-01": "a large modern city skyline at blue hour seen from above",
    "CORE-07": "a laptop with code-like colorful light reflections on a dark desk, no readable text",
    "X-03": "colorful building blocks forming a small app shape on a dark table",
    "X-06": "a glowing octopus-shaped sculpture on a dark pedestal, cinematic",
    "X-04": "a puzzle piece made of glowing glass fitting into a dark puzzle",
    "X-10": "architectural blueprints rolled out on a dark drafting table with a ruler",
    "CORE-01": "a minimal modern computer monitor on a dark desk with a mountain lake wallpaper glow",
    "SYS-01": "a power button glowing blue on a dark brushed metal surface",
    "CORE-11": "a glowing blue shield emblem made of glass on a dark pedestal",
    "SPEC-12": "a forensic magnifying lens over a hard disk drive on a dark evidence table",
    "SYS-03": "a glowing download arrow made of light above a dark keyboard",
    "SYS-04": "a stack of software install boxes with soft blue light on a dark shelf",
    "SYS-06": "two laptops connected by a glowing blue light beam on a dark table",
    "SYS-07": "a printer, a webcam and a USB stick arranged on a dark desk",
    "SYS-11": "a sleek rocket launching from a dark desk with a soft blue exhaust glow",
    "SYS-12": "large colorful letter tiles and a color palette on a dark table",
    "SYS-13": "a green checkmark made of glowing glass on a dark pedestal",
    "X-02": "a wrench and a screwdriver crossed on a dark workbench with blue light",
    "X-08": "a modern smartphone standing on a dark desk with a soft blue wireless glow",
    "CORE-04": "a raspberry pi single board computer, a laptop and a smartphone together on a dark desk",
    "X-09": "a computer processor chip glowing with blue light on a motherboard, macro shot",
}


def comfy():
    sys.path.insert(0, str(GF))
    from gflib import comfy as c  # noqa: E402
    return c


def jpg(src, ziel):
    ps = (f"Add-Type -AssemblyName System.Drawing; $i=[System.Drawing.Image]::FromFile('{src}');"
          "$c=[System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders()|?{$_.MimeType -eq 'image/jpeg'};"
          "$p=New-Object System.Drawing.Imaging.EncoderParameters 1;$p.Param[0]=New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::Quality,[long]82);"
          f"$i.Save('{ziel}',$c,$p);$i.Dispose()")
    subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", ps], check=True, capture_output=True, timeout=120)


def erzeugen(nur=None, seed=29):
    c = comfy()
    client = c.Comfy(GF.parents[1], OUT / "logs")
    if not client.online():
        sys.exit("ComfyUI läuft nicht: zuerst 'gf comfy start'.")
    OUT.mkdir(parents=True, exist_ok=True); ZIEL.mkdir(parents=True, exist_ok=True)
    reg = ROOT / "docs" / "design" / "STUDIOBILDER.json"
    daten = json.loads(reg.read_text(encoding="utf-8")) if reg.is_file() else {}
    for sid, motiv in MOTIVE.items():
        if nur and sid != nur:
            continue
        prompt = f"{motiv}. {STIL}"
        wf = c.klein_t2i(prompt, seed, 768, 432, prefix=f"trendo-studio-{sid}-{seed}")
        pid = client.queue(wf)
        for _ in range(600):
            h = client.history(pid)
            if h and h.get("outputs"):
                break
            time.sleep(1)
        else:
            sys.exit(f"Zeitüberschreitung bei {sid}")
        img = h["outputs"]["30"]["images"][0]
        roh = OUT / f"{sid}-{seed}.png"
        roh.write_bytes(client.fetch_image(img["filename"], img.get("subfolder", ""), img.get("type", "output")))
        ziel = ZIEL / f"{sid}.jpg"
        jpg(roh, ziel)
        daten[sid] = {"datei": ziel.relative_to(ROOT).as_posix(), "seed": seed, "prompt": prompt, "modell": MODELL,
                      "erzeugt": time.strftime("%Y-%m-%d"), "lizenz": "eigene KI-Erzeugung für Trendo OS, keine Fremdfotos"}
        print("Bild:", ziel.name, flush=True)
    reg.write_text(json.dumps(daten, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    a = sys.argv[1:]
    erzeugen(a[a.index("--nur") + 1] if "--nur" in a else None, int(a[a.index("--seed") + 1]) if "--seed" in a else 29)
