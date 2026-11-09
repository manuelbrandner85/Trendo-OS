# Technische Spezifikation: Kleine Helfer & Musik

Studio SYS-02 · Kleine Helfer & Musik · Bereich Arbeiten & Büro

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

Kleine Helfer: Rechner, Notizen, Bildschirmfoto, Musik und Videos abspielen.

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-0.9.3/README.md
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **Medien** (`medien`): Fotogalerie mit Betrachter
- **Übersicht** (`alltag-start`): Programm-Startseite (Kacheln der kleinen Helfer)
- **Werkzeuge: Notizen, Rechner, Wecker** (`werkzeuge`): Helfer-Arbeitsfläche (Haftnotizen, Rechner mit Tastenfeld, Wecker)

## Quellcode

- `src/Trendo.App/MainWindow.Helfer.cs`
- `src/Trendo.App/MainWindow.Sys02.cs`

## Externe Programme

- Blender (optional, 3D-Fotos/Render)
- FFmpeg
- LibreOffice (optional)
- Ollama – lokale KI (optional)
- VLC (optional)
- mpv (optional)

## Dateiformate

`.csv`, `.docx`, `.ics`, `.jpg`, `.json`, `.md`, `.mp3`, `.mp4`, `.ods`, `.odt`, `.ogg`, `.pdf`, `.png`, `.pptx`, `.txt`, `.wav`, `.webm`, `.xlsx`, `.zip`

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

Recherche und Bewertung: erledigt (Runde 9, 07./08.11.2026). Umsetzung: geplant, NOT RUN.
