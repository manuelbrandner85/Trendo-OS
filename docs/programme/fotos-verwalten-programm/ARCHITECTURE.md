# Fotos verwalten – Architektur

Studio: Fotos verwalten (STUDIO-PHOTOS-01) · Bereich: Kreativ & Medien · Seite: `fotos-verwalten-programm` · Stand: 0.9.113 (Stand 1978f1a)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.KreativStudios2.cs`

## Bedienelemente der Arbeitsfläche
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `AnimKit`
- `MotionKit`
- `ImageAiKit`
- `BookKit`
- `KreativSafe`
- `VectorKit`
- `PhotoOrganizer`
- `LogoKit`
- `KiLibrary`
- `KiRuntime`

## Dateiformate (im Code belegt)
- .tdoc
- .txt

## Externe Programme und Dienste
- Blender (optional, 3D-Fotos/Render)
- Ollama – lokale KI (optional)
- FFmpeg
- Inkscape (optional)
- Piper (Vorlesen)
- whisper (Spracherkennung)
- PDFsharp (MIT)
- Python (optional)

## Bausteine (Masterprompt 131 / 5)
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
