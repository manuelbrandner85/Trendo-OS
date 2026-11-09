# Technische Spezifikation: Kostenlose KI finden

Studio X-05 · Kostenlose KI finden · Bereich KI & Automatik

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

Kostenlose KI-Modelle finden und als Ersatz nutzen.

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-t0003/UEBERSICHT.md
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **KI-Bibliothek** (`ki-bibliothek`): KI-Zentrale (Bereiche, Suche, Seite – wie LM Studio)
- **KI-Quellen** (`ki-quellen`): KI-Zentrale (Bereiche, Suche, Seite – wie LM Studio)
- **Kostenlose Internet-KIs** (`ki-internet`): KI-Zentrale (Bereiche, Suche, Seite – wie LM Studio)

## Quellcode

- `src/Trendo.App/MainWindow.KiLibrary.cs`

## Externe Programme

- Blender (optional, 3D-Fotos/Render)
- FFmpeg
- Ollama – lokale KI (optional)
- Piper (Vorlesen)
- Python (optional)
- whisper (Spracherkennung)

## Dateiformate

`.glb`, `.jpg`, `.json`, `.mp3`, `.mp4`, `.ogg`, `.png`, `.txt`, `.wav`, `.webm`, `.zip`

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

Recherche und Bewertung: erledigt (Runde 12, 07./08.11.2026). Umsetzung: geplant, NOT RUN.
