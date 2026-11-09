# Filme machen – Architektur

Studio: Filme machen (STUDIO-FILM-01) · Bereich: Kreativ & Medien · Seite: `film-programm` · Stand: 0.9.36 (Stand df96c57)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.FilmStudio.cs`

## Bedienelemente der Arbeitsfläche
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `FountainKind`
- `Fountain`
- `FilmBreakdowns`
- `ShotPlan`
- `TypstDoc`

## Dateiformate (im Code belegt)
- .csv
- .fountain
- .html
- .m3u8
- .md
- .pdf
- .png
- .svg
- .tdoc

## Externe Programme und Dienste
- FFmpeg
- OBS Studio (optional)

## Bausteine (Masterprompt 131 / 5)
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
