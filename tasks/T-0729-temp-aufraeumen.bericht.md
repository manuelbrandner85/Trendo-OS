# Bericht T-0729: Temp-Ordner garantiert aufräumen

**Branch:** `claude/T-0729-temp-aufraeumen` (von `claude/T-0685-b1-integration`), Arbeitskopie `%TEMP%\trendo-auftrag-T-0729` (danach gelöscht).

## Was geändert wurde
| Stelle | vorher | jetzt |
|---|---|---|
| `src/Trendo.Core/TempDir.cs` (neu) | keine gemeinsame Hilfe; überall eigenes `Directory.Delete` | `TempDir.Create` + `using`, `TempDir.Delete` (bis zu N Versuche bei gesperrten Dateien, Schreibschutz weg, folgt keinen Verknüpfungen), `SweepStale` (feste Präfixliste, Schutzliste, 24 h, „jung“ = irgendein Eintrag darin jünger), `BeginTestRun` (Laufordner je Testprozess) |
| `src/Trendo.App/WebsitePreview.cs:222/268` (Chrome ohne Fenster, `--user-data-dir`) | Profil `trendo-chrome-<guid>`; nach Job-Ende **ein** Löschversuch, Fehler verschluckt → 585 Profile à ~70 MB blieben | Profil über `TempDir.Create`; nach Ende aller Chrome-Prozesse (Job-Objekt, eigene PIDs) bis zu 25 Versuche à 200 ms |
| `src/Trendo.Core/Letters.cs:485` (LibreOffice-Export, `-env:UserInstallation`) | ein Versuch, Fehler verschluckt | `TempDir.Delete` mit 25 Versuchen |
| `src/Trendo.Core/Letters.cs:540` (Tesseract-TSV) | einmal `File.Delete` | `TempDir.Delete` |
| `src/Trendo.Core/Extensions.cs:159`, `ExtensionCatalog.cs:66`, `Website/WebDesignMods.cs:297` | `Directory.Delete` (kann im finally werfen bzw. still scheitern) | `TempDir.Delete` |
| `src/Trendo.App/Program.cs:124` (App-Start) | keine Aufräumung | `TempDir.SweepInBackground()` 20 s nach Start, im Hintergrund, abschaltbar mit `TRENDO_TEMP_AUFRAEUMEN=0` |
| `tests/Trendo.{App,Core,Api}.Tests/TestTemp.cs` (neu, ModuleInitializer) | jeder Test schrieb direkt in `%TEMP%` (566 Stellen, u. a. `UiTests.cs:34` `Ui.Open` → 150 000 leere `trendo-ui-*`) | Testprozess lenkt TMP/TEMP auf `%TEMP%\trendo-testlauf-<pid>-<start>`; am Prozessende wird der ganze Ordner gelöscht; beim Start werden verwaiste Läufe (Prozess tot) und alte eigene Reste weggeräumt |
| 23 Testdateien mit festen Prüf-/Berichtsordnern (`trendo-tools-b`, `trendo-axe`, `trendo-t0705/t0725-beleg`, `trendo-ace-live`, `trendo-z3-quellen`, Bericht-TXT) | `Path.GetTempPath()` | `TempDir.SystemTemp` (echtes Temp, bleibt wie bisher erhalten) |
| `tests/Directory.Build.props` (neu) | `dotnet test --blame-hang` ohne Typ = voller Abzug (22 GB) | Standard `VSTestBlameHangDumpType=none`, `VSTestBlameCrashDumpType=mini`; ausdrückliche Angabe gewinnt |
| `tools/echtlauf/buero/t0705.py`, `fachstudios/t0725.py` | Rohordner nur am glücklichen Ende gelöscht | `atexit` räumt auch bei Fehlern |
| `tools/webdesign-editor/pruefe-*.mjs` (7) | `rmSync` einmal, Fehler verschluckt | `maxRetries: 25, retryDelay: 200` |

