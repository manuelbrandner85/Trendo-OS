# Texte zusammenfassen – Architektur

Studio: Texte zusammenfassen (STUDIO-SUMMARY-01) · Bereich: KI & Automatik · Seite: `zusammenfassen-programm` · Stand: 0.9.110 (Stand 0eb9d52)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.KreativStudios.cs`

## Bedienelemente der Arbeitsfläche
- Texteditor
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `LogoKit`
- `VectorKit`
- `SummaryKit`

## Dateiformate (im Code belegt)
- .md
- .tdoc
- .txt

## Externe Programme und Dienste
- FFmpeg
- Inkscape (optional)
- PDFsharp (MIT)

## Bausteine (Masterprompt 131 / 5)
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
