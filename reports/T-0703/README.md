# Belege T-0703 – bestes vorhandenes Modell als Standard, Bildverstehens-Modell

Alle Läufe am 16.11.2026 auf dem Prüf-PC (RTX 5070 12 GB, 32 GB Arbeitsspeicher, Ollama 0.40.11 der Person – nur benutzt, nie beendet). Benutzer- und Rechnernamen ersetzt.

| Datei | Was | Echt / ersetzt |
|---|---|---|
| `ki-17plus25.md` | „Was ist 17 + 25?“ über `MainWindow.KiModelChoice` → `Intel.AskAsync` → Ollama: automatisch **qwen3:14b**, Antwort „17 + 25 = 42“ (8,6 s) | echt; Modellliste aus den echten Manifesten, Grafikspeicher echt per nvidia-smi (im Testfenster explizit gesetzt, weil das Testfenster die Hardware-Abfrage abschaltet) |
| `echtlauf-helfer-ollama.log` | Echtlauf-Helfer (`tools/echtlauf`, Fachlauf ollama) mit derselben Auswahlregel: qwen3:14b → „42“ (vorher 16.11.: qwen2.5:0.5b → „32“, ROT) | echt |
| `speicher-vorher.txt` | Laufwerk C: vor dem Laden: 41 GB frei (Grenze 15 GB) | echt |
| `vlm-laden.md`, `vlm-laden.log` | Qwen 2.5 VL 7B (`qwen2.5vl:7b`, Apache-2.0) über `KiLibraryInstall` (Weg des Knopfs „Laden“): Freigabe `ki.pull`, Fortschritt je Prozent, „verifying sha256 digest“, „success“ nach 20 min; Audit `ki.model.pull.ok … gewichte=sha256:a99b7f83…` (= Schicht im offiziellen Manifest von registry.ollama.ai) | echt (5 969 233 408 Bytes geladen) |
| `vlm-beschreibung.md`, `vlm-testfoto.jpg` | „Bilder erkennen“ → „Ordner erkennen …“ → qwen2.5vl:7b lokal: „Im Bild ist ein Hund auf einer Veranda … neben einem roten Fahrrad …“, Schlagworte Hund, Veranda, Fahrrad, Auto, Bäume, Frieden, Alltag | Beschreibung echt über Ollama; nur die Modellsuche ist im Testfenster auf den Namen des wirklich vorhandenen Modells gesetzt (`VisionLocalModelOverride`), weil das isolierte Testfenster Ollama sonst nicht abfragt |
| `ki-quellen.png`, `ki-modell.png` | Oberfläche (Trendo.Render, 1600×1000): Satz „Standard für Chat und Agenten: … weil …“, Knöpfe „Automatisch“ / eigene Wahl | gerendert ohne Modelle (Render-Umgebung ist isoliert) – zeigt darum den Satz für „noch kein Modell“; der Fall qwen3:14b ist im UI-Test geprüft |
| `SHA256SUMS` | Prüfsummen aller Dateien hier | – |

Testfoto: YOLOX-Beispielbild `dog.jpg` (Megvii, Apache-2.0) aus der Prüfumgebung von T-0699.
