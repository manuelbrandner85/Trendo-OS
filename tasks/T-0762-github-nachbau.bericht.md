# T-0762 – Bericht: Projekte von GitHub holen (X-06) und Programme nachbauen (X-10)

**Zweig:** `claude/T-0762-github-nachbau` (Basis main, Release 0.9.109) · freigegeben vom Nutzer (Restliste Rang 38+39)

## Geänderte Dateien
- `src/Trendo.Core/GitHubAppsInstallers.cs` – `Ampel`, `InstallerCheck`, `CheckInstaller` (Authenticode über `Sys/Authenticode.cs` + SHA-256; Grün = gültig signiert, Gelb = nicht prüfbar, Rot = unsigniert/ungültig, fehlend = gesperrt).
- `src/Trendo.App/MainWindow.Programme.cs` – der Installer-Zweig von `GitHubInstall` führt nichts mehr aus, sondern hält den Installer an (`GitHubHoldInstaller`).
- `src/Trendo.App/MainWindow.GitHubPlus.cs` – wartender Installer: Hinweis-Karte mit Ampel, Herausgeber, SHA-256; „Installer ausführen“ ist ein eigener Klick; Gelb/Rot erst nach eigenem Häkchen; vor dem Lauf erneuter SHA-256-Vergleich (veränderte Datei wird abgewiesen); „Nicht ausführen (löschen)“; Protokoll `github.installer.held/risk/consent/declined`.
- `src/Trendo.Core/OpenSource.cs` – `BuildHints` (dotnet, npm inkl. Skripte, cargo, go, Python mit Umgebung im Projektordner, CMake, make, Gradle, Maven; Werkzeug vorhanden ja/nein), `Remove` (Ordner inkl. schreibgeschützter Git-Dateien und Herkunftsdatei), `OriginFile`.
- `src/Trendo.App/MainWindow.W4Oss.cs` – Karte „Installer wartet“ oben; geholte Projekte zeigen die erkannte Bauanleitung und „Im Code-Editor“; `OssOpenInCode`; Bauanleitung als Knöpfe im Terminal des Code-Editors (`CodeHintsBar`); Entfernen über `OpenSource.Remove` (schließt das Projekt vorher im Code-Editor).
- `src/Trendo.App/MainWindow.CodeEditor.cs` – eine Zeile: Bauanleitung im Terminal-Reiter.
- `src/Trendo.Core/PromptAction/CapabilityCatalog.cs` + `docs/improvement-42/studio-capability-matrix.md` – X-06-Text an die Wirklichkeit angepasst (KI-Auftrag klont nicht, das Studio schon; Code-Editor; Installer-Prüfung). Matrix mit `TRENDO_UPDATE_MATRIX=1` neu erzeugt (1 Zeile).
- `src/Trendo.Core/CleanRoomRequirements.cs` (neu) – `Requirement` (ID, Funktion, erwartetes Verhalten, Prüfung, Quelle, Datum, Stufe, Status, Test, Abgleich), Laden/Speichern mit `AtomicFile` (`1-Beschreibung/anforderungen.json`), `RequirementsDoc` mit Rückgängig (jede Änderung sofort gespeichert), Markdown-Export, `ResolveLink` (nur innerhalb des Projekts), `Reconcile`/`ReconcileAll`, `RunTests` (echter Testlauf in „2-Umsetzung“ über `DevBuild`), `MatchLines` (gleiche Abschnitte je Zeile).
- `src/Trendo.Core/CleanRoom.cs` – Prüfbericht: Anforderungstabelle, jeder Eintrag mit Herkunft und Datum (Treffer, Tagebuch, Anforderungen), nur Ordnernamen statt voller Pfade; Speichern mit `AtomicFile`.
- `src/Trendo.App/MainWindow.W5CleanRoom.cs` – neuer Schritt „2 Anforderungen“: Tabelle (je Anforderung zwei Zeilen: WAS / WOHER), „＋ Anforderung“, „↶ Rückgängig“ (auch Strg+Z außerhalb eines Textfelds), „Abgleichen (Tests laufen lassen)“, „Als .md exportieren“, Rechtsklick (Status, Code-Editor, Löschen), Datei auf das Test-Feld ziehen = verknüpfen, „↗“ öffnet die verknüpfte Datei im Code-Editor; Umsetzung „Im Code-Editor öffnen“; Treffer der Ähnlichkeitsprüfung anklickbar und nebeneinander (links eigener Code, rechts Vergleichstext, gleiche Zeilen orange).
- `tools/Trendo.Render/Program.cs` – Vorführzustände `TRENDO_NACHBAU_DEMO`, `TRENDO_OSS_DEMO` für Bildprüfung.
- Tests: neu `tests/Trendo.Core.Tests/T0762GitHubNachbauTests.cs` (9), neu `tests/Trendo.App.Tests/T0762GitHubNachbauUiTests.cs` (3 + PC-Lauf); angepasst `GitHubUsableUiTests.Installer_is_usable` (verlangt jetzt Ampel + eigene Bestätigung – vorher lief der Installer im selben Klick; verschärft, nicht geschwächt).
- Beleg: `reports/T-0762/x06-echtlauf.log`, `x06-echtlauf.sha256`.

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- Core gezielt (`T0762|W5_X10|GitHub|W4Oss|PromptActionTests|SystemPagesTests|NoFixedUser|DevBuild|Authenticode`): **Bestanden, 132 von 132**. Darin echt: Authenticode auf `notepad.exe` (Grün) und auf selbst geschriebener Datei (Rot), echter Python-unittest-Lauf für den Abgleich.
- App gezielt: `T0762_GitHubNachbau_Ui` 4 (3 bestanden, PC-Lauf ohne Variable übersprungen), `W5_X10_Ui` 1, `GitHubUsableUiTests` 3, `GitHubNet_Ui` 3, `GitHubPlus_Ui` 4, `GitHubProgramsUiTests` 2, `T0711_HuelleEtappe1b_Ui` 11, `CodeEditor_Ui` 1, `PromptBarTests` 6 – alle 0 Fehler. Darin echt: `dotnet build` eines Projekts aus „Open Source“ über den Bauanleitungs-Knopf (Exit 0, Hallo.dll entsteht), Python-Testlauf im Nachbau-Abgleich.
- PC-Lauf `TRENDO_PC_LAUF=1 … -method …Pc_run_real_github` → bestanden (12 s). Rohlog `reports/T-0762/x06-echtlauf.log`, SHA-256 `3d28ec67f8e85f80d94c9e44017fe51d205578b0ccd0844b2b27f45a3ff267f3 (Stand fix2)`.
- Rot-Grün: die neuen Tests bauen auf altem Code nicht (Methoden fehlen); `Installer_is_usable` schlägt auf altem Code fehl (Installer lief sofort).
- Kein Volltest (macht die Hauptsitzung).

