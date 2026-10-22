# T-0222: Programme nachbauen aus Name oder Beschreibung

**Auftrag:** Studio X-10 kann ein Programm nicht nur als Ordner entgegennehmen. Man benennt oder beschreibt es, zum Beispiel „ein Notizprogramm wie Notepad++“. Mit Schalter „Im Internet recherchieren“ liest Trendo öffentliche Wikipedia-Seiten, zeigt die Quellen und baut daraus einen abhakbaren Plan. „Los“ erzeugt eine eigene Avalonia-App, „In Trendo öffnen“ startet sie als eigenes Fenster, „Als Setup-Datei erstellen“ schreibt eine unsignierte Setup.exe in einen gewählten Ordner. Folgeaufträge ändern die Fassung, Rückgängig stellt sie wieder her.

**Branch:** `cursor/programme-nachbauen-2b4f`

## Geänderte Dateien

- Neu: `src/Trendo.Core/Rebuild/` (Plan, untrusted Text, Vorlage, Bau, Fassungen, Setup-Packer, Adapter `nachbauen`, „Meine Programme“)
- Neu: `src/Trendo.App/MainWindow.Rebuild.cs`, `src/Trendo.App/WikipediaResearch.cs`
- Neu: `tests/Trendo.Core.Tests/RebuildTests.cs`, `tests/Trendo.App.Tests/RebuildUiTests.cs`, `tests/Trendo.App.Tests/WikipediaResearchTests.cs`
- Angepasst: `StudioCatalog.cs`, `MainWindow.W5CleanRoom.cs`, `MainWindow.PromptAction.cs`, `MainWindow.StartMenu.cs`, `MainWindow.Sys13.cs`, `MainWindow.W1.cs`, `CapabilityCatalog.cs`, `IntelligencePromptModel.cs`, `PromptActionTests.cs`, `tools/Trendo.Render/Program.cs`
- Doku: `docs/improvement-42/studio-capability-matrix.md`, `architecture-decision.md`, `security-approvals.md`, dieser Bericht, `docs/build-log/2026-10-22-1935-programme-nachbauen.md`
- `requirements.yaml` ist unverändert. X-10 bleibt auf dem bisherigen Status.

## Testergebnis

`dotnet test Trendo.sln -c Release` endete mit Exitcode 0. `TRENDO_VPK` war nicht gesetzt.

- `Trendo.Api.Tests`: Failed 0, Passed 15, Skipped 0, Total 15, Dauer 645 ms.
- `Trendo.Core.Tests`: Failed 0, Passed 527, Skipped 10, Total 537, Dauer 11 s. Die 10 Übersprungenen sind die vorhandenen Windows-Prüfungen (`WinRealTests`).
- `Trendo.App.Tests`: Failed 0, Passed 215, Skipped 0, Total 215, Dauer 6 m 34 s.

Ein vollständiger Rebuild davor meldete 0 Fehler und die schon vorhandenen Warnungen (unter anderem `StudioWorkflow` CS8604, `StudioIcons`, veraltetes `Watermark`, xUnit-Hinweise).

Der Notiz-Lauf `RebuildTests.Notiz_wird_gebaut` hat wirklich `dotnet build` und `dotnet test` ausgeführt: 3 bestanden, 0 Warnungen, `--selftest` druckt `ok`. Beleg: `/opt/cursor/artifacts/notiz-selftest.txt`. Die erzeugte App enthält den Namen Notepad nicht.

Zusätzlich, außerhalb der Gesamtsuite, mit `TRENDO_VPK=/tmp/vpk-tool/vpk` (Velopack 1.2.161, nur in diesem Lauf installiert): framework-abhängiges `dotnet publish` und `vpk pack` erzeugten `/opt/cursor/artifacts/setup/Merkzettel-Setup.exe`, 22 072 268 Bytes, `file` meldet `PE32+ executable (GUI) x86-64`. Daneben liegt `HINWEIS-UNSIGNIERT.txt`. vpk meldete, dass ohne `--runtime` auf x86 vorgewählt wird; die Datei selbst ist als x86-64 erkannt. Sie wurde nicht installiert und nicht signiert. Die Schaltfläche in der Oberfläche packt mit `--self-contained true`. Dieses selbstständige Paket wurde nicht gebaut.

Oberfläche über `tools/Trendo.Render` mit `TRENDO_REBUILD=1`: Plan, eigenes Fenster mit Taskleisten-Eintrag, Startmenü „Meine Programme“. Bilder: `/opt/cursor/artifacts/nachbau-plan.png`, `nachbau-fenster.png`, `nachbau-start.png`. Das Render schreibt keine echte `RebuildKern.dll`, deshalb sagt das Fenster ehrlich, die Bibliothek habe sich nicht laden lassen. Der Kern-Test hat die echte Bibliothek per Reflexion und `--selftest` geladen.

## Offene Punkte

- Der registrierte Prompt-Adapter nutzt `OfflineWeb`. Erst der Schalter auf der Studio-Seite und ein Online-Lauf lesen Wikipedia. Der Seitentext bleibt untrusted.
- Der Kern referenziert `System.Net.Http` nicht. Netzfehler werden in der App als `PublicReadException` weitergegeben.
- Stufe in der Matrix bleibt `partial`. Beleg ist `RebuildTests.Notiz_wird_gebaut`. Setup und Windows-Installation sind nicht allgemein geprüft, eine Codesignatur gibt es nicht.
- Zettel in Trendo liegen unter `{projekt}/daten/zettel.txt`. Die eigenständige App schreibt nach `LocalAppData/EigeneApps/{Titel}`.
- Rückgängig stellt die Fassung wieder her und markiert den Bau als veraltet. Es gibt keinen gemeinsamen Undo-Stapel über `RunStore` hinaus, nur `fassungen/vNNN`.

## Nicht geprüft

- Keine Installation der Setup.exe, kein SmartScreen, keine Codesignatur, kein Windows-PC.
- Kein selbstständiges (`--self-contained true`) Win-x64-Paket.
- Keine laufende Ollama-Instanz und keine Cloud-KI. Die Reparatur-Runde ist mit einer Fake-KI geprüft.
- Kein echter Wikipedia-Abruf. Die Tests nutzen einen Handler, der JSON liefert, und eine feste Seite.
- Das Fenster der erzeugten App selbst wurde nicht geöffnet, nur `--selftest` und die Trendo-Ansicht.
- QA-017 ist nicht bestanden. QA-019 bleibt die vorhandene Prüfung „keine Netzbibliothek im Kern“; sie ist grün, weil der Client in der App liegt. Sie ist damit nicht als neues Abnahme-Siegel gemeint.
