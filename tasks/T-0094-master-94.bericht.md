# T-0094: Master bis 94, Phase 1

**Auftrag:** Die verbindliche Masterdatei unverändert aufnehmen, Lücken 42–94 ehrlich benennen, einen Fahrplan schreiben und Phase 1 wirklich bauen.
**Branch:** `cursor/master-bis-94-5d61`
**Geänderte Dateien:** `docs/spec/MASTER_BIS_94.md`, `docs/spec/MASTERPROMPT.md`, `reports/master-94/LUECKEN.md`, `reports/master-94/FAHRPLAN.md`, `src/Trendo.Core/PromptAction/ArbeitsplatzAdapter.cs`, `src/Trendo.Core/PromptAction/WorkspaceBoard.cs`, `src/Trendo.App/MainWindow.OneWorkspace.cs`, Leiste und Seitenkarte, `CapabilityCatalog` nur die Zeile X-11, `PromptActions.CreateDefault`, Tests `OneWorkspaceTests` und `OneWorkspaceUiTests`, Matrix und Bedienung unter `docs/improvement-42/`, `tools/Trendo.Render/Program.cs`.

Die Masterdatei ist 59976 Bytes, SHA-256 `76f963654854b380679b4821e10e3ba283d596705d023b63975cc71fcd1ce6b2`, unverändert. NEIN bleiben 23, 25, 34, Language Studio 2.0 und 86. 35 bleibt OFFEN und ist nicht aktiviert. `ForensicsLab.MaxLife` bleibt 120 Stunden. `decisions.yaml` ist nicht erweitert.

## Was Phase 1 tut

Eine Seite „Ein Arbeitsplatz“ zeigt das Ergebnis von `StudioWorkflow`. Der Adapter `arbeitsplatz.blatt` schreibt nur für X-11 die Datei `arbeitsplatz/blatt-vN.md` und prüft sie durch Lesen. Ohne Projekt kommt eine Rückfrage. Ein Studio ohne Adapter endet mit „Nicht unterstützt“ und ohne Fachdatei. Der Adapter hat keine Wirkung nach außen. „Einmal freigeben“ bleibt aus. Ein Satz im Text gibt keine Freigabe. 53 Studios bleiben `not_implemented`. Keine zweite Engine. Keine kostenpflichtige KI.

42 und 43 sind damit nicht fertig. Kein Paket 42–94 ist IMPLEMENTED oder TESTED.

## Testergebnis

`dotnet test Trendo.sln -c Release` am 22.10.2026, Linux, .NET 10. Zweimal gelaufen, vor und nach der Umordnung von Prüfung und Export. Beide Male dieselben Zahlen:

- `Trendo.Api.Tests`: 15 bestanden, 0 übersprungen, 0 Fehler.
- `Trendo.Core.Tests`: 526 bestanden, 10 übersprungen (`WinRealTests`), 0 Fehler, gesamt 536.
- `Trendo.App.Tests`: 215 bestanden, 0 übersprungen, 0 Fehler.

Neu in diesem Auftrag sind 8 Core-Tests (`OneWorkspaceTests`) und 4 App-Tests (`OneWorkspaceUiTests`). Die Kopfaufnahme kommt aus `tools/Trendo.Render` mit `TRENDO_ARBEITSPLATZ=blatt` und `=brief`. Die Bedienungszeile danach ändert keinen Test.

## Offene Punkte

- Die offenen PRs #237 und #243–#257 sind nicht gemergt und hier nicht ausgeführt. Sie treffen `CreateDefault`, die Matrix und teils die Leiste. Zusammenführen nur von Hand, eine Adapterliste.
- PR #246 hängt nicht an `StudioWorkflow`.
- X-11 legt hier kein Projekt an. Die Aktion `projekt.anlegen` aus dem offenen PR #255 ist eine andere Aktion.
- QA-017, QA-019 und die Codesignatur sind nicht bestanden.
- PDF im Format A4 ist nicht gesondert gelaufen. Die Gesamtsuite ist grün.

## Nicht geprüft

- Kein Windows-PC, kein Blender, kein Unreal, kein LibreOffice, keine Kamera, kein Roboter, keine Fräse.
- Kein Versand, keine Zahlung, keine Veröffentlichung, kein Gerätebefehl.
- AION und GENESIS liegen nicht in diesem Repo und sind nicht angefasst.
- Ob 35 je aktiviert wird, ist nicht entschieden.
