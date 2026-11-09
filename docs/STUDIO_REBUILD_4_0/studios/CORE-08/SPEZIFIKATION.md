# Technische Spezifikation: Videos & Fotos bearbeiten

Studio CORE-08 · Videos & Fotos bearbeiten · Bereich Kreativ & Medien

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

Videos schneiden, Musik aufnehmen und Bilder gestalten.

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-0.9.2/tests-pc-4e6b81d.txt
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **Audio bearbeiten** (`ton`): Wellenform-Editor (Auswahl, Schnitt, Effekte – wie Audacity)
- **Bildbearbeitung** (`bild`): Bild-Arbeitsfläche (Ebenen)
- **Medien bearbeiten** (`kreativ-medien`): Bildansicht, Medien-Werkbank (Dateien, Werkzeuge, Ergebnis)
- **Videoschnitt** (`schnitt`): Texteditor, Video-Zeitleiste

## Quellcode

- `src/Trendo.App/MainWindow.AudioEditor.cs`
- `src/Trendo.App/MainWindow.Bildbearbeitung.cs`
- `src/Trendo.App/MainWindow.VideoSchnitt.cs`
- `src/Trendo.App/MainWindow.W4Media.cs`

## Externe Programme

- Blender (optional, 3D-Fotos/Render)
- FFmpeg
- GIMP (optional)
- Inkscape (optional)
- Krita (optional)
- LibreOffice (optional)
- Ollama – lokale KI (optional)
- mpv (optional)

## Dateiformate

`.csv`, `.docx`, `.flac`, `.jpg`, `.json`, `.md`, `.mp3`, `.mp4`, `.ods`, `.odt`, `.ogg`, `.pdf`, `.png`, `.pptx`, `.svg`, `.tdoc`, `.txt`, `.wav`, `.webm`, `.xlsx`, `.zip`

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

Recherche und Bewertung: erledigt (Runde 1, 07.11.2026). Umsetzung: geplant, NOT RUN.
