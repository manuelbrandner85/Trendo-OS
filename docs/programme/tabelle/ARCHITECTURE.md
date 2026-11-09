# Trendo Tabelle – Architektur

Studio: Texte & Tabellen (SYS-10) · Bereich: Arbeiten & Büro · Seite: `tabelle` · Stand: 0.9.36 (Stand df96c57)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.Sys10.cs`
- `src/Trendo.App/MainWindow.Tabelle.cs`

## Bedienelemente der Arbeitsfläche
- Tabellenraster

## Fach-Engine (Trendo.Core)
- `Addr`
- `SheetExtras`
- `NumFmt`
- `CondOp`
- `Ooxml`
- `CondKind`
- `ChartKind`
- `Writing`
- `DocVersions`
- `SheetFiles`

## Dateiformate (im Code belegt)
- .csv
- .docx
- .ics
- .json
- .md
- .ods
- .odt
- .pdf
- .png
- .txt
- .xlsx
- .xml
- .zip

## Externe Programme und Dienste
- LibreOffice (optional)
- qpdf
- Tesseract OCR
- PDFsharp (MIT)
- Hunspell-Wörterbücher

## Bausteine (Masterprompt 131 / 5)
Eigene Arbeitsseite ohne die gemeinsame Programm-Hülle – Dokumentmodell, Rückgängig und Bedienstufen sind hier nicht einheitlich.