## PC-Lauf X-06 (echt, öffentliche GitHub-API ohne Anmeldung, isolierter Trendo-Datenordner)
Suche „ripgrep“ → `BurntSushi/ripgrep` (Unlicense) → `GitHubInstall` lädt `ripgrep-16.0.0-x86_64-pc-windows-msvc.zip`, SHA-256 `f418ae83…c73931` = GitHub-Angabe, entpackt in `werkzeuge/gh-burntsushi-ripgrep` → `rg --version` (Exit 0) und ein zweiter, wartender Start; dieser eigene Prozess per PID beendet → dieselbe echte Datei mit falscher Prüfsumme über den App-Weg: abgewiesen, Werkzeugordner unverändert → „Entfernen“ → „Wirklich entfernen?“ in der Oberfläche: Ordner weg, Liste leer, kein `gh-*` übrig. Danach Suche „itsdangerous“ → „Quellcode holen“ (`git clone --depth 1`, BSD-3-Clause, mit Herkunftsdatei) → „Im Code-Editor“ → erkannte Bauanleitung `python -m compileall -q .` → Knopf → Exit 0 → Entfernen: Ordner „Open Source“ leer (auch keine Herkunftsdatei).

## Abnahme (Audit)
- X-06: freies Projekt geholt, im Code-Editor geöffnet und gebaut ✔ (echt: Python-Projekt von GitHub mit compileall; dotnet-Bau echt nur mit lokalem Projekt); falsche Prüfsumme abgewiesen ✔ (echte Datei); Entfernen ohne Rest ✔ (echt).
- X-10: jede Anforderung hat Status und verknüpften Test ✔; Vergleich nebeneinander ✔; Bericht mit Herkunft und Datum ✔.

