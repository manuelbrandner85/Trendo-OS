"""Trendo Musik: Lied in Spuren trennen mit Demucs (htdemucs, MIT, Meta).

Aufruf: python trendo_spuren.py <repo> <audiodatei> <zielordner>
Schreibt gesang.wav, schlagzeug.wav, bass.wav, rest.wav und begleitung.wav (alles ohne Gesang).
Laden und Speichern laufen über soundfile, damit kein ffmpeg nötig ist."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
os.environ.setdefault("TORCH_HOME", os.path.join(HERE, "modelle"))  # Modell bleibt im Werkzeugordner

import numpy as np
import soundfile as sf
import torch
import julius
from demucs.apply import apply_model
from demucs.pretrained import get_model

NAMES = {"vocals": "gesang", "drums": "schlagzeug", "bass": "bass", "other": "rest", "guitar": "gitarre", "piano": "klavier"}


def main():
    src, out = sys.argv[-2], sys.argv[-1]
    # Modellwahl (V7, 07.11.2026): „htdemucs“ schnell, „htdemucs_ft“ genauer (vier Modelle, etwa 4× langsamer, einmal ca. 320 MB laden)
    name = "htdemucs"
    if "--modell" in sys.argv:
        name = sys.argv[sys.argv.index("--modell") + 1]
    # Suno V9 (07.11.2026): „htdemucs_6s“ – sechs Spuren, zusätzlich Gitarre und Klavier
    if name not in ("htdemucs", "htdemucs_ft", "htdemucs_6s"):
        print("Unbekanntes Modell: " + name, file=sys.stderr)
        sys.exit(2)
    os.makedirs(out, exist_ok=True)
    print("Modell laden: " + name + " …", flush=True)
    model = get_model(name)
    model.eval()
    data, sr = sf.read(src, dtype="float32", always_2d=True)
    wav = torch.from_numpy(data.T.copy())
    if wav.shape[0] == 1:
        wav = wav.repeat(2, 1)
    wav = wav[:2]
    if sr != model.samplerate:
        wav = julius.resample_frac(wav, sr, model.samplerate)
    device = "cuda" if torch.cuda.is_available() else "cpu"
    ref = wav.mean(0)
    mean, std = ref.mean(), ref.std() + 1e-8
    print(f"Trennen auf {device} …", flush=True)
    def run(dev):
        with torch.no_grad():
            return apply_model(model, ((wav - mean) / std)[None], device=dev, shifts=1, split=True, overlap=0.25, progress=False)[0]
    try:
        stems = run(device)
    except torch.OutOfMemoryError:  # Grafikspeicher belegt (z. B. von der Musik-KI): auf dem Prozessor weiter
        torch.cuda.empty_cache()
        print("Grafikspeicher voll – Trennen auf dem Prozessor …", flush=True)
        stems = run("cpu")
    stems = stems * std + mean
    if sr != model.samplerate:  # zurück auf die Abtastrate des Originals, damit die Spuren genau darauf passen
        stems = julius.resample_frac(stems, model.samplerate, sr)
    acc = torch.zeros_like(stems[0])
    for name, s in zip(model.sources, stems):
        sf.write(os.path.join(out, NAMES.get(name, name) + ".wav"), s.cpu().numpy().T, sr, subtype="PCM_16")
        if name != "vocals":
            acc += s
    sf.write(os.path.join(out, "begleitung.wav"), acc.cpu().numpy().T, sr, subtype="PCM_16")
    print("fertig", flush=True)


if __name__ == "__main__":
    main()
