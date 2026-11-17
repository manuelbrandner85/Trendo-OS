# Bericht T-0699: D6b – „Bilder erkennen“ erkennt wirklich, „Videos mit KI“ erzeugt wirklich Video

**Zweig:** `claude/T-0699-vision-video` (vom Integrationszweig `claude/T-0685-b1-integration`) · **Stand:** 16.11.2026

## Was war (Audit-Teilbericht 4)
- STUDIO-VISION-01: nur EXIF, Farben, Helligkeit, Schärfe; keine Inhaltserkennung; OCR nur mit Tesseract, ungetestet; Claude-Antwort nur im Chat.
- STUDIO-VIDEOAI-01: Diashow aus Standbildern ohne Ton (`-an`); „Szenen planen“ ist ein Parser; echter KI-Clip nur Verweis auf kie.ai.

## Was ist jetzt
**Bilder erkennen**
- Erkennung je Bild aus drei echten, lokalen Quellen: Objekte/Personen mit Rahmen (YOLOX-S über die vorhandene KI-Laufzeit „Objekte“), Bildbeschreibung mit Schlagworten (eingerichtetes Bildverstehens-Modell der KI-Bibliothek über Ollama; ohne lokales Modell nur auf ausdrücklichen Haken Claude über das eigene Abo), Text im Bild (Tesseract). Fehlt eine Quelle, läuft der Rest; die Werkzeugzeile zeigt, was bereit ist, mit Einrichten-Weg (YOLOX: offizielles Release mit Prüfsumme, erst nach Klick; Bildverstehen: KI-Bibliothek; Tesseract: Zusatzprogramme). Nichts wird ohne Klick geladen.
- Stapel „Ordner erkennen …“ (bis 500 Bilder, mit Unterordnern, Anhalten; unveränderte Fotos werden über die Prüfsumme übersprungen).
- Suche in Alltagssprache („zeig alle Fotos mit Hund“; Einzahl/Mehrzahl, ganze Wörter), Galerie der Treffer, Bild mit goldenen Rahmen, Doppelte über die Prüfsumme.
- Ergebnisse im Dokument (übersteht Neustart), Seitendatei `foto.jpg.xmp` (dc:subject/dc:description; Foto unverändert; fremde Seitendateien werden nie überschrieben), CSV mit Schlagworten/Objekten/Text.
- EXIF, Belichtung, Schärfe, „Ordner prüfen“, OCR-Knopf und Claude-Knopf bleiben.
- Erster Schritt im Leerzustand („Ordner erkennen …“, golden), sonst genau eine goldene Hauptaktion im Menüband. Studio-Satz und Hilfe beschreiben die echte Leistung.

**Videos mit KI**
- Schlüsselbilder mit der lokalen Bild-KI (wie bisher), echte Bewegung: Kamerafahrt/Zoom/Schwenk + Überblendung; Vorschau der Kamerafahrt (Anfang → Ende) in der Szenenliste.
- Neu je Szene „Mit Video-KI bewegen“: aus dem Schlüsselbild entsteht ein echter KI-Clip mit Wan 2.2 TI2V-5B (lokal, über `GenVideo`, Herkunftsnachweis), 1,5 s, auf Szenenlänge verlangsamt; im Video ersetzt er die Kamerafahrt. Benennung je Szene: „Bild mit Kamerafahrt“ bzw. „KI-Video (bewegt)“; die Werkzeugkarte sagt, was ohne Modell entsteht.
- Ton: Sprechertext je Szene („Sprecher: …“ im Plan oder im Feld) mit der lokalen Stimme (Piper), eigene Musik (wird ins Dokument kopiert, leise unter dem Sprecher, ein-/ausgeblendet), Untertitel als eigene Spur (mov_text) plus `.srt` neben dem MP4. Ohne Stimme: Video mit Untertiteln und klarem Hinweis.
- Einblendungen jetzt direkt mit SkiaSharp gezeichnet (vorher Avalonia-Render – im Echtlauf ohne Bildschirm ungültige PNG, FFmpeg brach ab).
- Neue Vorlage „Beispiel mit Sprecher“. Goldene Hauptaktion: Menüband „Szenen planen“; Arbeitsfläche ohne zweite goldene Fläche.

