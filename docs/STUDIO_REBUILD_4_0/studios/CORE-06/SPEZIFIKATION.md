# Technische Spezifikation: Spiele bauen

Studio CORE-06 · Spiele bauen · Bereich 3D, Spiele & VR

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

Eigene Spiele bauen – auch mit der Unreal Engine.

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-0.9.2/tests-pc-4e6b81d.txt
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **Spiele bauen** (`spiele-programm`): keine eigene Arbeitsfläche erkannt
- **Werkstatt** (`spiele`): Bildansicht, Spielesammlung (Bibliothek, gewähltes Spiel mit Einstellungen, Neu und Unreal)

## Quellcode

- `src/Trendo.App/MainWindow.ProgGame.cs`
- `src/Trendo.App/MainWindow.W4Game.cs`

## Externe Programme

- Blender (optional, 3D-Fotos/Render)
- Unreal Engine (optional)

## Dateiformate

`.html`, `.jpg`, `.json`, `.md`, `.mp3`, `.ogg`, `.png`, `.tdoc`, `.txt`, `.wav`, `.zip`

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

Recherche und Bewertung: erledigt (Runde 7, 07./08.11.2026). Umsetzung: geplant, NOT RUN.
