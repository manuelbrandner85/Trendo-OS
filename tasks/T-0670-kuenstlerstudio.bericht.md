# Bericht T-0670 Künstlerstudio (K1–K8)

- Auftrag: `tasks/T-0670-kuenstlerstudio.md` (Freigabe 15.11.2026, K1–K8 alle Ja)
- Zweig: `claude/T-0670-kuenstlerstudio`, Basis `d36a5a3f1` (0.9.101)
- Version: 0.9.102 (`installer/neuerungen/0.9.102.md`)

## Geänderte und neue Dateien
- Kern: `src/Trendo.Core/Kreativ/Kuenstler.cs` (Pixmap, PngCodec, PaintBlend, BrushPreset, PaintSymmetry, DrawAssist, Smoother, StrokeEngine, PaintColors, Palettes, PaintDoc, PaintFiles ORA/PSD), `src/Trendo.Core/Kreativ/Kuenstler2.cs` (Mannequin, Timelapse, Sketch, PrintFile, Portfolio, Courses)
- KI-Adapter: `src/Trendo.Core/PromptAction/KunstPromptAdapter.cs` (`kunst.vorlage`), angemeldet in `IntelligencePromptModel.cs`
- Oberfläche: `src/Trendo.App/MainWindow.Kuenstlerstudio.cs`, `src/Trendo.App/ArtControls.cs` (ArtState, PaintCanvas mit Stiftdruck, HsvWheel)
- Anmeldung: StudioCatalog, StudioIcons, PromptChips, CapabilityCatalog, StudioKnowledge, StudioProfiles, StudioTeams, `requirements.yaml`, `MainWindow.W6.cs`
- Studiobild `src/Trendo.App/Assets/studios/STUDIO-ART-01.jpg` lokal mit ComfyUI + FLUX.2 klein (`tools/studio_bilder.py`)
- Doku: `docs/nachbau/kuenstlerstudio/anforderungen.md`, `docs/claude/vorbilder/kunst-krita-procreate.md`, `docs/programme/kuenstler-programm/`, Katalog und Fähigkeitsmatrix neu erzeugt
- Tests: `tests/Trendo.Core.Tests/KuenstlerTests.cs` (25), `tests/Trendo.App.Tests/KuenstlerstudioUiTests.cs` (6); Zählungen angepasst (76 Studios, 103 Adapter, 73 teilweise, 54 in der Übersicht); `einfach-vorher-ausgeblendet.txt` um die in „Einfach“ eingeklappten Eigenschaften ergänzt
- Nebenbei: Vorbild-Analyse `web-vscode-webflow.md` und Testliste auf den neuen Programmnamen „Webseite bauen“ (vorher „Software“, umbenannt in T-0663)
- `tools/Trendo.Render/Program.cs`: `TRENDO_ART_DEMO` für Bildvergleiche

