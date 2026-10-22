# UI-041 Bericht: Premium-Desktop, Startmenü, Fenster, Briefe schreiben

**Auftrag:** Design 41, Phase 1. Desktop, Startmenü und Fensterchrome wie die Referenzen 01, 02 und 06. UI41-SM-01 zu Ende: jedes Katalog-Studio öffnet aus dem Startmenü ein eigenes Trendo-Fenster mit eigenem Taskleisten-Eintrag. Briefe schreiben wie Referenz 04. Zusätzlich der Wunsch des Besitzers: die App- und Studio-Liste links vom Schreibtisch entfernen. Die Tapete bleibt frei. Wenige Widgets (Uhr, echte Messwerte, echte Projekte, selbst angeheftete Studios). Alles andere über Start oder Suche. Danach Stopp bis zur Freigabe.
**Branch:** `cursor/ui41-sm-01-startmenu-3c36` (Entwurf PR #237), auf `main` `19a37c3` neu aufgesetzt.

## Geänderte Dateien

- `src/Trendo.App/MainWindow.axaml.cs` – Schreibtisch ohne linke Liste. Mitte: Logo A, „Trendo OS“, Suche. Rechts schwebend: Uhr und echte CPU/RAM/GPU/SSD/Netz (`widget-system`), „Zuletzt“ nur mit echten Projekten (`widget-projekte`), „Angeheftet“ nur für selbst gewählte Studios.
- `src/Trendo.App/MainWindow.StartMenu.cs`, `MainWindow.Windows.cs` – Menü und Fensterfläche über die eine Arbeitsfläche. Anheften aktualisiert die Schreibtisch-Karte.
- `src/Trendo.App/MainWindow.W1Letter.cs`, `MainWindow.W1Office.cs` – Briefe schreiben: Navigation, Formular, DIN-Vorschau, Speichern / Exportieren / Als PDF / Senden.
- `src/Trendo.App/MainWindow.W2.cs`, `MainWindow.Look.cs`, `MainWindow.Workflows.cs`, `MdiIcons.cs` – KI-Reiter, Timer am Fenster, Symbole.
- `src/Trendo.Core/SystemMetrics.cs` – RAM, SSD und Netz aus echten Quellen. GPU nur mit nvidia-smi, sonst „n. v.“.
- `src/Trendo.Core/Shell/PathFields.cs` – Suchfelder sind keine Dateifelder.
- `tests/Trendo.App.Tests/UiTests.cs` – Tab/Enter über Profil und Einstellungen im Startmenü. Notizen über die Suche. Briefe schreiben über das Startmenü.
- `tools/Trendo.Render/Program.cs` – Aufnahmen Startmenü, Brief, mehrere Fenster.
- `reports/design-41/vergleich/` – Soll/Ist-Bilder und `UNTERSCHIEDE.md`.

UI41-SM-01 (jedes Studio, drei parallel, 1366×768) steckt im Commit `241da0e` und bleibt grün.

## Testergebnis

`dotnet test Trendo.sln -c Release`. Die Laufzeit schreibt Englisch („Passed!“).

- `Trendo.Api.Tests`: Failed 0, Passed 15, Skipped 0, Total 15, Duration 601 ms.
- `Trendo.Core.Tests`: Failed 0, Passed 471, Skipped 10, Total 481, Duration 6 s. Die 10 Übersprungenen sind `WinRealTests` (Windows-Programme, auf Linux nicht da).
- `Trendo.App.Tests`: Failed 0, Passed 203, Skipped 0, Total 203, Duration 11 m 11 s. Darin `Jedes_Studio`, `Drei_gleichzeitig` bei 1366×768, KI-Enter, Brief, Fenster, DesignHonesty.

## Offene Punkte

- Die übrigen Studios (Karten, KI-Seite, Einstellungen, Dateien) haben den neuen Look noch nicht. Erst nach JA.
- Die linke Liste aus Referenz 01 ist bewusst weg. Unterschiede stehen in `reports/design-41/vergleich/UNTERSCHIEDE.md`.
- Kein Wetter auf dem Schreibtisch. Es bleibt in „Zeit & Kalender“ und erscheint erst nach Ort und Klick.
- Fehlt die Texterkennung, öffnet „Briefe schreiben“ im Startmenü zuerst die Programmfrage. „Später“ führt in den Brief. Die Suche nach „Brief“ öffnet die Seite direkt.
- Auf dieser Linux-Box liegt Chrome im PATH, deshalb zeigt das Dock den Browser. Mail, Blender und Unreal nicht.

## Nicht geprüft

- Windows-PC, DPI 125/150 %, echte Windows-Taskleiste, Codesignatur, QA-017 am Gerät, QA-019.
- `SYS-03-P14` (Seite als PDF, A4) bleibt auf diesem Image offen, sobald `google-chrome` im PATH liegt. Die App ist dafür unverändert. `WinRealTests` sind übersprungen.
- Kein zweiter Blick auf Studio-Karten, Einstellungen und die KI-Seite gegen die Referenzen 03, 05 und 07.
