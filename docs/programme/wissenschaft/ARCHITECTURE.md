# Messdaten & Auswertung – Architektur

Studio: Messwerte auswerten (SPEC-05) · Bereich: Wissen & Lernen · Seite: `wissenschaft` · Stand: 0.9.36 (Stand 5b38700)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.Spec05.cs`

## Bedienelemente der Arbeitsfläche
- Diagramm/Zeichnung
- Tabellenraster
- Menüband
- Eigenschaften
- Statusleiste
- Dokument-Reiter

## Fach-Engine (Trendo.Core)
- `MeasureSheet`
- `StatsPlus`
- `Stats`
- `ChartDrawing`
- `Fitting`

## Dateiformate (im Code belegt)
- .csv
- .docx
- .ics
- .json
- .md
- .odt
- .pdf
- .png
- .svg
- .tdoc
- .txt
- .xml

## Externe Programme und Dienste
- LibreOffice (optional)
- qpdf
- Tesseract OCR
- PDFsharp (MIT)
- Hunspell-Wörterbücher

## Bausteine (Masterprompt 131 / 5)
A Programmfenster mit Menüband und Kürzeln · B Arbeitsfläche · C Werkzeuge ändern das Dokument · D .tdoc-Projektdatei mit Autosave/Wiederherstellen · E Rückgängig/Wiederholen und Verlauf · I Prüfungen · J Startmenü, Suche, Ordner, Bedienstufen
