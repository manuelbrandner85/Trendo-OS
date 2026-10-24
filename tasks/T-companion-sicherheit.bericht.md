# T-companion-sicherheit: Begleiter nur noch lokal, Kopplung begrenzt, Tokens laufen ab

**Auftrag:** QA-Rückgabe zur Standard-Bindung `http://+:{port}/`. Der im ersten Auftrag genannte Commit `af4f3a7` ist auf `main` der Merge von #246 (Webseite), nicht die Begleiter-Bindung. Die Bindung selbst saß in `CompanionServer`. Loopback als Standard, Netz nur bewusst, Rate-Limit und Sperre pro Client, Ablauf für Zugriffs- und Kopplungstokens. Bestehende Kopplung (W7) bleibt nutzbar, wenn das Heimnetz ausdrücklich frei ist.

**Branch:** `cursor/companion-server-sicherheit-dd61`

**Geänderte Dateien:**
- `src/Trendo.Companion/CompanionServer.cs`
- `src/Trendo.Companion/Program.cs`
- `src/Trendo.Core/W7/Companion.cs`
- `tests/Trendo.Core.Tests/W7Tests.cs`
- `src/Trendo.App/MainWindow.W7.cs` (ein Hinweistext)
- `installer/companion/trendo-companion.service`
- `installer/companion/install.sh`
- `docs/build-log/2026-10-22-companion-sicherheit.md`

**QA-Folge auf demselben Branch:** Entkoppeln nutzt `CryptographicOperations.FixedTimeEquals` (ungleiche Länge wirft nicht). `DropExpired` entfernt abgelaufene PCs, der Server schreibt die Datei. `Trim` entfernt alte Versuche und Fehlerserien nach der Sperrfrist; laufende Sperre und frische Fehlversuche bleiben. `Load` protokolliert beschädigtes JSON. `main` ist per Merge drin (Basis war `c291903`), kein Rebase, kein Force-Push.

**Testergebnis** (`dotnet build Trendo.sln -c Release`: 0 Fehler; `dotnet test Trendo.sln -c Release --no-build`, Ausgabe englisch „Passed!“, Exit 0), Stand nach dem Merge und der QA-Folge:
- Trendo.Api.Tests: 15 bestanden, 0 Fehler, 0 übersprungen
- Trendo.Core.Tests: 538 bestanden, 0 Fehler, 10 übersprungen (WinRealTests, nur Windows), 548 gesamt
- Trendo.App.Tests: 222 bestanden, 0 Fehler, 0 übersprungen
- W7-Klasse darin: 16 bestanden, darunter Entkoppeln, abgelaufene PCs, Trim und die früheren HTTP-Prüfungen
- curl gegen `serve` (vor dem Merge, unverändertes Verhalten): 127.0.0.1 antwortet 200, 127.0.0.2 verweigert die Verbindung. Mit `--lan` antwortet 127.0.0.2 mit 200. Mit gespeichertem `netz an` und `--local` wieder nur 127.0.0.1

**Offene Punkte / Risiken:**
- Nach dem Update ist ein schon installierter Dienst ohne `--lan` nicht mehr aus dem Heimnetz erreichbar, bis jemand `netz an` speichert oder `--lan` setzt. Das ist der Fix, kein Versehen.
- Tokens ohne gespeichertes Ablaufdatum (alte `zustand.json`) enden bei Koppelzeit plus Gültigkeit (Standard 30 Tage).
- Sperre steht in `zustand.json` und bleibt über einen Neustart. Aufheben: warten oder `forget` (nimmt auch die Kopplungen mit).
- Keine echte Raspberry-Pi- oder Handy-Hardware. 127.0.0.2 steht hier für „nicht die Loopback-Adresse 127.0.0.1“.

**Nicht geprüft:** QA-017, QA-019, Codesignatur, Veröffentlichung, Windows-HttpListener (die HTTP-Tests kehren dort sofort zurück). StudioWorkflow und #246 sind durch den Merge von `main` im Branch; ihre Tests liefen in der Gesamtsuite mit, eine eigene Durchsicht gab es nicht. bc-58118c68 und #236 wurden nicht bearbeitet.
