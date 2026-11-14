# Tagesplaner – Architektur

Studio: Tagesplaner (STUDIO-DAYPLAN-01) · Bereich: Arbeiten & Büro · Seite: `tagesplaner-programm` · Stand: 0.9.95 (Stand cce5c62)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.KreativStudios4.cs`

## Bedienelemente der Arbeitsfläche
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `CiteKit`
- `DayPlanner`
- `CodeAiKit`
- `VisionKit`
- `KreativSafe`
- `CodeProject`
- `RealProcessRunner`

## Dateiformate (im Code belegt)
- .csv
- .html
- .ics
- .jpg
- .json
- .md
- .mp4
- .ogg
- .png
- .svg
- .tdoc
- .txt
- .wav
- .xml
- .zip

## Externe Programme und Dienste
- Blender (optional, 3D-Fotos/Render)
- Unreal Engine (optional)
- Ollama – lokale KI (optional)
- FFmpeg
- LibreOffice (optional)
- qpdf
- Krita (optional)
- Inkscape (optional)
- Tesseract OCR
- PDFsharp (MIT)
- Hunspell-Wörterbücher
- Git
- OBS Studio (optional)
- mpv (optional)
- Python (optional)
- Node.js (optional)

## Bausteine (Masterprompt 131 / 5)
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
