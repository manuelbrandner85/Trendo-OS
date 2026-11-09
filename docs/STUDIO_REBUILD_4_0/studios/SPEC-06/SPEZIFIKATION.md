# Technische Spezifikation: Lernen mit Karteikarten

Studio SPEC-06 · Lernen mit Karteikarten · Bereich Wissen & Lernen

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

Karteikarten, Vokabeln und Prüfungen üben.

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-0.9.2/tests-pc-4e6b81d.txt
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **Lernen mit Karteikarten** (`lernen`): Diagramm/Zeichnung, Lernkarte

## Quellcode

- `src/Trendo.App/MainWindow.Spec06.cs`

## Externe Programme

- Blender (optional, 3D-Fotos/Render)
- PDFsharp (MIT)

## Dateiformate

`.csv`

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

Recherche und Bewertung: erledigt (Runde 4, 08.11.2026).

Umsetzung:
- FSRS-Planer (FSRS-5, eigener C#-Code nach py-fsrs, MIT): eingebaut, im Lernplan umschaltbar (SM-2 bleibt Standard), Behaltensrate 80–95 %. Tests: 12 Kern (FsrsTests), 1 Oberfläche (FsrsUi), grün.
- Optimierer (fsrs-rs, Rust) und .apkg-Import: geplant, NOT RUN.