## Abnahmekriterien 1–6
1. Leere Vorlage → echtes Ergebnis: **VOLLWERTIG** für X-10 (Projekt → Anforderungen → Abgleich → Bericht/Export über die Oberfläche); X-06 entsprechend Holen/Einrichten.
2. Fremddatei importieren/exportieren, im Fremdprogramm öffnen: **TEILWEISE** – echtes GitHub-Programm geholt und gestartet, echter Quellcode gebaut; der .md-Export wurde nicht in einem Fremdprogramm geöffnet.
3. Absturz verliert ≤ 30 s: **VOLLWERTIG** für die Anforderungsliste (jede Änderung sofort per `AtomicFile`); Prüfbericht ebenfalls atomar.
4. Claude/KI: **FEHLT** in diesem Paket (nicht beauftragt; KI-Auftrag X-06 erklärt weiterhin nur die Lizenz).
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** (UI-Tests bis Dateien/Build, PC-Lauf echt).
6. Bedienung: **TEILWEISE** – Strg+Z, Rechtsklick, Ziehen und Ablegen auf das Test-Feld vorhanden; Ziehen/Ablegen und Strg+Z nicht im Test gedrückt; keine eigenen Einfach/Profi-Unterschiede.

## Nicht geprüft / ersetzt
- Echter Installer-Lauf (NSIS/Inno/Squirrel) nicht ausgeführt – in den UI-Tests ist der Installer-Lauf ersetzt (`GitHubInstallerRunOverride`); die Signaturprüfung selbst ist echt (WinVerifyTrust). Grün im UI-Test über `GitHubSignatureOverride`, Grün echt nur im Core-Test mit `notepad.exe`.
- Sperrlisten (Widerruf) fragt die Authenticode-Prüfung nicht ab (so in `Authenticode.cs` angelegt) – im Hinweis ehrlich genannt.
- cargo, go, cmake, make fehlen auf dem PC: diese Bauanleitungen nur als erkannte, gesperrte Vorschläge geprüft.
- Bildprüfung: Seiten „Programme nachbauen“ (Profi) und „Projekte von GitHub holen“ mit wartendem Installer gerendert und angesehen; Code-Editor mit Bauanleitungs-Leiste nicht gerendert (nur im UI-Test).

## Offene Punkte
- Ausdrückliche Risiko-Bestätigung ist ein Häkchen + eigener Klick auf derselben Karte; ein eigener Dialog über `FreigabeConfirm` wäre möglich.
- X-10: Testlauf nur für Projekte, die `DevBuild` kennt (.NET, Python, Node).

**Status:** umgesetzt, gezielt getestet, PC-Lauf belegt; Volltest steht aus.

