# T-0757 Bericht: Niedrige Restbefunde aus den Prüfungen von 0.9.108

**Auftrag:** Restbefunde aus `tasks/T-0752-konsolenspiele.bericht.md`, `tasks/T-0754-dateien-schutz.bericht.md`, `tasks/T-0755-installieren-surfen-gefahren.bericht.md` – freigegeben am 23.11.2026.
**Branch:** `claude/T-0757-restpunkte-0.9.108`, Basis main 24bb4f70e (Release 0.9.108).

## Was war – was ist jetzt – Beleg

| Punkt | Vorher (Rot auf main) | Jetzt | Test |
|---|---|---|---|
| R1 Lizenz | „Darf nicht weitergegeben werden. Kopieren verboten.“ + voller MIT-Satz galt als MIT | Zusätzliche harte Ausschlussgründe: nicht weitergegeben, darf nicht weitergegeben, confidential, vertraulich, all copying prohibited, kopieren verboten. 10 echte Lizenztexte aus dem NuGet-Cache (MIT 6, BSD 1, Apache-2.0 2, zlib 1) gelten weiter | `T0757RestpunkteTests.R1_mit_in_unfreier_datei` (rot auf main), `R1_echte_lizenzen_aus_nuget` (echte Dateien) |
| R1 Bestätigung | „Eigenes Homebrew“ startete ohne Bestätigung | Haken „Ich habe die Rechte an diesem Programm“ (`fp-rights`), gilt nur für die gewählte ELF-Datei; ohne Haken BLOCKIERT, auch beim direkten Aufruf von `RunAsync`. In `ergebnis.json` steht `rechte_bestaetigt`, im Protokoll „Bestätigt“ | `T0757NeuTests.R1_bestaetigung`, `ConsolePort_Ui.Free_port_click_to_result` (erweitert), Bilder `eigen-*.png` |
| R2 SYSTEM.CNF | `BOOT2 = cdrom0:\MEINSPIEL.ELF;1` eines eigenen Projekts wurde als Spiel-Disc abgewiesen | Nur abweisen, wenn BOOT2 auf eine Disc-Kennung zeigt (`cdrom0:\\?(S[CL][AEKPU][ASMJDH]\|P[ABC]PX\|T[CL][EPU]S)[_-]`). Neutrale Meldung: „SYSTEM.CNF, deren Startprogramm (BOOT2) die Kennung einer Spiel-Disc trägt“ | `R2_eigene_system_cnf` (rot auf main) |
| R3 Liste | untergeschobene `trendo-einsortiert.txt` mit richtiger Prüfsumme löschte eine PS2Recomp-Datei | Kopie der Liste im WorkDir; gelöscht wird nur, was in beiden Listen mit gleicher Prüfsumme steht. Liegt eine Datei nur in der Liste im PS2Recomp-Ordner noch vor, wird nichts gelöscht und nichts gemischt (Fehler mit Hinweis) | `R3_untergeschobene_liste_loescht_nichts` (rot auf main), `T0757NeuTests.R3_beide_listen` |
| B Formel | ohne MaxCapacity 5 % des Laufwerks | `WindowsDefaultBinBytes`: min(40 GB, Größe)·10 % + max(0, Größe − 40 GB)·5 %; Größe über GetDiskFreeSpaceEx des Laufwerks | `T0757NeuTests.B_formel`; Echttest übersprungen (MaxCapacity auf diesem PC gesetzt – Wert 49 233 MB = genau die Formel, siehe BELEG) |
| B Reparse-Point | Junction als Element: Inhalt des Ziels mitgezählt → „größer als der Papierkorb“ | Größe 0 für Junction/Symlink als Element | `B_junction_groesse_null` (echte Junction, rot auf main) |
| B Mount-Point | Laufwerk über GetPathRoot (bei eingehängtem Laufwerk falsch: C:) | `VolumePath` (GetVolumePathName) für Einstellungen, Laufwerksart (GetDriveType), Größe, $Recycle.Bin-Suche und `IsTrashLocation`; schlägt GetVolumeNameForVolumeMountPoint fehl, gilt das allgemeine NukeOnDelete. Registry nur gelesen | `T0757NeuTests.B_groesse_und_laufwerk` (echt, nur lesend); echter Mount-Point NICHT GETESTET |
| C `^` | `cmd /c p^owershell -enc`, `powershell^.exe`, `-e^nc`, Base64 mit `^` nicht rot | Startet cmd (oder steht cmd in den Argumenten), werden vor der Prüfung alle `^` entfernt | `C_caret_rot` (rot auf main) |
| C Fehlalarm | `cmd /c echo powershell -e …` und `dotnet tool --shell powershell -e …` rot „Versteckter Befehl“ | Innere Prüfung nur, wenn das Startprogramm cmd, conhost, forfiles, explorer oder wmic ist und PowerShell direkt nach /c, /k, start (mit Schaltern/Titel), &, \|, --headless, „create“ (wmic) oder am Anfang steht | `C_kein_fehlalarm` (rot auf main), `T0757NeuTests.C_nur_startprogramme`; alle T0755-Fälle bleiben rot |
| C Echtlauf | 174 Einträge, Rot 15 / Gelb 58 / Grün 101 | unverändert 174, Rot 15 / Gelb 58 / Grün 101, gleiche Gründe | `reports/T-0757/spec12-systempruefung-echt.txt` (echt, nur Zählungen) |