## Fundstellen (Liste, nur Stellen mit Wegwerf-Ordnern)
Produkt (`src/`, alle über `Path.GetTempPath()`): WebsitePreview.cs:221 (`trendo-chrome-`), Letters.cs:477/530 (`trendo-lo-`, `trendo-ocr-`), Sys/Sys03.cs:91 (`trendo-pdf-profil`, fest, wiederverwendet), Extensions.cs:117, ExtensionCatalog.cs:52, Website/WebDesignMods.cs:189, Website/WebsiteStudio.cs:81, Bewerbung.cs:240, ClaudeExtras.cs:471/504, ClaudeFallback.cs:263/275, ComicStudio.cs:263, DocScan.cs:293, Formulare.cs:236, KiLibrary.cs:408, KreativStudios2.cs:504, KreativStudios3.cs:220, Library3D.cs:68, MusikCover.cs:32, MusikCoverWerkstatt.cs:75/83/90, MusikStimme.cs:26/49, MusikTools.cs:36, MusikVerstehen.cs:77, Pdf.cs:119, PdfF3.cs:100/119, PdfOrganize.cs:123, PodcastStudio.cs:333/361/362, PodPrintful.cs:105, Praesentation.cs:285, Print3D.cs:125, ProgGameMixamo.cs:201, Programme.cs:384/421, Sys03.cs(App):127, Tabelle.cs:215, Text.cs:248/390, TextPro.cs:216/221, VideoExportSettings.cs:46, W4Studio3D.cs:429/642, W5Ext.cs:80, W5ExtCat.cs:79, WebDesign3D.cs:26/79, WebDesignAssets.cs:34/78, WebDesignCode.cs:287/288, WissenDocs.cs:23, AudioEditor.cs:31, CompanionServer.cs:155, Core: PlanPhoto.cs:70, ModelConverter.cs:73, Dependencies.cs:331, DevBuild.cs:482, LetterPdf.cs:98, MixamoBlender.cs:303, DeckScanPromptAdapters.cs:44, Studio3D.cs:78/378, Studio3DEngines.cs:99, Studio3DPro.cs:94, PdfSignOcr.cs:35, PdfWerkstatt.cs:435, Sys10.cs:542, Sewing.cs:119, XrSample.cs:12.
Die meisten haben schon try/finally; ohne finally bleiben sie höchstens bis zur nächsten Startaufräumung (24 h) liegen.
Tests: 566 Stellen in 382 Dateien (u. a. UiTests.cs:34/334/389/560, W5ExtTests.cs:8, Sys02Tests.cs:10, Sys03Tests.cs:8, Sys10Tests.cs:10/14, W7Tests.cs:9, W4MediaUiTests.cs:28, WebsitePreviewTests.cs:7, WebsiteStudioTests.cs:7, TrendoImageTests.cs:29) – alle landen jetzt im Laufordner.
Werkzeuge: tools/webdesign-editor/pruefe-*.mjs (`trendo-chrome-*-`), tools/echtlauf/buero/t0705.py:33, fachstudios/t0725.py:33, tools/echtlauf/Trendo.Echtlauf/Program.cs:21 (Audit-Datei je PID, jetzt in der Präfixliste), tools/design/build.py:35/46 (`TemporaryDirectory`, räumt selbst), tools/Trendo.Render/Program.cs:55/880 (feste Ordner, wiederverwendet).
Skripte: scripts/setup-rundlauf*.ps1 (`trendo-setup-rundlauf-*`, `trendo-t0724-setup-*`) – **bewusst nicht** in der Liste: enthalten Beleg und eine Sicherung echter Daten.
CI: `.github/workflows/trendo-os-abbild.yml` ohne `dotnet test`/blame; in scripts/, tools/ und Doku kein `--blame-hang` gefunden (nur alte Berichte T-0221/T-0222, schon mit `none`). CLAUDE.md nur gelesen.

