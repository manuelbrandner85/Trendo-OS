# Technische Spezifikation: KI fragen

Studio CORE-02 · KI fragen · Bereich KI & Automatik

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

Dein Helfer, der Fragen beantwortet und Aufgaben erledigt.

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-t0003/UEBERSICHT.md
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **KI** (`ki`): KI-Zentrale (Bereiche, Suche, Seite – wie LM Studio)
- **KI fragen** (`ki-fragen`): KI-Zentrale (Bereiche, Suche, Seite – wie LM Studio)
- **Mit Claude verbinden** (`claude-verbinden`): KI-Zentrale (Bereiche, Suche, Seite – wie LM Studio)
- **Online-Dienste** (`keys`): KI-Zentrale (Bereiche, Suche, Seite – wie LM Studio)
- **Verbindungen** (`verbindungen`): KI-Zentrale (Bereiche, Suche, Seite – wie LM Studio)

## Quellcode

- `src/Trendo.App/MainWindow.Connections.cs`
- `src/Trendo.App/MainWindow.Skills.cs`

## Externe Programme

- Blender (optional, 3D-Fotos/Render)
- Docker (optional)
- FFmpeg
- Git
- Hunspell-Wörterbücher
- Inkscape (optional)
- Krita (optional)
- LibreOffice (optional)
- Node.js (optional)
- OBS Studio (optional)
- Ollama – lokale KI (optional)
- PDFsharp (MIT)
- Python (optional)
- Tesseract OCR
- Unreal Engine (optional)
- mpv (optional)
- qpdf

## Dateiformate

`.csv`, `.gltf`, `.html`, `.jpg`, `.json`, `.md`, `.obj`, `.png`, `.svg`, `.tdoc`, `.txt`, `.xml`, `.zip`

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

Recherche und Bewertung: erledigt (Runde 12, 07./08.11.2026). Umsetzung: geplant, NOT RUN.
