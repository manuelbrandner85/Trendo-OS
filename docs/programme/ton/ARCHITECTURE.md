# Audio bearbeiten – Architektur

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `ton` · Stand: 0.9.36 (Stand df96c57)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.AudioEditor.cs`
- `src/Trendo.App/MainWindow.Bildbearbeitung.cs`
- `src/Trendo.App/MainWindow.VideoSchnitt.cs`
- `src/Trendo.App/MainWindow.W4Media.cs`

## Bedienelemente der Arbeitsfläche
- Wellenform-Editor (Auswahl, Schnitt, Effekte – wie Audacity)
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

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
- .flac
- .jpg
- .mp3
- .mp4
- .ogg
- .png
- .svg
- .tdoc
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
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
