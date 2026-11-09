# Technische Spezifikation: Gegenstände einscannen

Studio SPEC-09 · Gegenstände einscannen · Bereich 3D, Spiele & VR

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

Gegenstände und Räume einscannen und vermessen.

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-t0003/UEBERSICHT.md
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **Aus Fotos** (`scan3d`): Fotoscan (Fotos, Rechnung, 3D-Ergebnis – wie Meshroom)
- **Punktwolke** (`punktwolke`): Punktwolken-Ansicht

## Quellcode

- `src/Trendo.App/MainWindow.Spec09.cs`

## Externe Programme

- keine zugeordnet

## Dateiformate

`.csv`, `.md`, `.obj`, `.ply`, `.pts`, `.tdoc`, `.txt`, `.xyz`

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

Recherche und Bewertung: erledigt (Runde 2, 07./08.11.2026). Umsetzung: geplant, NOT RUN.
