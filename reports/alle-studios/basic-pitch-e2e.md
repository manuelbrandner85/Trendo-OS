# Echter Lauf: basic-pitch (Noten erkennen, auch Akkorde) – 08.11.2026

Freigabe des Nutzers: „Ja, alle testen“. Eigene Python-Umgebung unter `C:\Users\<user>\AppData\Local\Temp\trendo-tools-b\py-basicpitch` (Python 3.12, nur Binärpakete).

## Befund Einrichtung
- `basic-pitch[onnx]==0.4.0` lässt sich unter Windows mit Python 3.12 nicht einrichten: Es verlangt `tensorflow<2.15.1`, das es für Python 3.12 nicht gibt (pip: „No matching distribution“).
- Lösung (in `KiPyApps` übernommen): basic-pitch mit `--no-deps`, dazu nur onnxruntime 1.31.1, numpy 2.2.6, scipy 1.18.1, librosa 1.0.0, pretty_midi 0.2.11.post0, mir_eval 0.8.2, resampy 0.4.3, soundfile 0.14.0. Das Modell `saved_models/icassp_2022/nmp.onnx` liegt im Paket – kein TensorFlow, keine Grafikkarte nötig.

## Lauf
- Testklang (FFmpeg `aevalsrc`, 22,05 kHz, 3,5 s): 0–2 s C-Dur-Akkord C4+E4+G4, dann A4, H4, C5 – `akkord.wav` SHA-256 71bbd41378c4140958357052c9bf91cdc03601e0a39176700f315169d875209b.
- `trendo_noten.py` (Trendo-Skript aus `src/Trendo.App/Assets/ki`): „6 Noten erkannt“, Dauer ≈ 1 s.
  - Noten: 60/64/67 ab 0,012 s bis ≈ 1,95 s (Akkord), 69 ab 1,987 s, 71 ab 2,486 s, 72 ab 2,99 s – alle richtig.
  - `akkord2.mid` SHA-256 828179426cac124addbfc0f206d20d0c3f06abc9154329246f792b3a4715febc, `akkord2.json` SHA-256 9f022fc46d524d57399d70f4b3255824421c9b4292ea92a09ad05020551b553b.
- Fehlende Eingabedatei: Meldung „Datei fehlt: fehlt.wav“, Exit 2.

## Tests
- `BasicPitchTests` (Kern): 8 grün – die JSON-Beispiele sind die echte Ausgabe dieses Laufs.
- `BasicPitchUi` (Trendo Musik → Spur → MIDI, basic-pitch nachgebildet): grün.

## Nicht geprüft
- Einrichtung über die Trendo-KI-Bibliothek mit uv (Schritte geprüft, nicht ausgeführt).
- Echte Lieder bzw. getrennte Demucs-Spuren (nur Testklang).
