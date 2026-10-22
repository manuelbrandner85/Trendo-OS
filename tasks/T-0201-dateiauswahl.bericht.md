# T-0201 Bericht: Dateiauswahl und Drag & Drop

**Auftrag:** Jedes Datei- und Ordnerfeld bekommt „Durchsuchen …“, Ziehen und Ablegen und die zuletzt benutzten Dateien des Studios. Der Pfad muss nicht mehr abgetippt werden. Berechnungslogik bleibt unverändert.
**Branch:** `cursor/dateiauswahl-programme-430c`

## Geänderte Dateien

- `src/Trendo.App/DateiFeld.cs` – gemeinsames Feld: Text, „Durchsuchen …“, Liste „Zuletzt benutzt“, Ablegen auf dem Feld.
- `src/Trendo.App/MainWindow.Dateien.cs` – Dialog über `StorageProvider` (im Test ersetzbar), Ablegen auf dem Studio, Zuletzt-Liste.
- `src/Trendo.Core/RecentPaths.cs` – zuletzt benutzte Pfade je Studio, höchstens acht, lokale JSON-Datei.
- `src/Trendo.App/MainWindow.W6.cs` – Fach-Werkzeuge mit Schlüssel `file`, `file2` oder `folder` nutzen das gemeinsame Feld. Der Textfeld-Name bleibt `{id}-{schlüssel}`.
- `src/Trendo.App/MainWindow.W4Media.cs`, `MainWindow.W5Finance.cs`, `MainWindow.W5Fusion.cs`, `MainWindow.Setup.cs`, `MainWindow.W1Letter.cs` – Medien, Bank-CSV, Videopaket, Projektordner im Assistenten, Anlagen und Scans.
- `src/Trendo.App/MainWindow.axaml.cs` – die Feldliste wird beim Seitenwechsel geleert, bevor die Seite aufgebaut wird.
- `tests/Trendo.App.Tests/DateiFeldUiTests.cs`, `tests/Trendo.Core.Tests/DateiUndProgrammeTests.cs`

Der Systemdialog öffnet sich über Avalonia `StorageProvider` mit einem Filter aus der Beschriftung (CSV, GPX, SRT, Bild, Video, Punktwolke und so weiter). Im Test liefert `DateiWahlOverride` den Pfad, damit kein echter Dialog hängen bleibt.

## Testergebnis

Ausgabe des Testläufers auf diesem Rechner (englisch, nicht „Bestanden!“):

- `Trendo.Core.Tests`: Passed 420, Failed 0, Skipped 10, Total 430
- `Trendo.App.Tests`: Passed 140, Failed 1, Skipped 0, Total 141
- `Trendo.Api.Tests`: Passed 14, Failed 0, Skipped 0, Total 14

Der eine Fehler ist `SYS-03-P14` (Seite als PDF). Chrome unter `/usr/bin/google-chrome` schreibt die PDF-Datei und beendet sich hier nicht, weil DBus fehlt. Der Test bricht nach 90 Sekunden ab. `pdfinfo` ist nicht installiert. Der Test und der PDF-Code sind unverändert. Auf einem Windows-PC nicht geprüft.

Die neuen Prüfungen: jedes Fach-Feld mit Endung `-file`, `-file2` oder `-folder` hat einen Knopf „Durchsuchen …“ (mindestens 30 Felder), dazu Medien, Bank-CSV, Videopaket und der Projektordner. Durchsuchen setzt den Pfad und merkt ihn. Ablegen füllt das erste leere Feld; ein Ordnerfeld nimmt den Ordner der abgelegten Datei.

## Offene Punkte

- Weitere Pfad-Textfelder außerhalb der beauftragten Seiten haben noch keinen Durchsuchen-Knopf: Wissen, Erweiterungen, Clean-Room, Abläufe, Farben, PDF zusammenfügen, Paket, Telefon, Verknüpfung, Sicherheitsordner, Forensik, Kontakte. Die 36 Fach-Felder und die im Auftrag genannten Seiten sind umgestellt.
- Kein echter Explorer-Dialog und kein echtes Ziehen aus dem Dateimanager. Der Test ruft dieselbe Übernahme auf, die das Ablegen benutzt.
- Nicht `PC_VERIFIED`. QA-017, QA-019 und Codesignatur sind nicht geprüft und nicht als bestanden markiert.

## Nicht geprüft

Echtes Fenster auf Windows, Aussehen des Feldes, ein echter `StorageProvider`-Dialog, Drag-Ereignisse des Betriebssystems, ob jedes der übrigen Pfadfelder aus dem Audit gemeint war.