## Belege (echt) – `reports/T-0699/` (README, SHA256SUMS)
- YOLOX-S echt: Hund 0,91 / Fahrrad 0,95 / Lastwagen / Topfpflanze mit Rahmen; Suche findet genau das Hundefoto – `Vision_real_tools` grün.
- Tesseract echt: „Rechnung Nummer 4711 / Betrag: 128 Euro“ aus einem Trendo-Bild – grün.
- Claude über das Abo echt (10,5 s, nur Read-Werkzeug): Beschreibung + „Schlagworte: Hund, Rennrad, …“; die Antwort ist als Testfall hinterlegt.
- Bildbeschreibung lokal: **BLOCKIERT** – kein Bildverstehens-Modell in Ollama (nur Text-Modelle). Voraussetzung: KI-Bibliothek → „Bilder verstehen“ (z. B. Qwen 2.5 VL 7B) nach Zustimmung.
- Video echt (`Video_real_tools`, TRENDO_ECHT_VIDEO=1): SDXL Turbo malt 2 Schlüsselbilder, Piper spricht 2 Texte, FFmpeg rendert; ffprobe: h264 1280×720 + aac + mov_text, Dauer 7,5 s; Lautstärke −23,4 dB; Standbildunterschied Szene 1 (Wan-Clip) 7,3, Szene 2 (Schwenk) 34,3 → Bewegung. MP4-SHA-256 im Log.
- Wan 2.2 echt über „Mit Video-KI bewegen“: 1,5-s-Clip in 2141 s (36 min, Grafikspeicher geteilt); 4-s-Versuch nach 50 min abgebrochen (`roh/wan-laufzeit.md`). Das Abschluss-MP4 übernimmt diesen Clip nach Prüfsummen-Prüfung gegen den Herkunftsnachweis (TRENDO_ECHT_WAN_CLIP), statt Wan erneut 36 min rechnen zu lassen.
- Oberfläche im Bild: `vision-leer.png`, `vision-erkannt.png` (echte Erkennung mit Rahmen), `video-beispiel-ton.png`, `video-szenen.png` (Szene 1 KI-Video, Szene 2 Kamerafahrt mit Vorschau), `video-standbilder.jpg`.

## Tests
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core gezielt (VisionVideoT0699Tests, KreativKits3/4, ObjectDetect inkl. echtem YOLOX, VorbildAnalysen, NoFixedUser, GenVideo): 51 bestanden, 1 übersprungen, 0 Fehler.
- App: VisionVideoT0699_Ui 5 (4 grün, Video-Echtlauf ohne TRENDO_ECHT_VIDEO übersprungen; mit TRENDO_ECHT_VIDEO=1 einzeln grün), KreativStudios3_Ui 5/5, KreativStudios4_Ui 5/5, ProgramHeadDesign_Ui 6/6, ClaudePrograms_Ui 6/6, StudioUebergabe_Ui 4/4, UebersichtEinfach_Ui 2/2.
- Ersetzt blieb: in den schnellen UI-Tests die Aufrufe von YOLOX, Beschreibung, Stimme, Bild-KI, Wan und FFmpeg (Overrides); in den Echtläufen nur die Werkzeugsuche (Testmodus) – die Werkzeuge laufen echt. Kein Volltest (macht die Hauptsitzung).

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **VOLLWERTIG** (Ordner erkennen → Schlagworte/Rahmen/Seitendatei; Szenen → MP4 mit Ton).
2. Fremddatei importieren/exportieren, Export im Fremdprogramm geöffnet: **TEILWEISE** – echte Fotos (YOLOX-Demobild mit EXIF) und eigene Musik importiert; MP4 mit ffprobe/ffmpeg gelesen; XMP mit XML-Parser gelesen; nicht in digiKam/VLC geöffnet.
3. Prozessabbruch ≤ 30 s Verlust: **TEILWEISE** – Erkennungen stehen nach jedem Bild im Dokument (Programm-Hülle mit Wiederherstellung), Seitendateien atomar (`AtomicFile`); nicht eigens mit Abbruch getestet.
4. KI führt Fachaufgabe aus, Ergebnis im Dokument, rückgängig: **VOLLWERTIG** für Erkennung (YOLOX/Claude-Beschreibung landen als Schlagworte im Dokument, „Erkannt“ rückgängig machbar) und KI-Clip („KI-Video“ rückgängig); lokale Beschreibung **BLOCKIERT** (Modell fehlt).
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** (Vision_real_tools: Klick „Ordner erkennen“ → XMP/CSV; Video_real_tools: Planen/Malen/Musik/Bauen → MP4 mit ffprobe).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen und Ablegen: **TEILWEISE** – Stufen und Kürzel der Hülle bleiben (UebersichtEinfach grün); kein Rechtsklick-Menü, kein Ziehen eines Ordners auf die Galerie, kein Umsortieren der Szenen per Ziehen.

