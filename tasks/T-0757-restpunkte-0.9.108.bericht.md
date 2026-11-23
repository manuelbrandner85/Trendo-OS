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