## Nachbesserung nach unabhängiger Gegenprüfung (fix1, auf @2aa2f951d)
| Befund | Änderung | Beleg/Test |
|---|---|---|
| B1 fremder Code beim Bauen | Python-Vorschläge mit `python -I -m …` (auch `DevBuild` „Bauen“: `-I -X utf8`); Terminal des Code-Editors mit `NoDefaultCurrentDirectoryInExePath=1` (keine `python.bat`/`.exe` aus dem Projekt); fester Hinweis „Bauen führt Code aus diesem fremden Projekt mit deinen Rechten aus.“; erster Bau-Lauf je geholtem Projekt nur nach eigener Bestätigung („Ja, im Projekt ausführen“, vermerkt in der Herkunftsdatei, nicht im selben Klick); Tooltips ehrlich (npm-Installationsskripte, MSBuild-Ziele, build.rs, CMake/Makefile, gradlew aus dem Projekt, Maven) | Rot-Grün `B1_project_module_not_run` (präparierte `compileall.py` + `python.bat`: mit `-I` kein Marker, ohne `-I` Marker – echt mit Python/cmd), `B1_honest_hints_and_approval`, UI `Fetched_project_in_code_editor` (Klick fragt nur, Abbrechen, Bestätigen, echter dotnet-Bau), PC-Lauf |
| B2 unsignierte Programme starteten sofort | `GitHubLaunch`: gültig signiert oder schon erlaubt (an SHA-256 gebunden) → Start; sonst Karte „Erster Start“ mit Ampel, Gelb/Rot erst nach Häkchen, eigener Klick. Alle Start-Knöpfe (Open Source, Programme, Hinzufügen) gehen darüber. Nach bestätigtem Installer-Lauf gilt die Zustimmung für das so eingerichtete Programm (an seine Prüfsumme gebunden). Neues optionales Feld `startFreigabe` in `github-programme.json` (alte Dateien lesbar) | UI `B2_unsigned_program_first_start` (echte Authenticode-Prüfung Rot; Grün-Zweig mit ersetzter Signatur), `B2_approve_start` |
| B3 Abgleich | Knopf während des Laufs gesperrt, „■ Abbrechen“ (Abbruchsignal, `Cli.RunStreamAsync` beendet den Prozessbaum), Liste vor dem Warten festgehalten, Hinweis „führt Code aus „2-Umsetzung“ mit deinen Rechten aus“ | `B3_cancel_test_run` (Core, echt), UI `B3_reconcile_lock_switch_cancel` (Projektwechsel während des Laufs, Abbrechen, echt mit Python) |
| B4 kaputte Liste | Meldung in der Seite; vor dem ersten Speichern Kopie `anforderungen.defekt-<Zeit>.json` | `B4_broken_file_backed_up`, UI `B4_broken_list_shown` |
| B5 TOCTOU | Installer und Programmdatei bleiben vom Prüfen bis zum Start mit `FileShare.Read` offen (Hash aus dem gesperrten Datenstrom, erneuter Vergleich vor dem Start) | `B5_locked_file` (Ändern/Umbenennen scheitert, Start geht – echt mit where.exe), UI `Green_installer_own_click_and_tamper` |
| B6 Knopf ↔ Karte | `GitHubConfirmInstaller(p)` / `GitHubConfirmStart(p)` prüfen `ReferenceEquals` | UI `Green_installer_own_click_and_tamper` (alte Karte führt nichts aus) |
| B7 Junction | Entfernen geht selbst durch den Baum, löst Verknüpfungen einzeln, betritt sie nie | `B7_junction_not_followed` (echte Junction per mklink /J, Ziel samt Schreibschutz unverändert) |
| B8 Datei#Test | Treffer nur, wenn Testname UND Datei (Ergebnisdatei) bzw. Modulname passen | `B8_same_name_other_file` |
| B9 Kennzeichnung | DisplayName „(UI, Signatur ersetzt, Installer-Lauf ersetzt)“ | – |

**Ausdrücklich:** Ein echter Installer-Lauf (NSIS/Inno/Squirrel) ist NICHT belegt – in allen Tests ist der Installer-Lauf ersetzt (`GitHubInstallerRunOverride`); echt sind nur die Signaturprüfung, die Sperre und die Zustimmungslogik.

Tests nach der Nachbesserung: Build 0 Fehler. Core gezielt (`T0762|W5_X10|GitHub|W4Oss|PromptActionTests|SystemPagesTests|NoFixedUser|DevBuild|Authenticode|CodeEditor|DevStudio`) 143/143, `T0688` 7 + 2 übersprungen. App: `T0762_GitHubNachbau_Ui` 7 (6 bestanden, PC-Lauf separat bestanden), `GitHubUsableUiTests` 3, `W5_X10_Ui` 1, `GitHubNet_Ui` 3, `GitHubPlus_Ui` 4, `GitHubProgramsUiTests` 2 (zuerst rot: wartender Erststart sperrte die Datei beim Entfernen – Entfernen und Neuinstallation geben die Sperre jetzt frei), `T0711_HuelleEtappe1b_Ui` 11, `CodeEditor_Ui` 1, `PromptBarTests` 6 – 0 Fehler. PC-Lauf neu: SHA-256 `3e5456a55f131f621ec14c0ad1035a0cbf864f19ac3c8549d4372116619bce05`.
Zusätzlich berührt: `src/Trendo.Core/GitHubApps.cs` (Feld `startFreigabe`), `src/Trendo.Core/DevBuild.cs` (`-I -X utf8`), `src/Trendo.App/MainWindow.AddHub.cs` (Start/Entfernen).

