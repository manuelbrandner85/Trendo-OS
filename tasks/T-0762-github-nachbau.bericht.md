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
- PC-Lauf `TRENDO_PC_LAUF=1 … -method …Pc_run_real_github` → bestanden (12 s). Rohlog `reports/T-0762/x06-echtlauf.log`, SHA-256 `41063c50721076987c7c7578e36fb804922a2f2008603e349eac781916b3be89`.
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
