# T-0699 – Belege: „Bilder erkennen“ und „Videos mit KI“ mit echten Werkzeugen

Stand 16.11.2026, Zweig `claude/T-0699-vision-video`. Pfade mit Benutzernamen sind durch `C:\Users\<user>` ersetzt. SHA-256 aller Dateien: `SHA256SUMS.txt`.

| Beleg | Werkzeug (echt) | Datei |
|---|---|---|
| Objekte mit Rahmen: Hund 0,91, Fahrrad 0,95, Lastwagen, Topfpflanze auf dem YOLOX-Demobild | YOLOX-S (onnxruntime, Modell mit gepinnter Prüfsumme) über Trendos `trendo_objekte.py` | `roh/bilder-erkennen-echt.log` |
| Texterkennung „Rechnung Nummer 4711 / Betrag: 128 Euro“ aus einem von Trendo gezeichneten Bild | Tesseract 5.4 (deu) | `roh/bilder-erkennen-echt.log`, `roh/brief-fuer-tesseract.png` |
| Suche „zeig alle Fotos mit Hund“ findet genau das Hundefoto, „Fotos mit Rechnung“ genau den Brief | Trendo `VisionTags` auf den echten Ergebnissen | Test `VisionVideoT0699_Ui.Vision_real_tools` |
| Seitendatei neben dem Foto (Foto bleibt unverändert) | – | `roh/hund-und-rad.jpg.xmp` |
| CSV mit Schlagworten, Objekten, Text | – | `roh/bilder-erkennen-liste.csv` |
| Bildbeschreibung mit Schlagworten über Claude (eigenes Abo, Frage-Modus, nur Read-Werkzeug) | Claude Code 2.1.319 | `roh/claude-bildbeschreibung.log`, Skript `claude_bildbeschreibung.py` |
| Bildbeschreibung lokal (Ollama-Bildverstehens-Modell) | **BLOCKIERT** – auf diesem PC ist kein Modell unter „Bilder verstehen“ eingerichtet (Ollama hat nur Text-Modelle). Einrichten-Weg: KI-Bibliothek → „Bilder verstehen“ (z. B. Qwen 2.5 VL 7B) – nur nach Zustimmung, nicht geladen. | – |
| Video: Schlüsselbilder (SDXL Turbo), Wan-2.2-Clip, Sprecher (Piper Thorsten), Musik, Untertitel, MP4, ffprobe, Bewegung | sd-cli, Piper, FFmpeg/ffprobe 9.0.2 | `roh/videos-mit-ki-echt.log`, `roh/video-*.png`, `roh/video-untertitel.srt`, `roh/video-herkunft.json` |
| Oberfläche im Bild | Trendo.Render | `*.png` in diesem Ordner |

Echtläufe: `TRENDO_ECHT_VIDEO=1 TRENDO_ECHT_WAN=1 TRENDO_BELEG=<ordner> Trendo.App.Tests.exe -class Trendo.App.Tests.VisionVideoT0699_Ui`.
Ersetzt ist dort nur die Werkzeugsuche (der Testmodus sucht keine Werkzeuge) – Bild-KI, Stimme, FFmpeg, YOLOX, Tesseract und Wan laufen echt.
