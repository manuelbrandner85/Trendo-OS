# T-0201 Bericht: Dateiauswahl und Drag & Drop

**Auftrag:** Jedes Datei- und Ordnerfeld bekommt „Durchsuchen …“, Ziehen und Ablegen und die zuletzt benutzten Dateien des Studios. Der Pfad muss nicht mehr abgetippt werden. Berechnungslogik bleibt unverändert.
**Branch:** `cursor/dateiauswahl-programme-430c`

## Geänderte Dateien

- `src/Trendo.App/DateiFeld.cs` – gemeinsames Feld: Text, „Durchsuchen …“, Liste „Zuletzt benutzt“, Ablegen auf dem Feld.
- `src/Trendo.App/MainWindow.Dateien.cs` – Dialog über `StorageProvider` (im Test ersetzbar), Ablegen auf dem Studio, Zuletzt-Liste.
- `src/Trendo.Core/RecentPaths.cs` – zuletzt benutzte Pfade je Studio, höchstens acht, lokale JSON-Datei.
- `src/Trendo.App/MainWindow.W6.cs` – Fach-Werkzeuge mit Schlüssel `file`, `file2` oder `folder` nutzen das gemeinsame Feld. Der Textfeld-Name bleibt `{id}-{schlüssel}`.
- `src/Trendo.App/MainWindow.W4Media.cs`, `MainWindow.W5Finance.cs`, `MainWindow.W5Fusion.cs`, `MainWindow.Setup.cs`, `MainWindow.W1Letter.cs` – Medien, Bank-CSV, Videopaket, Projektordner im Assistenten, Anlagen und Scans.
- `src/Trendo.App/MainWindow.Sys02.cs` – `Field` packt Pfadfelder in die gemeinsame Auswahl. `Line` und `Section` setzen das ganze Feld ein, nicht nur das Textfeld.
- `src/Trendo.App/MainWindow.Sys10.cs`, `Sys`-Seiten über `Field`, dazu `MainWindow.Wissen.cs`, `W5Ext.cs`, `Workflows.cs`, `W3.cs`, `W5CleanRoom.cs`, `W7.cs`, `Forensik.cs`, `Mail.cs`, `Share.cs`, `W4Builder.cs` – Wissen, Erweiterungen, Abläufe, Schutzordner, Nachbau, Handy, Forensik, Kontakte, Heimnetz, Baukasten-Bild, PDF-Zusammenfügen und Versand-Anlagen.
- `src/Trendo.App/MainWindow.axaml.cs` – die Feldliste wird beim Seitenwechsel geleert, bevor die Seite aufgebaut wird.
- `tests/Trendo.App.Tests/DateiFeldUiTests.cs`, `tests/Trendo.Core.Tests/DateiUndProgrammeTests.cs`

Der Systemdialog öffnet sich über Avalonia `StorageProvider` mit einem Filter aus der Beschriftung (CSV, GPX, SRT, Bild, Video, Punktwolke und so weiter). Im Test liefert `DateiWahlOverride` den Pfad, damit kein echter Dialog hängen bleibt.

## Testergebnis

Ausgabe des Testläufers auf diesem Rechner (englisch, nicht „Bestanden!“):

- `Trendo.Core.Tests`: Passed 420, Failed 0, Skipped 10, Total 430
- `Trendo.App.Tests`: Passed 141, Failed 1, Skipped 0, Total 142
- `Trendo.Api.Tests`: Passed 14, Failed 0, Skipped 0, Total 14

Der eine Fehler ist `SYS-03-P14` (Seite als PDF). Chrome unter `/usr/bin/google-chrome` schreibt die PDF-Datei und beendet sich hier nicht, weil DBus fehlt. Der Test bricht nach 90 Sekunden ab. `pdfinfo` ist nicht installiert. Der Test und der PDF-Code sind unverändert. Auf einem Windows-PC nicht geprüft.

Die neuen Prüfungen: jedes Fach-Feld mit Endung `-file`, `-file2` oder `-folder` hat einen Knopf „Durchsuchen …“ (mindestens 30 Felder), dazu Medien, Bank-CSV, Videopaket und der Projektordner. Durchsuchen setzt den Pfad und merkt ihn. Ablegen füllt das erste leere Feld; ein Ordnerfeld nimmt den Ordner der abgelegten Datei.

Zusätzlich öffnet ein Test jede Seite. Jedes Textfeld, das einen Pfad oder Ordner erwartet, muss in der gemeinsamen Auswahl liegen und einen Knopf „Durchsuchen …“ haben. Fehlt er, schlägt der Test fehl und nennt Seite und Feld. Mitgeprüft werden die Seiten, die das Feld erst nach einer Aktion zeigen: eigener Ablauf, Videopaket, Bild im Baukasten, Beweis-Fall, Nachbau-Projekt, Heimnetz-Freigabe und ein verbundenes Handy. Felder für Datei oder Ordner haben zusätzlich „Ordner …“. Mehrere Dateien (PDF zusammenfügen, Anlagen im Paket) stehen eine pro Zeile; Durchsuchen hängt an.

Adressen, Prüfsummen, Muster, Parameter und der Name einer Vorlage bleiben normale Textfelder. Die Berechnungslogik ist unverändert.

## Offene Punkte

- Kein echter Explorer-Dialog und kein echtes Ziehen aus dem Dateimanager. Der Test ruft dieselbe Übernahme auf, die das Ablegen benutzt.
- Nicht `PC_VERIFIED`. QA-017, QA-019 und Codesignatur sind nicht geprüft und nicht als bestanden markiert.

## Nicht geprüft

Echtes Fenster auf Windows, Aussehen des Feldes, ein echter `StorageProvider`-Dialog, Drag-Ereignisse des Betriebssystems. Ein Handy erscheint im Test nur als eingesetztes Gerät, nicht über adb.
