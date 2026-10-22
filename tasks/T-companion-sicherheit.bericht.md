# T-companion-sicherheit: Begleiter nur noch lokal, Kopplung begrenzt, Tokens laufen ab

**Auftrag:** QA-Rückgabe zur Standard-Bindung `http://+:{port}/` (genannter Commit af4f3a7, in diesem Checkout nicht in `git log`). Loopback als Standard, Netz nur bewusst, Rate-Limit und Sperre pro Client, Ablauf für Zugriffs- und Kopplungstokens. Bestehende Kopplung (W7) bleibt nutzbar, wenn das Heimnetz ausdrücklich frei ist.

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

**Testergebnis** (`dotnet build Trendo.sln -c Release`: 0 Fehler; `dotnet test Trendo.sln -c Release`, Ausgabe englisch „Passed!“):
- Trendo.Api.Tests: 15 bestanden, 0 Fehler, 0 übersprungen
- Trendo.Core.Tests: 524 bestanden, 0 Fehler, 10 übersprungen (WinRealTests, nur Windows), 534 gesamt
- Trendo.App.Tests: 211 bestanden, 0 Fehler, 0 übersprungen
- W7-Klasse darin: 15 bestanden, darunter echte HTTP-Prüfungen für Bindung, 429/Sperre/zweiter Client und Token-Ablauf
- curl gegen `serve`: 127.0.0.1 antwortet 200, 127.0.0.2 verweigert die Verbindung. Mit `--lan` antwortet 127.0.0.2 mit 200. Mit gespeichertem `netz an` und `--local` wieder nur 127.0.0.1

**Offene Punkte / Risiken:**
- Nach dem Update ist ein schon installierter Dienst ohne `--lan` nicht mehr aus dem Heimnetz erreichbar, bis jemand `netz an` speichert oder `--lan` setzt. Das ist der Fix, kein Versehen.
- Tokens ohne gespeichertes Ablaufdatum (alte `zustand.json`) enden bei Koppelzeit plus Gültigkeit (Standard 30 Tage).
- Sperre steht in `zustand.json` und bleibt über einen Neustart. Aufheben: warten oder `forget` (nimmt auch die Kopplungen mit).
- Keine echte Raspberry-Pi- oder Handy-Hardware. 127.0.0.2 steht hier für „nicht die Loopback-Adresse 127.0.0.1“.

**Nicht geprüft:** QA-017, QA-019, Codesignatur, Veröffentlichung, Windows-HttpListener (die HTTP-Tests kehren dort sofort zurück), Issues #246 / #236 / #104, StudioWorkflow, bc-58118c68.
