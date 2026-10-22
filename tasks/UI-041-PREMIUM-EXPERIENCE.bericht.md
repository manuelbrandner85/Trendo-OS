# UI-041 Bericht: Premium-Desktop, Startmenü, Fenster, Briefe schreiben

**Auftrag:** Design 41, Phase 1. Desktop, Startmenü und Fensterchrome wie die Referenzen 01, 02 und 06. UI41-SM-01 zu Ende: jedes Katalog-Studio öffnet aus dem Startmenü ein eigenes Trendo-Fenster mit eigenem Taskleisten-Eintrag. Briefe schreiben wie Referenz 04. Danach Stopp bis zur Freigabe.
**Branch:** `cursor/ui41-sm-01-startmenu-3c36` (Entwurf PR #237), auf `main` `19a37c3` neu aufgesetzt.

## Geänderte Dateien

- `src/Trendo.App/MainWindow.axaml.cs` – Schreibtisch ohne globalen Zoom: linke Leiste, Wortmarke, Suche, rechte Uhr mit echten Messwerten, Dock nur für vorhandene Programme. KI-Eingabe bleibt am unteren Rand der Seite. Fenster-Timer stoppen beim Schließen.
- `src/Trendo.App/MainWindow.StartMenu.cs` – großes Glasmenü, Kategorien mit Symbolen, Nutzer-Spalte.
- `src/Trendo.App/MainWindow.Windows.cs` – Fensterfläche über die ganze Arbeitsfläche, dichtere Scheibe, Titelleiste, Schatten, roter Schließen-Knopf.
- `src/Trendo.App/MainWindow.W1Letter.cs`, `MainWindow.W1Office.cs` – Briefe schreiben: Navigation, Formular, DIN-Vorschau, Speichern / Exportieren / Als PDF / Senden.
- `src/Trendo.App/MainWindow.W2.cs`, `MainWindow.Look.cs`, `MainWindow.Workflows.cs`, `MdiIcons.cs` – KI-Reiter ohne Doppelte in der Anzeige, Timer am Fenster, Symbole für Hilfe, Neu starten, Beenden.
- `src/Trendo.Core/SystemMetrics.cs` – RAM, SSD und Netz aus echten Quellen. GPU nur mit nvidia-smi, sonst „n. v.“.
- `src/Trendo.Core/Shell/PathFields.cs` – Suchfelder sind keine Dateifelder.
- `tests/Trendo.App.Tests/UiTests.cs`, `KiEnterTests.cs`, `StudioClickTests.cs`, `WindowsUiTests.cs`, `tests/Trendo.Core.Tests/PathFieldsTests.cs`
- `tools/Trendo.Render/Program.cs` – Aufnahmen Startmenü, Brief, mehrere Fenster.
- `reports/design-41/vergleich/` – Soll/Ist-Bilder und `UNTERSCHIEDE.md`.

UI41-SM-01 (jedes Studio, drei parallel, 1366×768) steckt im Vorgänger-Commit `241da0e` und bleibt grün.

## Testergebnis

`dotnet test Trendo.sln -c Release`. Die Laufzeit schreibt Englisch („Passed!“).

- `Trendo.Api.Tests`: Failed 0, Passed 15, Skipped 0, Total 15, Duration 285 ms.
- `Trendo.Core.Tests`: Failed 0, Passed 471, Skipped 10, Total 481, Duration 8 s. Die 10 Übersprungenen sind `WinRealTests` (Windows-Programme, auf Linux nicht da).
- `Trendo.App.Tests`: Failed 0, Passed 203, Skipped 0, Total 203, Duration 10 m 46 s. Darin `Jedes_Studio`, `Drei_gleichzeitig` bei 1366×768, KI-Enter, Brief, Fenster, DesignHonesty.

Davor einmal rot: SPEC-10 meldete „Alarm“, weil geschlossene Fenster samt Hintergrundbild im Speicher blieben (Arbeitsspeicher des Testprozesses über 3000 MB). Die Timer stoppen jetzt beim Schließen. Der Lauf danach ist grün. Ein einzelner Begleiter-Test war in einem früheren Lauf an „Address already in use“ beim Aufräumen gescheitert und im grünen Lauf bestanden.

## Offene Punkte

- Die übrigen Studios (Karten, KI-Seite, Einstellungen, Dateien) haben den neuen Look noch nicht. Erst nach JA.
- Unterschiede zur Referenz stehen in `reports/design-41/vergleich/UNTERSCHIEDE.md`.
- Auf dieser Linux-Box liegt Chrome im PATH, deshalb zeigt das Dock den Browser. Mail, Blender und Unreal nicht.

## Nicht geprüft

- Windows-PC, DPI 125/150 %, echte Windows-Taskleiste, Codesignatur, QA-017 am Gerät, QA-019.
- `SYS-03-P14` (Seite als PDF, A4) bleibt auf diesem Image offen, sobald `google-chrome` im PATH liegt. Die App ist dafür unverändert. `WinRealTests` sind übersprungen.
- Kein zweiter Blick auf Studio-Karten, Einstellungen und die KI-Seite gegen die Referenzen 03, 05 und 07.
