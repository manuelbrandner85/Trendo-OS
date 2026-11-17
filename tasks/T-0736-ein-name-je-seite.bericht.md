# T-0736 – Bericht

**Zweig:** `claude/T-0736-ein-name-je-seite` (Basis main 8a476686e). Arbeitskopie `%TEMP%\trendo-auftrag-T-0736`.

## Geändert
- `src/Trendo.App/MainWindow.Einstellungen.cs`: neue Funktion `ShellLabel(key, label)` – Studio-Startseiten heißen in der linken Leiste und in den Treffern der Suche wie das Studio aus `StudioCatalog`; die Suche findet die Seite auch unter dem früheren Leistennamen.
- `tests/Trendo.App.Tests/T0736SeitennamenUiTests.cs`: zwei neue Tests.

Beispiele (vorher Leiste → jetzt): „Leistung messen“ → „Qualität prüfen“, „Sehen & Bedienhilfen“ → „Schrift & Farben anpassen“,
„KI-Quellen“ → „Kostenlose KI finden“, „KI-Helfer und Teams“ → „KI-Team einsetzen“, „Sicher prüfen“ → „Surfen & Herunterladen“,
„Geräte & Laufwerke“ → „Geräte erkennen“, „Im Heimnetz teilen“ → „Dateien teilen“.

## Tests (gezielt, Release-Build)
- Rot-Probe (ShellLabel gibt den alten Namen zurück): `T0736Seitennamen_Ui` 2 von 2 rot, u. a. „bedien-sehen: Leiste „Sehen & Bedienhilfen“, Fenster „Schrift & Farben anpassen““.
- Grün: `T0736Seitennamen_Ui` 2/2, `DesignPaket1_Ui` 31/31, `StudioHeadingTests` 2/2, `AppsAndSetupResumeTests` 8/8, `Design41RolloutTests` 3/3,
  `EinfachBedienen_Ui` 11/11, `Einfachheit_Ui` 2/2, `Leistungsprofil_Ui` 1/1, `OsStyles_Ui` 10/10.
- Bild geprüft: Einstellungen auf „Qualität prüfen“ (Profi), Leiste und Fenstertitel gleich.

## Nicht geprüft
- Volltest (macht die Hauptsitzung am Schluss).

## Status
Umgesetzt und gezielt getestet.
