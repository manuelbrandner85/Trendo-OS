# Trendo – Noten aus Audio (mehrstimmig) mit Spotify basic-pitch 0.4.0 (Apache-2.0), ONNX-Modell aus dem Paket, ohne TensorFlow.
# Aufruf: python trendo_noten.py <repo (unbenutzt)> <eingabe.wav> <ausgabe.mid> <ausgabe.json>
# Geprüft 08.11.2026: C-Dur-Akkord (60/64/67) und Tonfolge 69/71/72 richtig erkannt, 3,5 s Ton in 1 s.
import json
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 5:
        print("Aufruf: trendo_noten.py <repo> <eingabe> <ausgabe.mid> <ausgabe.json>", file=sys.stderr)
        return 2
    src, mid, out = Path(sys.argv[2]), Path(sys.argv[3]), Path(sys.argv[4])
    if not src.is_file():
        print(f"Datei fehlt: {src.name}", file=sys.stderr)
        return 2
    import basic_pitch
    from basic_pitch.inference import predict

    model = Path(basic_pitch.__file__).parent / "saved_models" / "icassp_2022" / "nmp.onnx"
    if not model.is_file():
        print("ONNX-Modell von basic-pitch fehlt.", file=sys.stderr)
        return 3
    _, midi, events = predict(str(src), str(model))
    midi.write(str(mid))
    notes = sorted(
        ({"tonhoehe": int(p), "start": round(float(s), 3), "ende": round(float(e), 3), "staerke": round(float(a), 3)} for s, e, p, a, _ in events),
        key=lambda n: (n["start"], n["tonhoehe"]),
    )
    out.write_text(json.dumps({"werkzeug": "basic-pitch 0.4.0", "noten": notes}, ensure_ascii=False), encoding="utf-8")
    print(f"{len(notes)} Noten erkannt")
    return 0


if __name__ == "__main__":
    sys.exit(main())
