# Technische Spezifikation: Programme starten

Studio SYS-11 · Programme starten · Bereich System & Sicherheit

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

Programme ohne Installation starten und verknüpfen.

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-0.9.3/README.md
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **Apps** (`apps`): Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11)
- **Hinzufügen** (`hinzufuegen`): Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11)
- **Pflegen** (`prog-pflege`): Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11)
- **Zusatzprogramme** (`components`): Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11)

## Quellcode

- `src/Trendo.App/MainWindow.Sys11.cs`

## Externe Programme

- Blender (optional, 3D-Fotos/Render)
- FFmpeg
- Git
- Hunspell-Wörterbücher
- Inkscape (optional)
- Krita (optional)
- LibreOffice (optional)
- OBS Studio (optional)
- Ollama – lokale KI (optional)
- PDFsharp (MIT)
- Python (optional)
- Tesseract OCR
- Unreal Engine (optional)
- VLC (optional)
- mpv (optional)
- qpdf

## Dateiformate

`.csv`, `.docx`, `.html`, `.jpg`, `.json`, `.mp3`, `.mp4`, `.odt`, `.pdf`, `.png`, `.txt`, `.xlsx`, `.zip`

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

Recherche und Bewertung: erledigt (Runde 14, 07./08.11.2026). Umsetzung: geplant, NOT RUN.
