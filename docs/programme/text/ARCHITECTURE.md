# Trendo Text – Architektur

Studio: Texte & Tabellen (SYS-10) · Bereich: Arbeiten & Büro · Seite: `text` · Stand: 0.9.36 (Stand 6da70dc)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.Sys10.cs`
- `src/Trendo.App/MainWindow.Text.cs`

## Bedienelemente der Arbeitsfläche
- Dokumentseite (Textverarbeitung)

## Fach-Engine (Trendo.Core)
- `TextDoc`
- `ParaStyle`
- `MarkKind`
- `ListKind`
- `Ooxml`
- `TextTools`
- `Align`
- `Spelling`
- `Writing`
- `DocVersions`

## Dateiformate (im Code belegt)
- .csv
- .docx
- .ics
- .jpg
- .json
- .md
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