## Anforderungen – Ergebnis
| ID | Funktion | Stand | Nachweis |
|---|---|---|---|
| K1-01 | Pinselstrich lückenlos, weiche Kante | erfüllt | KuenstlerTests.Stroke_continuous_and_soft |
| K1-02 | Stiftdruck, Aufbau je Pinsel | erfüllt | Pressure_and_buildup |
| K1-03 | Pinselsatz, Radierer, Deckkraft sperren | erfüllt | Brush_set |
| K1-04 | Ebenen | erfüllt | Layers, UI Layers_and_files |
| K1-05 | Mischarten | erfüllt | Blend_modes (9 Fälle) |
| K1-06 | Rückgängig | erfüllt | Undo_via_document, UI Paint_with_mouse_and_undo |
| K1-07 | PNG/ORA/PSD | erfüllt | Files_roundtrip, UI Layers_and_files |
| K2-01 | Farbrad, Hex, letzte Farben | erfüllt | UI Colors_and_assists |
| K2-02 | Paletten .gpl | erfüllt | Palettes_and_harmonies |
| K2-03 | Farbe aus Foto | erfüllt | Colors_from_photo, UI Reference_puppet_sketch |
| K2-04 | Harmonien | erfüllt | Palettes_and_harmonies |
| K3-01 | Spiegeln | erfüllt | Symmetry, UI Colors_and_assists |
| K3-02 | Perspektive 1/2/3 | erfüllt | Assist_snap, UI (Fluchtpunkt per Klick) |
| K3-03 | Raster, Lineal, gerade Linie | erfüllt | Assist_snap |
| K3-04 | Glätten | erfüllt | Smoothing |
| K4-01 | Referenzbild mit Farbaufnahme | erfüllt | UI Reference_puppet_sketch |
| K4-02 | Posenpuppe | teilweise | Mannequin_pose: eigene 3D-Gliederpuppe; eine Puppe aus dem 3D-Studio gibt es dort nicht |
| K4-03 | Ideenskizze lokal, nur Referenz | erfüllt (mit Ersatz-KI geprüft) | UI Reference_puppet_sketch; echte Bild-KI nicht im Test |
| K5-01 | Zeitraffer → Video → Videoschnitt | erfüllt (FFmpeg im Test ersetzt) | Timelapse_frames, UI Timelapse_share_course |
| K6-01 | Skizze säubern | erfüllt | Sketch_cleanup_and_flats |
| K6-02 | Vorfärben | erfüllt | Sketch_cleanup_and_flats, UI |
| K7-01 | Portfolio-Webseite | erfüllt | Portfolio_and_print, UI |
| K7-02 | Druckdatei mit Beschnitt | erfüllt | Portfolio_and_print (A4 300 dpi + 3 mm = 2551 × 3579) |
| K7-03 | Print on Demand | erfüllt | UI Timelapse_share_course |
| K8-01 | Malkurse | erfüllt | Courses_progress, UI (Kurs „Erste Schritte“ bis „geschafft“) |

„Erfüllt“ heißt: Die genannten Prüfungen bestehen. Es heißt nicht, dass das Programm identisch mit Krita oder Procreate ist.

## Kommandos und Testergebnis
- `dotnet test Trendo.sln -c Release` auf `0ff66ff50`:
  - Api: 16/16 bestanden
  - Core: 2590/2597 bestanden; 2 Fehler, 5 übersprungen
    - Vorbild-Programmname „Software“ statt „Webseite bauen“
    - QA-015-Zählung 75 statt 76
  - App: 1335/1341 bestanden; 6 übersprungen, 0 Fehler
  - Exitcode 1 wegen der beiden Core-Fehler
- `python -m unittest discover -s tests/claude_handoff`: 11 Tests OK, Exitcode 0
- Nach der Korrektur (nur Test und Doku): Core in drei Läufen
  - Lauf 1: 2591/2597 bestanden, 1 Fehler. Der wackelnde Test ließ sich in den nächsten beiden Läufen nicht wiederholen.
  - Läufe 2 und 3: je 2592 bestanden, 0 Fehler
- Belege: `reports/volltest-T-0670/` mit SHA256SUMS
- Sichtprüfung: Render der Seite (Mandala-Vorlage mit Tusche und Aquarell, Spiegelachsen sichtbar)

## Nicht geprüft
- Echtes Grafiktablett: Stiftdruck nur im Kern geprüft, die Oberfläche wurde nur mit der Maus getestet
- Echte Bild-KI (sd-cli) und echtes FFmpeg im Künstlerstudio: im Test ersetzt; beide Wege nutzt die App schon in anderen Studios
- Sehr große Leinwände (über 4000 px): Geschwindigkeit nicht gemessen

## Offene Punkte
- Leinwand drehen
- Füllwerkzeug
- Auswahl und Transformieren
- eigene Pinselspitzen
- Nass-in-Nass-Farbmischung

Siehe die Vorbild-Analyse.

## Rust-Prüfung
Kein Rust: Der Mal-Kern ist rechenleicht genug in C#, nur der geänderte Bereich wird neu berechnet. Eine eigene Sprache wäre eine unnötige zweite Toolchain im Mal-Pfad. Die Messung vom 08.11.2026 ergab keinen Rust-Bedarf.

Status: fertig, Volltest grün nach Korrektur, bereit für main und Release 0.9.102.
