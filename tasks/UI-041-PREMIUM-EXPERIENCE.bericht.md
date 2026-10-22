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

## Offene Punkte (Stand vor dem JA, danach erledigt)

- Die übrigen Studios hatten den neuen Look noch nicht. Der Besitzer hat JA gesagt. Die Übertragung steht unten.
- Die linke Liste aus Referenz 01 ist bewusst weg. Unterschiede stehen in `reports/design-41/vergleich/UNTERSCHIEDE.md`.
- Kein Wetter auf dem Schreibtisch. Es bleibt in „Zeit & Kalender“ und erscheint erst nach Ort und Klick.
- Fehlt die Texterkennung, öffnet „Briefe schreiben“ im Startmenü zuerst die Programmfrage. „Später“ führt in den Brief. Die Suche nach „Brief“ öffnet die Seite direkt.
- Auf dieser Linux-Box liegt Chrome im PATH, deshalb zeigt das Dock den Browser. Blender und Unreal Engine sind auf dem PC installiert. Diese Box startet die Windows-Programme nicht, deshalb fehlen ihre Dock-Symbole hier.

## Nicht geprüft

- Windows-PC, DPI 125/150 %, echte Windows-Taskleiste, Codesignatur, QA-017 am Gerät, QA-019.
- `SYS-03-P14` (Seite als PDF, A4) bleibt auf diesem Image offen, sobald `google-chrome` im PATH liegt. Die App ist dafür unverändert. `WinRealTests` sind übersprungen.
- Kein zweiter Blick auf Studio-Karten, Einstellungen und die KI-Seite gegen die Referenzen 03, 05 und 07. Das ist mit dem JA nachgeholt, siehe unten.

## UI41-7 – Übertragung auf die übrigen Studios

**Auftrag:** Der Besitzer hat JA gesagt. Dieselbe Hülle (Glas, eigenes Trendo-Fenster mit Taskleisten-Eintrag, Startmenü UI41-SM-01, Katalognamen, passende Symbole) liegt jetzt auf den übrigen Studios und Seiten. Die Arbeitsflächen bleiben. Kein Knopf ist neu tot.
**Branch:** weiterhin `cursor/ui41-sm-01-startmenu-3c36`, PR #237.

### Geänderte Dateien

- `src/Trendo.App/MainWindow.axaml.cs` – Seitentitel mit Katalog-Symbol und Satz. Alle Studios: Bereichs-Chips und Suche. KI in drei Spalten, Eingabe fest unten. Widgets nur auf dem freien Schreibtisch.
- `src/Trendo.App/MainWindow.W2.cs`, `MainWindow.W45.cs`, `MainWindow.W1Apps.cs` – Glas-Reiter, Glas-Knöpfe, Leistungskarten `res-cpu`, `res-ram`, `res-gpu`.
- `src/Trendo.App/MainWindow.Look.cs` – Einstellungen mit linker Leiste (Aussehen, Farbe, Hintergrund, Bedienung, Startbildschirm, Weiteres). Kachel-Reihenfolge mit Katalognamen.
- `src/Trendo.App/MainWindow.Setup.cs` – Loslösen auch aus Inhalt und Rahmen, damit die KI-Seite ein zweites Mal aufgeht.
- `src/Trendo.App/MainWindow.Windows.cs` – Nach dem Fensterzeichnen die Widget-Sichtbarkeit neu setzen.
- `src/Trendo.App/MainWindow.W1.cs`, `MainWindow.W1Office.cs` – Suchtreffer und der Knopf heißen „Briefe schreiben“ bzw. „Akten“.
- `src/Trendo.Core/Shell/PathFields.cs` – „Studio suchen“ ist kein Dateifeld.
- `tools/Trendo.Render/Program.cs` – Aufnahme `seite` für eine gewählte Seite.
- `tests/Trendo.App.Tests/Design41RolloutTests.cs` – Filter und Suche, Einstellungs-Abschnitte, KI erneut öffnen.
- `reports/design-41/vergleich/gruppen/` – Soll/Ist je Gruppe, 1920×1080 und 1366×768. Unterschiede in `UNTERSCHIEDE.md`.

### Testergebnis

`dotnet test Trendo.sln -c Release` auf diesem Stand. Ausgabe auf Linux: „Passed!“.

- `Trendo.Api.Tests`: Failed 0, Passed 15, Skipped 0, Total 15, Duration 326 ms.
- `Trendo.Core.Tests`: Failed 0, Passed 471, Skipped 10, Total 481, Duration 6 s. Die 10 Übersprungenen sind `WinRealTests`.
- `Trendo.App.Tests`: Failed 0, Passed 206, Skipped 0, Total 206, Duration 11 m 57 s. Darin die drei neuen Prüfungen plus Studio-Überschriften, Symbole, Klicks, KI-Enter, Fenster und QA-017 in der Suite.

### Offene Punkte

- DPI 125 % und 150 %, Windows-PC, QA-017 am Gerät, QA-019, Codesignatur.
- `SYS-03-P14` (PDF, A4) bleibt auf diesem Image offen, sobald `google-chrome` im PATH liegt. Die App ist unverändert.
- Keine Fotokarten, keine Themen aus den Bildern, kein „GPT-4o“. Tapete bleibt Bergsee bei Nacht. Der Schriftzug kann durch das Glas schwach durchscheinen. Kurze Seiten lassen darunter leeres Glas.
- Fach- und Kreativ-Reiter umbrechen bei 1366×768 und bleiben klickbar.
- „Briefe schreiben“ aus dem Startmenü fragt ohne Texterkennung zuerst nach dem Zusatzprogramm. „Später“ öffnet den Brief.
- Wetter nur in „Zeit & Kalender“. SPEC-07 nicht gebaut. Logo bleibt Entwurf A.
- Die Dateien-Seite ist „Kleine Helfer & Musik“, kein nachgebauter Explorer.
- Dock auf dieser Box: Dateien, Browser (Chrome im PATH), Papierkorb. Blender und Unreal Engine sind auf dem PC installiert; die Windows-Programme laufen in dieser Linux-Aufnahme nicht.

### Nicht geprüft

- Kein Klick durch jede der über 50 Arbeitsflächen von Hand. Die Suite prüft Überschriften, Symbole, das Öffnen aus dem Startmenü und die genannten Knöpfe. Die Bilder zeigen je Bereich eine echte Seite.
- Kein Windows-PC, keine DPI-Stufen, kein Gerät für QA-017 und QA-019, keine Codesignatur.
