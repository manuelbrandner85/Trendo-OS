# T-0216 – Mikrofon nicht in der Vorschau

## Auftrag
Nach dem Livestream: „Mikrofone suchen“ darf das Häkchen „Mikrofon mit aufnehmen“ nicht von allein setzen. In `LiveFfmpeg.Args` kommt das Mikrofon nur in die Aufnahme und in die Sendung, nie in die Vorschau, damit die Vorschau das Gerät nicht belegt. Für beides gibt es Tests.

Dieser Lauf legt den Branch `cursor/live-mic-preview-3e3d` an, weil der PR-Ablauf dieses Namensschema verlangt. Basis ist der aktuelle `main` nach dem Merge von #228.

## Branch
`cursor/live-mic-preview-3e3d`

## Geänderte Dateien
- `src/Trendo.App/MainWindow.Live.cs` – Suche füllt die Liste und lässt das Häkchen, wie es war
- `src/Trendo.Core/W6/LiveStream.cs` – Mikrofon-Eingang nur bei Aufnahme und Sendung
- `tests/Trendo.Core.Tests/LiveStreamTests.cs`
- `tests/Trendo.App.Tests/LiveStreamUiTests.cs`

## Testergebnis
`dotnet test Trendo.sln -c Release`, SDK 10, Linux:

- Trendo.Core.Tests: Bestanden 453, übersprungen 10, Fehler 0, gesamt 463. Dauer 7 s. Darin der neue Test „Mikrofon nur bei Aufnahme und Sendung, nie in der Vorschau“ (Klasse 9 bestanden).
- Trendo.Api.Tests: Bestanden 15, Fehler 0, gesamt 15. Dauer 566 ms.
- Trendo.App.Tests: Bestanden 190, Fehler 0, gesamt 190. Dauer 5 min 45 s. Darin der neue Test „Mikrofone suchen setzt das Häkchen nicht“ (Klasse 6 bestanden).

## Offene Punkte / Risiken
- Wer das Häkchen schon gesetzt hat, behält es. Die Suche ändert es nicht.
- Ohne Mikrofon legt die Sendung weiter eine stille Tonspur an (`anullsrc`), damit das Ziel Ton erwartet.

## Nicht geprüft
- Kein echtes Mikrofon und kein echtes FFmpeg. Die Tests prüfen den Befehl und das Häkchen.
- Kein Codesign. QA-017 und QA-019 nicht als bestanden gewertet. Kein PC_VERIFIED.
