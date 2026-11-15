# Karten & Einladungen – Architektur

Studio: Karten & Einladungen (STUDIO-CARDS-01) · Bereich: Arbeiten & Büro · Seite: `karten-programm` · Stand: 0.9.103 (Stand ef4dc8b)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.Karten.cs`

## Bedienelemente der Arbeitsfläche
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `KartenKit`
- `Gaeste`
- `KartenPdf`
- `KartenVorlagen`
- `Karte`

## Dateiformate (im Code belegt)
- .csv
- .jpg
- .pdf
- .png
- .tdoc
- .txt

## Externe Programme und Dienste
- PDFsharp (MIT)

## Bausteine (Masterprompt 131 / 5)
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
