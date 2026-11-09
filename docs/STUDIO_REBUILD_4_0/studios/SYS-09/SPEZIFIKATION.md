# Technische Spezifikation: E-Mails & Termine

Studio SYS-09 · E-Mails & Termine · Bereich Arbeiten & Büro

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

E-Mails, Kontakte und Termine an einem Ort.

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-t0003/UEBERSICHT.md
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **Adressbuch** (`kontakte`): Adressbuch (Gruppen, Liste, Kontaktkarte)
- **Aufgaben** (`aufgaben`): Aufgabentafel
- **E-Mail & Kontakte** (`post`): Texteditor
- **Kalender** (`kalender`): Monatskalender
- **Posteingang** (`posteingang`): Posteingang (Ordner, Liste, Lesebereich)

## Quellcode

- `src/Trendo.App/MainWindow.Pim.cs`

## Externe Programme

- FFmpeg
- VLC (optional)
- mpv (optional)

## Dateiformate

`.docx`, `.eml`, `.gltf`, `.ics`, `.jpg`, `.json`, `.md`, `.mp3`, `.mp4`, `.ogg`, `.pdf`, `.png`, `.txt`, `.wav`, `.webm`, `.zip`

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

Recherche und Bewertung: erledigt (Runde 6, 07./08.11.2026). Umsetzung: geplant, NOT RUN.
