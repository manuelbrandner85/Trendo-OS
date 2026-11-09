# Trendo PDF – Architektur

Studio: Texte & Tabellen (SYS-10) · Bereich: Arbeiten & Büro · Seite: `pdf` · Stand: 0.9.36 (Stand 20e850a)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.Pdf.cs`
- `src/Trendo.App/MainWindow.Sys10.cs`

## Bedienelemente der Arbeitsfläche
- PDF-Startseite (Zuletzt geöffnet, Öffnen, Werkzeuge)

## Fach-Engine (Trendo.Core)
- `PdfTools`
- `Ooxml`
- `Writing`
- `DocVersions`
- `PdfForm`
- `PdfText`
- `Speller`
- `TaskList`
- `TaskItem`

## Dateiformate (im Code belegt)
- .csv
- .docx
- .ics
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