Nicht automatisch gelöscht (Schutzliste, Manuel entscheidet): `trendo-tools-b`, `trendo-axe`, `trendo-ue-e2e`, `trendo-ps2`, `trendo-abbild-latest`, `trendo-abbild-0.*`, `trendo-ace-live`, `trendo-z3-quellen`, `trendo-render`, `trendo-auftrag-*`, `%TEMP%\claude`.

## Tests (alle mit `TRENDO_TEMP_AUFRAEUMEN=0`, damit meine Läufe nichts Fremdes in %TEMP% wegräumen)
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- Core `TempDirTests` 5/5 (u. a. gesperrte Datei: ein Versuch scheitert wie früher, mit Wiederholung gelöscht; Aufräumung mit Gegenprobe: fremd, `claude`, `trendo-auftrag-*`, `trendo-tools-b`, `trendo-ue-e2e`, `trendo-ps2`, `trendo-abbild-latest/-0.9.84`, junger eigener, alter mit frischer Datei, lebender Laufordner bleiben; alte eigene, Guid-Name, toter Laufordner, alte PDF-Datei gehen).
- Core QA019_Offline, QA019_SimulatedNoNetwork, LetterQa, LetterOcrReal, W5_X04*, NoFixedUser: 32/32 bestanden. Weitere berührte Core-Klassen (DocMarkdown, IconSets, ObjectDetect, PciIds, ResticBackup, YaraScan, ToolAdaptersReal, WebDesignStufe3*, SystemPages): 100 bestanden, 8 übersprungen (Prüfordner `trendo-tools-b` fehlt auf diesem PC – ist auch im echten Temp nicht mehr vorhanden).
- Api: 16/16.
- App: 35 Klassen (UiTests-Klassen QA017/QA004/QA019_OfflineUi/…/W1_LetterCases, FreigabenKernUi, UebersichtEinfach_Ui, alle berührten UI-/Browser-/Echtlauf-Klassen): 111 Tests, 2 rot nur im Lauf mit künstlich langem Temp-Pfad (LibreOffice scheitert still bei langen Profilpfaden); Wiederholung mit echtem Temp: TempAufraeumen_Ui, BueroEchtlauf_Ui (echtes LibreOffice/qpdf), WebDesignStufe2/3_Browser, WebsitePreviewTests → 16 Tests, 0 Fehler, 3 übersprungen (axe-core nicht eingerichtet, PC-Lauf, Kind-Test).
- `TempAufraeumen_Ui.Typical_run_leaves_nothing` (echt): startet das Testprogramm als Kindprozess mit privatem „Temp“, dort Ui.Open + Schließen, echte Chrome-Vorschau, echter LibreOffice-PDF-Export; danach ist der private Ordner leer.
- Hang-Dump: `dotnet test tests/Trendo.Api.Tests --blame-hang --blame-hang-timeout 2s` → Hänger ausgelöst, Lauf abgebrochen, 0 Byte Abzug, keine .dmp in %TEMP%.
- `python -m unittest discover -s tests/claude_handoff` → 11 OK.

## Nicht geprüft / offen
- App-Start-Aufräumung nur über die Kernfunktion getestet, nicht in der installierten App beobachtet.
- „Datenordner-Temp“ für die App NICHT umgesetzt: der Datenordner liegt in `%APPDATA%` (Roaming) – 70-MB-Chrome-Profile gehören dort nicht hin. Die App nutzt weiter `%TEMP%`, aber mit Aufräumung.
- Laufordner verlängert Temp-Pfade in Tests um ~30 Zeichen; LibreOffice ist empfindlich bei langen Pfaden (mit echtem Temp grün).
- Bei hartem Abbruch eines Testprozesses bleibt sein Laufordner bis zum nächsten Teststart (dann sofort weg, da Prozess tot) bzw. App-Start.
- Kein Volltest (macht die Hauptsitzung).

**Status:** umgesetzt, gezielt getestet, gepusht (siehe Abgabe).
