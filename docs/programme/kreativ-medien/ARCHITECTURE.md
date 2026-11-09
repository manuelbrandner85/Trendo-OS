# Medien bearbeiten – Architektur

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `kreativ-medien` · Stand: 0.9.36 (Stand 20e850a)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.AudioEditor.cs`
- `src/Trendo.App/MainWindow.Bildbearbeitung.cs`
- `src/Trendo.App/MainWindow.VideoSchnitt.cs`
- `src/Trendo.App/MainWindow.W4Media.cs`

## Bedienelemente der Arbeitsfläche
- Bildansicht
- Medien-Werkbank (Dateien, Werkzeuge, Ergebnis)

## Fach-Engine (Trendo.Core)
- `ImageDoc`
- `VideoEdit`
- `AudioEdit`
- `MediaStudio`
- `VideoTools`
- `VideoLanes`
- `AudioFile`
- `AudioMulti`
- `VideoPro`
- `ImageOps`

## Dateiformate (im Code belegt)
- .jpg
- .mp3
- .mp4
- .ogg
- .png
- .svg
- .txt
- .wav
- .webm

## Externe Programme und Dienste
- FFmpeg
- Krita (optional)
- Inkscape (optional)
- GIMP (optional)
- mpv (optional)

## Bausteine (Masterprompt 131 / 5)
Eigene Arbeitsseite ohne die gemeinsame Programm-Hülle – Dokumentmodell, Rückgängig und Bedienstufen sind hier nicht einheitlich.
