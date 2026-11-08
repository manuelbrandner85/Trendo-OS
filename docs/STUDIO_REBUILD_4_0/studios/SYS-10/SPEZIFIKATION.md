# Technische Spezifikation: Texte & Tabellen

Studio SYS-10 · Texte & Tabellen · Bereich Arbeiten & Büro

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

Texte, Tabellen und Präsentationen erstellen und bearbeiten.

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-0.9.3/README.md
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **Start** (`doku-start`): Office-Startseite (Programme, Vorlagen, Zuletzt benutzt – wie Microsoft 365)
- **Trendo PDF** (`pdf`): PDF-Startseite (Zuletzt geöffnet, Öffnen, Werkzeuge)
- **Trendo Präsentation** (`praesentation`): Folienansicht
- **Trendo Tabelle** (`tabelle`): Tabellenraster
- **Trendo Text** (`text`): Dokumentseite (Textverarbeitung)

## Quellcode

- `src/Trendo.App/MainWindow.Pdf.cs`
- `src/Trendo.App/MainWindow.Praesentation.cs`
- `src/Trendo.App/MainWindow.Sys10.cs`
- `src/Trendo.App/MainWindow.Tabelle.cs`
- `src/Trendo.App/MainWindow.Text.cs`

## Externe Programme

- FFmpeg
- Hunspell-Wörterbücher
- LibreOffice (optional)
- PDFsharp (MIT)
- Tesseract OCR
- qpdf

## Dateiformate

`.csv`, `.docx`, `.ics`, `.jpg`, `.json`, `.md`, `.mp3`, `.mp4`, `.ods`, `.odt`, `.ogg`, `.pdf`, `.png`, `.pptx`, `.txt`, `.wav`, `.webm`, `.xlsx`, `.xml`, `.zip`

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

Recherche und Bewertung: erledigt (Runde 2, 07./08.11.2026). Umsetzung: geplant, NOT RUN.
