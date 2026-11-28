# Podcast aufnehmen – Architektur

Studio: Podcast aufnehmen (STUDIO-PODCAST-01) · Bereich: Kreativ & Medien · Seite: `podcast-programm` · Stand: 0.9.114 (Stand 99cbb5c)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.PodcastStudio.cs`

## Bedienelemente der Arbeitsfläche
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `PodcastEdit`
- `PodcastKit`
- `AudioFile`
- `AudioEdit`
- `KreativSafe`

## Dateiformate (im Code belegt)
- .jpg
- .md
- .mp3
- .mp4
- .ogg
- .png
- .tdoc
- .txt
- .wav
- .xml

## Externe Programme und Dienste
- FFmpeg

## Bausteine (Masterprompt 131 / 5)
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
