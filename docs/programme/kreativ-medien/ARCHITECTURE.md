# Medien bearbeiten – Architektur

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `kreativ-medien` · Stand: 0.9.36 (Stand c50f512)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.AudioEditor.cs`
- `src/Trendo.App/MainWindow.Bildbearbeitung.cs`
- `src/Trendo.App/MainWindow.VideoSchnitt.cs`
- `src/Trendo.App/MainWindow.W4Media.cs`

## Bedienelemente der Arbeitsfläche
- Bildansicht
- Medien-Werkbank (Dateien, Werkzeuge, Ergebnis)

## Fach-Engine (Trendo.Core)
- `VideoEdit`
- `ImageDoc`
- `AudioEdit`
- `MediaStudio`
- `AudioFile`
- `FileShelf`
- `Colors`
- `EditMarker`
- `Progress`
- `Rect`

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
- VLC (optional)
- mpv (optional)

## Bausteine (Masterprompt 131 / 5)
Eigene Arbeitsseite ohne die gemeinsame Programm-Hülle – Dokumentmodell, Rückgängig und Bedienstufen sind hier nicht einheitlich.
