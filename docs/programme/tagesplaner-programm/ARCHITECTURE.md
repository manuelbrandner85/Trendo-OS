# Tagesplaner – Architektur

Studio: Tagesplaner (STUDIO-DAYPLAN-01) · Bereich: KI & Automatik · Seite: `tagesplaner-programm` · Stand: 0.9.113 (Stand 1978f1a)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.KreativStudios4.cs`

## Bedienelemente der Arbeitsfläche
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `CiteKit`
- `CodeAiKit`
- `DayPlanner`
- `VisionKit`
- `KreativSafe`
- `ClaudeCode`
- `CodeProject`
- `FolderSnapshot`
- `VisionTags`
- `RealProcessRunner`

## Dateiformate (im Code belegt)
- .ics
- .tdoc
- .txt

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
