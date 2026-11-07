# Videoschnitt – Architektur

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `schnitt` · Stand: 0.9.36 (Stand 63090ad)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.AudioEditor.cs`
- `src/Trendo.App/MainWindow.Bildbearbeitung.cs`
- `src/Trendo.App/MainWindow.VideoSchnitt.cs`
- `src/Trendo.App/MainWindow.W4Media.cs`

## Bedienelemente der Arbeitsfläche
- Texteditor
- Video-Zeitleiste
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `ImageDoc`
- `VideoEdit`
- `AudioEdit`
- `MediaStudio`
- `AudioMulti`
- `AudioFile`
- `ImageOps`
- `VideoExport`
- `Rect`
- `FileShelf`

## Dateiformate (im Code belegt)
- .csv
- .docx
- .jpg
- .json
- .md
- .mp3
- .mp4
- .ods
- .odt
- .ogg
- .pdf
- .png
- .pptx
- .svg
- .tdoc
- .txt
- .wav
- .webm
- .xlsx
- .zip

## Externe Programme und Dienste
- Blender (optional, 3D-Fotos/Render)
- Ollama – lokale KI (optional)
- FFmpeg
- LibreOffice (optional)
- Krita (optional)
- Inkscape (optional)
- GIMP (optional)
- mpv (optional)

## Bausteine (Masterprompt 131 / 5)
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