## Zweite Prüfrunde (fix2, auf @434f42894)
| Befund | Änderung | Test/Beleg |
|---|---|---|
| N1 gradlew nach Schutz-Variable nicht auffindbar | Projektskripte mit ausdrücklichem Pfad: `.\gradlew.bat build` / `./gradlew build`; Maven-Wrapper `.\mvnw.cmd package` (sonst `mvn package`); nur Unix-`gradlew` unter Windows = gesperrt | `N1_local_wrappers_resolve` (Dummy-`gradlew.bat`/`mvnw.cmd`, echt über cmd mit `NoDefaultCurrentDirectoryInExePath=1`; ohne `.\` nicht gefunden) |
| N2 Grün nur für die .exe | `CheckProgramFolder`: Grün nur, wenn Startdatei UND alle ausführbaren Inhalte des Programmordners (.exe/.dll/.asar/.node/Skripte …) gültig signiert sind, sonst Gelb mit Dateinamen; mehr als 1500 → Gelb. Erlaubnis an `FolderHash` über alle ausführbaren Inhalte gebunden; jede Änderung fragt erneut. Auch die Zustimmung nach einem Installer-Lauf speichert die Ordner-Prüfsumme | `N2_folder_check`, UI `B2_unsigned_program_first_start` (neue DLL → erneute Frage; Startdatei signiert + DLL nicht → Gelb), Bild der Karte geprüft |
| N3a Code-Editor umging B1 | `CodeMayStart` fragt bei Ordnern in „Open Source“ ohne Freigabe zuerst (Ausführen, Bauen, Testen, Paket bauen) – dieselbe Rückfrage-Karte, auch im Reiter „Ausgabe“; Bestätigung führt genau die gewählte Aktion aus | UI `N3a_code_editor_asks_for_oss` (direkt geöffnet, Ausführen fragt, Abbrechen, Bauen fragt, Bestätigen → echter Python-Bau, danach keine zweite Frage), Bild geprüft |
| N3b KI-Quellen / Bambu Studio | Startstellen über `GitHubGate` (Startdatei gesperrt, Start mit den ursprünglichen Argumenten nach Bestätigung): `KiSourceStart` (eine Zeile + Feld), Bambu „In Bambu Studio öffnen“ und „Vorbereiten“ | `KiSourceSetupUiTests.Portable_from_github` (unsignierter KoboldCpp-Start wird angehalten, nach Bestätigung startet der Server, danach ohne Rückfrage). Bambu: nur gebaut – die Startstellen laufen im isolierten Testfenster nie (`!_opt.Isolated`), daher NICHT GETESTET |
| N4 PC-Lauf | Start über die Oberfläche: „Starten“ unter Programme → Karte „Erster Start“ (Rot, nicht signiert) → Häkchen → „Trotzdem starten“; der Test-Starter des Testfensters übergibt den Pfad, genau diese Datei startet der Lauf als eigenen Prozess und beendet ihn per PID | neuer Rohlog, SHA-256 `3d28ec67f8e85f80d94c9e44017fe51d205578b0ccd0844b2b27f45a3ff267f3` |
| N5 A→B→A überschrieb Bearbeitungen | eine Anforderungsliste je Projektordner (zwischengespeichert); Abgleich wendet Status/Grund auf die aktuelle Liste an | UI `N5_switch_back_keeps_edits` (während des Laufs bearbeitet und ergänzt – bleibt, Status kommt hinzu) |
| N6 Karte von „Programme“/„Hinzufügen“ | Tor springt zur Karte („Projekte von GitHub holen“) | UI `B2_unsigned_program_first_start` (Klick auf „Starten“ unter Programme → Seite mit Karte) |

Tests fix2: Build 0 Fehler. Core `T0762` 19/19. App: `T0762_GitHubNachbau_Ui` 9 (8 bestanden, PC-Lauf separat bestanden), `KiSourceSetupUiTests` 2, `GitHubUsableUiTests` 3, `GitHubProgramsUiTests` 2, `GitHubNet_Ui` 3, `GitHubPlus_Ui` 4, `CodeEditor_Ui` 1, `BambuPro_Ui` 1, `W5_X10_Ui` 1 – 0 Fehler. Render-Vorführungen neu: `TRENDO_OSS_DEMO=start`, `TRENDO_CODE_OSS_DEMO=1`.
Zusätzlich berührt: `src/Trendo.App/MainWindow.KiSourceSetup.cs` (nur Startstelle, T-0764 ändert die Datei parallel), `MainWindow.Print3D.cs`, `MainWindow.Print3DPro.cs`, `tests/Trendo.App.Tests/KiSourceSetupUiTests.cs`.
