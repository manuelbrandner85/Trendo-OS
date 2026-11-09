# Technische Spezifikation: Surfen & Herunterladen

Studio SYS-03 · Surfen & Herunterladen · Bereich System & Sicherheit

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

Im Internet surfen und Dateien laden.

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-0.9.3/README.md
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **Downloads** (`downloads`): Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11)
- **Sicher prüfen** (`netz-pruefen`): Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11)

## Quellcode

- `src/Trendo.App/MainWindow.Sys03.cs`

## Externe Programme

- Blender (optional, 3D-Fotos/Render)
- FFmpeg
- LibreOffice (optional)
- Ollama – lokale KI (optional)
- VLC (optional)
- mpv (optional)

## Dateiformate

`.csv`, `.docx`, `.html`, `.jpg`, `.json`, `.md`, `.mp3`, `.mp4`, `.ods`, `.odt`, `.ogg`, `.pdf`, `.png`, `.pptx`, `.svg`, `.txt`, `.wav`, `.webm`, `.xlsx`, `.zip`

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

Recherche und Bewertung: erledigt (Runde 10, 07./08.11.2026). Umsetzung: geplant, NOT RUN.