## Tests (Zahlen aus der Ausgabe)
- Rotlauf auf main 24bb4f70e: `T0757RestpunkteTests` 8 Tests, 6 Fehler, 2 bestanden (`reports/T-0757/rot-auf-main-24bb4f70e-core.log`). `T0757NeuTests` übersetzt auf main nicht (neue Schnittstellen).
- Build `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core (T0757*, FreePortTests, ConsolePortTests, T0754*, T0755*, NoFixedUser, OsBehaviorTests, W7Tests): 100, bestanden 96, übersprungen 4 (nur Linux), Fehler 0.
- App ConsolePort_Ui, T0754DateienSchutzUi, OsStyles_Ui, T0755_Ui, SPEC12_Ui: 22/22. App W7_Ui, Sys03_Netz_Ui, PhoneEasyTests: 14, 13 bestanden, 1 übersprungen (nur Linux).

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis: TEILWEISE (Portierlauf bis ergebnis.json mit ersetzten Werkzeugen; echter Lauf weiter BLOCKIERT – PS2Recomp und CMake fehlen).
2. Fremddatei importieren/exportieren: NICHT GETESTET (nicht Teil des Pakets); echte Fremddateien nur als Prüfeingabe (NuGet-Lizenzen, Junction, Windows-Einstellungen).
3. Prozessabbruch ≤ 30 s: unverändert (Protokoll alle 3 s, AtomicFile auch für die neue Liste).
4. Claude/KI: NICHT GETESTET (nicht Teil des Pakets).
5. Klick bis Datei: TEILWEISE (UI-Test Klick → Haken → ergebnis.json mit `rechte_bestaetigt`, Werkzeuge ersetzt).
6. Einfach/Profi, Rückgängig: TEILWEISE (Haken in beiden Stufen, rückgängig machbar über die Hülle; kein neues Tastenkürzel/Ziehen).

## Nicht geprüft
- Echtes als Ordner eingehängtes Laufwerk (gibt es auf diesem PC nicht, anlegen bräuchte Admin).
- Echter Zweig „ohne MaxCapacity“ auf diesem PC (MaxCapacity gesetzt).
- Echter PS2Recomp-Lauf (BLOCKIERT wie in T-0752).

## Offene Punkte / Risiken
- R3: Die Kopie der Liste liegt im WorkDir je Titel (`<Projekt>/frei/<Titel>`). Wer im selben PS2Recomp-Ordner erst einen Titel und dann einen anderen (oder ein anderes Projekt) portiert, bekommt jetzt einen Fehler mit Hinweis statt still gelöschter oder gemischter Dateien – PS2Recomp frisch einrichten oder im ursprünglichen Projekt weiterarbeiten.
- C: `^` wird auch innerhalb von Anführungszeichen entfernt (cmd nimmt sie dort wörtlich) – das kann höchstens zusätzlich rot machen, im Echtlauf gab es keinen Fall.

## Geänderte Dateien
`src/Trendo.Core/ConsolePort/FreePort.cs`, `src/Trendo.App/MainWindow.FreePort.cs`, `src/Trendo.Core/Shell/FileBrowser.cs`, `src/Trendo.Core/Sys/Gefahren.cs`, `tools/Trendo.Render/Program.cs` (Schalter `TRENDO_FP_TITLE`), `tests/Trendo.Core.Tests/FreePortTests.cs` (Hilfsfunktion mit Bestätigung, Distribute mit WorkDir, zweiter Lauf im selben WorkDir), `tests/Trendo.Core.Tests/T0757RestpunkteTests.cs` (neu), `tests/Trendo.Core.Tests/T0757NeuTests.cs` (neu), `tests/Trendo.App.Tests/ConsolePortUiTests.cs`, `reports/T-0757/*`, `tasks/T-0757-restpunkte-0.9.108*.md`.

**Status:** Alle Restpunkte umgesetzt und getestet; Einschränkungen siehe „Nicht geprüft“.

## Gegenprüfung behoben (Stand nach d456edd43)

| Befund | Lösung | Test (Rot auf d456edd43, grün jetzt) |
|---|---|---|
| HOCH 1 Rückschritt Gefahren: Positivliste der Startstellen machte `cmd /c call/if/for … powershell`, `cmd /r`, `/q/c`, `start /D …`, `start /affinity 1`, `start/min`, Pfade mit Leerzeichen in Anführungszeichen, `conhost --headless --width …`, `forfiles /c "cmd /c call powershell"` gelb; `@ , ; /cpowershell` waren auch auf main gelb | Zurück zur breiten Erkennung von main: PowerShell irgendwo in den Argumenten, Grenzen sind Leerraum, Anführungszeichen, Pfadtrenner, `& \|` und die cmd-Trenner `@ , ; = ( )`, auch direkt hinter `/c /k /r`. Vorher werden alle `^` entfernt. Ausgenommen ist nur echte Nicht-Ausführung: `echo`/`rem` im selben Befehlsabschnitt und PowerShell als Wert von `--shell` | `T0757Fix1Tests.Hoch_cmd_varianten_rot` (20 Befehle), `T0757NeuTests.C_breit_mit_ausnahmen` |
| MITTEL 2 nur 5 Startprogramme | Keine Liste der Startprogramme mehr; `mshta vbscript:…Run ""powershell -enc …""`, `pcalua -a powershell.exe -c "-enc …"`, `rundll32 shell32.dll,ShellExec_RunDLL powershell -enc …`, `bash -c "powershell -enc …"`, `node -e "…exec('powershell -enc …')"` sind rot; `echo`, `rem`, `--shell powershell`, `node -e <Base64>` bleiben nicht „Versteckter Befehl“ | `T0757Fix1Tests.Mittel_andere_startprogramme_rot` |
| MITTEL 2 Echtlauf | erneut gelaufen: 174 Einträge, Rot 15 · Gelb 58 · Grün 101, Gründe gleich – keine neuen Fehlalarme | `reports/T-0757/fix1-spec12-systempruefung-echt.txt` (echt, nur Zählungen) |
| MITTEL 3 zweiter Titel dauerhaft blockiert, erst nach 4 Schritten | Kopie der Liste je PS2Recomp-Ordner in Trendos Datenordner (`<Daten>/konsolenspiele/ps2recomp-listen/einsortiert-<SHA-256 des Pfads>.txt`, `FreePort.ListCopy`), unabhängig von Titel, Projekt und Arbeitsordner. Fremde Reste prüft schon die Voraussetzung „PS2Recomp ohne fremde Reste“ – dann startet nichts. Neue Aktion „Alte Trendo-Dateien aus PS2Recomp entfernen“ mit Rückfrage („Ja, entfernen“ / „Abbrechen“); sie entfernt nur Dateien, deren SHA-256 in beiden Listen steht (`FreePort.RemoveOld`) | `T0757Fix1Tests.Mittel_zweiter_titel` (Arbeitsordner gelöscht, zweiter Titel läuft durch), `Mittel_konflikt_vor_dem_start` (0 Aufrufe), `T0757NeuTests.R3_beide_listen`, `ConsolePort_Ui.Free_port_click_to_result` (Rückfrage, Abbrechen, Ja; fremde Datei bleibt) |
| NIEDRIG 4 Lizenzwörter | zusätzlich „Weitergabe verboten“, „Do not distribute“, „Not for redistribution“, „Redistribution prohibited“, „For internal use only“, „Vervielfältigung untersagt“, „End User License Agreement“; Verneinung vor Lizenzkennzeichen („NOT CC0“, „nicht unter CC0“, „not licensed under the MIT license“; MIT nur in Großbuchstaben, damit das deutsche „nicht mit“ nicht greift). Die 10 echten NuGet-Lizenztexte gelten weiter | `T0757Fix1Tests.Niedrig_lizenzwoerter_und_verneinung`, `T0757RestpunkteTests.R1_echte_lizenzen_aus_nuget` |
| NIEDRIG 5 Haken an Pfad gebunden | `fp_rights` speichert die SHA-256 der ELF; eine geänderte Datei verlangt eine neue Bestätigung („Das Programm hat sich seit deiner Bestätigung geändert …“) | `ConsolePort_Ui.Free_port_click_to_result` (ELF verändert → BLOCKIERT) |
| NIEDRIG 6 BOOT2 mit Unterordner | `cdrom0:` mit beliebigen Unterordnern und mehrfachen `\` bzw. `/` vor der Disc-Kennung | `T0757Fix1Tests.Niedrig_boot2_unterordner` |
| NIEDRIG 7 Schutzprüfungen und Hinweistext | Pfad-Regel, Prüfsumme und Präfix einzeln getestet, jeweils mit dem Eintrag in BEIDEN Listen (sonst würde schon der Listenvergleich schützen). Hinweis jetzt „BLOCKIERT – Rechte am Programm: noch nicht bestätigt. Setze unten bei „1 · Quelle und Lizenz“ den Haken …“ | `T0757NeuTests.Manifest_pfadregel`, `Manifest_pruefsumme`, `Manifest_praefix`; Bild `fix1-eigen-einfach.png` |

Rot-Grün:
- `T0757Fix1Tests` auf d456edd43: 6 Tests, 6 Fehler (`reports/T-0757/fix1-rot-auf-d456edd43-core.log`).
- Core (T0757*, FreePortTests, ConsolePortTests, T0754*, T0755*, NoFixedUser, OsBehaviorTests, W7Tests): 109, bestanden 105, übersprungen 4 (nur Linux), Fehler 0 (`fix1-gruen-core.log`).
- App ConsolePort_Ui, T0755_Ui, SPEC12_Ui: 8/8 (`fix1-gruen-app.log`). FileBrowser wurde in Fix1 nicht geändert; T0754DateienSchutzUi und OsStyles_Ui deshalb nicht erneut.

Geändert in Fix1: `src/Trendo.Core/Sys/Gefahren.cs`, `src/Trendo.Core/ConsolePort/FreePort.cs`, `src/Trendo.App/MainWindow.FreePort.cs`, `tests/Trendo.Core.Tests/T0757Fix1Tests.cs` (neu), `tests/Trendo.Core.Tests/T0757NeuTests.cs`, `tests/Trendo.App.Tests/ConsolePortUiTests.cs`, `reports/T-0757/fix1-*`, dieser Bericht.

Offen nach Fix1:
- Der Offene Punkt „R3 … Fehler mit Hinweis“ oben ist überholt: Ein zweiter Titel oder ein zweites Projekt im selben PS2Recomp-Ordner läuft jetzt durch. Gesperrt bleibt nur, was Trendo nicht über beide Listen belegen kann (z. B. nach Löschen des Datenordners) – dann schon vor dem ersten Schritt, mit Hinweis.
- Die breite Erkennung kann ein `powershell -enc` in reinem Text, der kein echo/rem ist (z. B. ein Schalterwert außer `--shell`), weiter rot machen; im Echtlauf gab es keinen solchen Fall.
- `T0757NeuTests.C_nur_startprogramme` aus d456edd43 ist durch `C_breit_mit_ausnahmen` ersetzt (die alte Erwartung „nur 5 Startprogramme“ war der Rückschritt).

## Zweitprüfung behoben (Stand nach a9135704d)

| Befund | Lösung | Test (Rot auf a9135704d, grün jetzt) |
|---|---|---|
| MITTEL 1 echo/rem-Ausnahme zu breit: `cmd /c start "echo" powershell -enc …`, `start "rem" /min …`, `if not defined echo powershell …`, `bash -c "echo start; powershell -enc …"`, mshta mit `echo=1:`, `node -e "var echo=1;…"` waren gelb/grün | echo/rem zählt nur noch als ERSTES Befehlswort des Abschnitts: am Anfang, nach Schaltern (`/q /d /c /k /r`, auch `/q/c`), nach `(` oder `@` – auch in einem inneren `cmd /c` (forfiles). Abschnittsgrenzen jetzt `& \| ;` und Zeilenumbruch. Zusätzlich darf zwischen echo und PowerShell kein Anführungszeichen offen enden (`start "cmd /c echo" powershell` ist rot). Fehlalarm-Schutz bleibt für `cmd /c echo powershell -e …`, `@echo`, `(echo …)`, `"echo …"`, `rem`, `echo "powershell …" > datei` und `forfiles /c "cmd /c echo powershell …"` | `T0757Fix2Tests.Mittel_echo_nur_als_erstes_wort` (8 rot, 8 nicht rot) |
| NIEDRIG 2 Verschleierung (auch auf main gelb) | Vor der Prüfung werden `""` und Anführungszeichen mitten im Wort entfernt; `/` beendet den Programmnamen (`powershell.exe/NoP`); `/c` `/k` `/r` direkt vor dem Namen auch ohne Leerraum davor (`/q/cpowershell`) | `T0757Fix2Tests.Niedrig_verschleierung` (`pow""ershell`, `"power"shell`, `powershell.exe/NoP /enc`, `/q/cpowershell`) |
| NIEDRIG 5 Lizenz-Verneinung | Zwischen Verneinung und Lizenz dürfen jetzt released/available/licensed/distributed/offered/provided/published bzw. veröffentlicht/lizenziert/verfügbar und unter/under stehen | `T0757Fix2Tests.Niedrig_lizenz_verneinung`; echte Dateien: `Niedrig_alle_nuget_lizenzen` – alle 52 Lizenzdateien im NuGet-Cache (MIT 50, BSD 1, 1 MPL/GPL) plus die Apache- und zlib-Abschnitte aus `R1_echte_lizenzen_aus_nuget`: keine durch Fix1/Fix2-Regeln abgewiesen |
| Echtlauf | 174 Einträge, Rot 15 · Gelb 58 · Grün 101, Gründe gleich – keine neuen Fehlalarme | `reports/T-0757/fix2-spec12-systempruefung-echt.txt` (echt, nur Zählungen) |
| NIEDRIG 4 R3 (nur Doku) | siehe unten | – |

R3 – Grenzen (dokumentiert, nicht geändert):
- Beide Listen sind vom Nutzer beschreibbar: `ps2xRuntime/trendo-einsortiert.txt` im PS2Recomp-Ordner und Trendos Kopie unter `<Daten>/konsolenspiele/ps2recomp-listen/`. Wer beide gleich ändert, kann Trendo dazu bringen, eine Datei im Ordner `ps2xRuntime/src/runner` bzw. `include` zu löschen, deren Prüfsumme er kennt. Pfad-Regel, Prüfsumme und Präfix bleiben die Schranken (kein Löschen außerhalb, nichts Verändertes).
- Reste aus 0.9.108 (Liste nur im PS2Recomp-Ordner, ohne Kopie im Datenordner) gelten als fremd: Die Voraussetzung „PS2Recomp ohne fremde Reste“ sperrt, bis PS2Recomp frisch eingerichtet oder die Dateien von Hand entfernt sind. Die Aktion „Alte Trendo-Dateien aus PS2Recomp entfernen“ greift dort nicht, weil die Dateien nicht in beiden Listen stehen.

Rot-Grün:
- `T0757Fix2Tests` auf a9135704d: 4 Tests, 3 Fehler, 1 bestanden (der Regressionsschutz über alle NuGet-Lizenzen) – `reports/T-0757/fix2-rot-auf-a9135704d-core.log`.
- Core (T0757*, FreePortTests, ConsolePortTests, T0754*, T0755*, NoFixedUser, OsBehaviorTests, W7Tests): 113, bestanden 109, übersprungen 4 (nur Linux), Fehler 0 (`fix2-gruen-core.log`).
- App T0755_Ui, SPEC12_Ui, ConsolePort_Ui: 8/8 (`fix2-gruen-app.log`).

Geändert in Fix2: `src/Trendo.Core/Sys/Gefahren.cs`, `src/Trendo.Core/ConsolePort/FreePort.cs`, `tests/Trendo.Core.Tests/T0757Fix2Tests.cs` (neu), `reports/T-0757/fix2-*`, dieser Bericht.
