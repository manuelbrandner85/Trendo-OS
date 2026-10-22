# T-0210 Bericht

**Auftrag:** Einrichtung – gewählte Optionen sichtbar, Design sofort als Vorschau, „Ordner wählen …“ öffnet einen Dialog, Reparieren und „Jetzt prüfen und reparieren“ zeigen Fortschritt und Ergebnis. Nichts ohne Rückfrage installieren.

**Branch:** `cursor/t-0210-einrichtung-6aed` (PR https://github.com/manuelbrandner85/Trendo-OS/pull/264). Der genannte Name `cursor/t-0210-einrichtung` existiert nicht. Der ältere Branch `cursor/t-0210-einrichtung-18f9` (PR #226) ist geschlossen und ohne Merge, weil #217 eine kürzere Fassung auf `main` gebracht hat. Dieser Branch setzt die offenen Punkte auf dem aktuellen `main` um. Nicht gemergt.

**Geänderte Dateien:**
- `src/Trendo.App/MainWindow.Setup.cs` – Häkchen, Rahmen, Zusammenfassung, `ApplyTheme` sofort, Reparieren mit Rückfrage
- `src/Trendo.App/MainWindow.Heal.cs` – „Prüfung läuft …“ / „Gerade geprüft.“
- `src/Trendo.App/MainWindow.Files.cs` – nur die Meldung nach dem bestehenden Ordnerdialog für `wiz-folder`
- `src/Trendo.App/MainWindow.Look.cs` – Hilfetexte
- `tests/Trendo.App.Tests/SetupFeedbackTests.cs`

**Testergebnis** (Release, Linux, 22.10.2026, `dotnet build Trendo.sln -c Release` und `dotnet test Trendo.sln -c Release --no-build`):
- Build: erfolgreich, 0 Fehler, 26 Warnungen (bestanden schon vorher, nicht aus diesem Auftrag)
- Trendo.Api.Tests: Passed 15, Failed 0, Total 15
- Trendo.Core.Tests: Passed 529, Skipped 10, Failed 0, Total 539
- Trendo.App.Tests: Passed 224, Failed 0, Total 224 (Dauer 12 min 50 s)

Die Konsole hat englisch „Passed!“ ausgegeben, nicht „Bestanden!“. Die Zahlen stammen aus dieser Ausgabe. `SYS-03-P14` ist in den 224 enthalten und in diesem Lauf nicht fehlgeschlagen. Der PDF-Code wurde nicht geändert. Chrome liegt unter `/usr/local/bin/google-chrome`.

**Offene Punkte / Risiken:**
- Dock und Seitenleiste übernehmen das Design erst beim nächsten Fensteraufbau. Die Assistentenseite und die Farbkarte wechseln sofort. Rückweg: in `ApplyWizardTheme` nur `_settings.Theme` setzen und `ApplyTheme` weglassen.
- „Reparieren“ installiert nicht mehr beim ersten Klick. Rückweg: `ConfirmRepair` wieder direkt `InstallOne` aufrufen lassen.
- Ein echter winget-Lauf und der native Windows-Ordnerdialog wurden nicht ausgeführt.
- QA-017, QA-019 und Codesignatur sind nicht als bestanden markiert.

**Nicht geprüft:**
- Windows-PC, echter Ordnerdialog, echtes winget.
- Ob ein bereits installiertes Programm ohne Rückfrage „repariert ✓“ zeigt (auf diesem Rechner fehlen die sechs Programme; der Test sieht „fehlt – Installieren?“).
- StudioWorkflow, Brief, Tabelle, E-Mail, Rechnung, #236, #104 (nicht angefasst).