## Nicht geprüft
- Lokale Bildbeschreibung über Ollama (BLOCKIERT, kein Modell). Objekterkennung in der echten Trendo-Installation (KI-Laufzeit „Objekte“ ist dort nicht eingerichtet; geprüft mit der Prüfumgebung `%TEMP%/trendo-tools-b/yolox`, gleiches Skript und Modell).
- Wan-Dauer „etwa 5 bis 15 Minuten“ in der Meldung ist geschätzt; gemessen 36 min bei geteiltem Grafikspeicher.
- MP4 in einem fremden Player, XMP in digiKam.

## Offene Punkte
- Bildverstehens-Modell für lokale Beschreibung nach Zustimmung einrichten (KI-Bibliothek).
- Szenenplan aus einer Ein-Satz-Idee mit KI (heute Parser + „Szenen mit Claude verbessern“ im Chat), kie.ai-Clip als Szene übernehmen, Szenen umsortieren, Rechtsklick/Ziehen.
- Wan: kein Abbrechen-Knopf während der Erzeugung (Zeitgrenze 60 min).
- Menüband „Videos mit KI“: Gold bleibt auf „Szenen planen“, auch wenn danach „Video bauen“ der nächste Schritt wäre (Primary ist statisch in der Hülle).
- `tools/Trendo.Render`: neuer generischer Schalter `TRENDO_PROGDATA` (Dokumentfelder setzen) – außerhalb des engen Bereichs, nur für die Bildprüfung.

## Berührte Dateien
- neu: `src/Trendo.Core/Kreativ/VisionTags.cs`, `src/Trendo.App/MainWindow.VisionErkennen.cs`, `src/Trendo.App/MainWindow.VideoKiTon.cs`, `tests/Trendo.Core.Tests/VisionVideoT0699Tests.cs`, `tests/Trendo.App.Tests/VisionVideoT0699UiTests.cs`, `reports/T-0699/**`, `tasks/T-0699-vision-video.md`, `tasks/T-0699-vision-video.bericht.md`
- geändert: `src/Trendo.Core/Kreativ/KreativKits3.cs` (VideoAiKit), `src/Trendo.App/MainWindow.KreativStudios3.cs` (nur Videos mit KI), `src/Trendo.App/MainWindow.KreativStudios4.cs` (nur Bilder erkennen), `tools/Trendo.Render/Program.cs` (TRENDO_PROGDATA)
- nicht angefasst: `MainWindow.ProgramShell.cs`, Film-Studio, `SimpleWords.cs`

## Rust
Nicht eingesetzt – vorhandenen C#-Bestand erweitert; die Rechenlast liegt in vorhandenen Werkzeugen (YOLOX/onnxruntime, sd-cli, Piper, FFmpeg).
