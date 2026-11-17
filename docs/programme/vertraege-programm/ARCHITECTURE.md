# Verträge-Ordner – Architektur

Studio: Verträge-Ordner (STUDIO-CONTRACTS-01) · Bereich: Arbeiten & Büro · Seite: `vertraege-programm` · Stand: 0.9.103 (Stand 0262de0)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.Vertraege.cs`

## Bedienelemente der Arbeitsfläche
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `Vertraege`
- `LetterPdf`
- `VertragsOrdner`

## Dateiformate (im Code belegt)
- .csv
- .ics
- .jpg
- .pdf
- .png
- .tdoc

## Externe Programme und Dienste
- LibreOffice (optional)
- PDFsharp (MIT)

## Bausteine (Masterprompt 131 / 5)
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
