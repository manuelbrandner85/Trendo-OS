# Bericht T-0752: Konsolenspiele – „Freies Spiel portieren“

**Auftrag:** Restliste d7 Rang 28 (STUDIO-CONSOLE-01, Audit-Urteil NICHT GETESTET). Ziel: dokumentierter Portierlauf mit einem freien Titel als fester Ablauf im Studio.
**Branch:** `claude/T-0752-konsolenspiele` (Basis main, 0.9.107)

## Was war
- Das Studio war ein Startpult für Claude-Code-Skills (`ConsolePortKit`). Einen eigenen Portierweg gab es nicht, und ein Lauf war im Repo nicht belegt (audit-3, STUDIO-CONSOLE-01). Die beiden vorhandenen UI-Tests ersetzen Claude bzw. Git.
- Der Lauf vom 09.11.2026 (`cubes_demo.elf`, glampert/ps2-homebrew, MIT) stand nur in einer Notiz und war nicht ins Studio übernommen.

## Was ist jetzt
- **Core `FreePort`** (`src/Trendo.Core/ConsolePort/FreePort.cs`):
  - Katalog freier Titel: `cubes-demo` mit Quelle, MIT und den sieben Bindungen, die nur für genau diese ELF-Datei gelten; dazu „Eigenes Homebrew“.
  - Prüfungen: Lizenzdatei (MIT/BSD/zlib/Apache/Unlicense/CC0; beim Katalogtitel genau die erwartete Lizenz), ELF (32 Bit, Little Endian, MIPS; Kaufspiel-Namen wie `SLUS_123.45` werden abgewiesen), PS2Recomp-Ordner (CMakeLists.txt + ps2xRuntime, Pfad höchstens 40 Zeichen), CMake, C++-Compiler (Suchpfad oder Visual Studio mit MSVC).
  - `config.toml` mit `[general]`; das Einsortieren legt `.cpp` nach `ps2xRuntime/src/runner` und `.h` nach `ps2xRuntime/include`.
  - `RunAsync` arbeitet 8 Schritte ab, mit Fortschritt, Abbruch (beendet den Prozess samt Unterprozessen über `Cli.RunStreamAsync`) und `protokoll.txt`. Das Ergebnis landet in `ergebnis.json` mit SHA-256 von ELF und Programm (`AtomicFile`).
  - `TrialAsync` startet das fertige Programm kurz als Probelauf, beendet es nach dem Zeitlimit und erkennt einen Hänger (`[run:tick]` mit stehender pc).
- **Studio** (`MainWindow.FreePort.cs`): neuer Schritt „Freies Spiel portieren“ mit den Bereichen 1 Quelle und Lizenz, 2 Voraussetzungen (✓/✕ mit Einrichten-Hinweis, Ordnerwahl), 3 Portieren (Schrittliste, Fortschrittsbalken, goldene Hauptaktion „Portieren starten“, „Abbrechen“). Darunter stehen das Protokoll und das Ergebnis (SHA-256, „Probelauf (20 s)“, „Ordner öffnen“). Fehlt etwas, steht „BLOCKIERT – …“ da und der Start ist gesperrt. Ab Erweitert lassen sich die Bindungen bearbeiten, beim eigenen Titel auch in Einfach. Die Auswahl ist rückgängig machbar (ProgramDocument).
- PS2Recomp bleibt extern: nichts davon liegt im Repo oder Setup, es wird nur aus dem von der Person gewählten Ordner aufgerufen. Trendo lädt nichts herunter. Dazu gibt es einen Hinweis in `THIRD_PARTY.md`.

## Echter Lauf: BLOCKIERT
PS2Recomp, CMake, `cubes_demo.elf` und der Ordner `%TEMP%\trendo-ps2` sind nicht mehr auf dem PC. Vorhanden ist nur der Compiler (MSVC 14.50/14.51). Laut Vorgabe wurde nichts installiert und nichts geladen. Beleg: `reports/T-0752/bestandsaufnahme.log`, `BELEG.md`.

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler
- `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "FullyQualifiedName~FreePortTests|FullyQualifiedName~ConsolePortTests"` → 15/15 bestanden (davon 10 FreePortTests, Rohlog `reports/T-0752/test-core.log`)
- `dotnet test … --filter "FullyQualifiedName~NoFixedUserTests"` → 1/1 bestanden
- `Trendo.App.Tests.exe -class Trendo.App.Tests.ConsolePort_Ui` → 4/4 (Rohlog `reports/T-0752/test-app.log`)
- `-class Trendo.App.Tests.GameLibrary_Ui` → 8/8, `-class Trendo.App.Tests.DesignPaket3_Ui` → 14/14, `-class Trendo.App.Tests.UebersichtEinfach_Ui` → 2/2
- Vorschaubilder: `TRENDO_STUDIO=STUDIO-CONSOLE-01 TRENDO_PROGTPL=neu TRENDO_CONSTEP=frei TRENDO_LEVEL=einfach|profi` → `reports/T-0752/freies-spiel-*.png`
- Kein Volltest (macht die Hauptsitzung).

## Echt oder ersetzt
- Echt: Prüfung der Voraussetzungen gegen diesen PC (`Real_probe_on_this_pc`). Lizenz und ELF sind dabei Testdateien. Der Probelauf startet einen echten Prozess, beendet ihn und erkennt den Hänger (`Trial_real_process`, Python an Stelle des Spiels).
- Ersetzt: CMake, PS2Recomp und `ps2EntryRunner.exe` in `Run_to_result`, `Cancel` und `Free_port_click_to_result` (`FreePortRunnerOverride`/`FakeTools`). Ein Ende-zu-Ende-Nachweis der Übersetzung fehlt also.

## Abnahmekriterien
1. Aus leerer Vorlage zum echten Ergebnis: **BLOCKIERT** (Werkzeuge fehlen). Der Weg ist bis zur Ergebnisdatei nur mit ersetzten Werkzeugen getestet.
2. Fremddatei importieren/exportieren, Export im Fremdprogramm: **BLOCKIERT** (ELF-Import geprüft, das erzeugte Programm konnte nicht gebaut werden).
3. Prozessabbruch ≤ 30 s: **TEILWEISE**. Die Auswahl liegt in der Programm-Hülle mit Wiederherstellung, Protokoll und Ergebnis werden mit `AtomicFile` geschrieben. Ein laufender Bau startet nach einem Absturz neu, es gibt keine Fortsetzung mitten im Schritt.
4. Claude/KI: **NICHT GETESTET** (nicht Teil dieses Pakets; die bisherigen Skill-Aufträge bleiben unverändert).
5. Test vom Klick bis zur Datei: **TEILWEISE** (Klick → `ergebnis.json` mit SHA-256, Werkzeuge ersetzt).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **TEILWEISE**. Einfach und Profi unterscheiden sich (Bindungen), Rückgängig läuft über die Hülle. Rechtsklick und Ziehen gibt es im neuen Bereich nicht.

## Nicht geprüft
- Ob PS2Recomp in der heutigen Fassung die Befehle und das `config.toml`-Format so annimmt (Stand der Notiz vom 09.11.2026).
- Andere Titel als `cubes_demo.elf`; andere Zielgeräte.
- Ob der Probelauf-Knopf mit einem echten `ps2EntryRunner.exe` funktioniert.

## Offene Punkte
- Echten Lauf nachholen, sobald PS2Recomp (kurzer Pfad) und CMake eingerichtet sind. Dann `protokoll.txt`, `ergebnis.json`, `probelauf.txt` und ein Bildschirmfoto unter `reports/T-0752/` ablegen.
- Katalogstatus `konsole-programm` bleibt `IN_PROGRESS / NOT_RUN`, bis der echte Lauf belegt ist.

## Geänderte Dateien
`src/Trendo.Core/ConsolePort/FreePort.cs` (neu), `src/Trendo.App/MainWindow.FreePort.cs` (neu), `src/Trendo.App/MainWindow.ConsolePort.cs`, `tools/Trendo.Render/Program.cs`, `tests/Trendo.Core.Tests/FreePortTests.cs` (neu), `tests/Trendo.App.Tests/ConsolePortUiTests.cs`, `THIRD_PARTY.md`, `reports/T-0752/*`, `tasks/T-0752-konsolenspiele*.md`.

## Gegenprüfung behoben (Stand nach a44ab8fb2)

| Befund | Lösung | Test (Rot auf a44ab8fb2: kompiliert nicht, fehlende API) |
|---|---|---|
| M1 Lizenz per Teilstring | Vollständige Kennzeichen je Lizenz (MIT zwei Sätze, „CC0 1.0 Universal“ + „Statement of Purpose“, Apache mit Version und TERMS). Abweisen bei „All rights reserved“, „Alle Rechte vorbehalten“, EULA, proprietary/proprietär, GPL/General Public License. Lizenz muss im Ordner der ELF oder darüber liegen. Katalogtitel: SHA-256 der LICENSE.txt (LF-normalisiert) | `FreePortTests.License_bypass_rejected`, `License_same_project_and_hash` |
| M2 Kaufspiel nur am Namen, Titel nicht an ELF gebunden | Katalogtitel verlangt SHA-256 `5af35384…` der echten cubes_demo.elf, sonst Hinweis auf „Eigenes Homebrew“. Regex `^[SPT][A-Z]{3}[_-]\d{3}\.?\d{2}`. SYSTEM.CNF mit `BOOT2 = cdrom0:` im Ordner oder darüber wird abgewiesen, ebenso ein Disc-Laufwerk bzw. eingebundenes Abbild (DriveType CDRom) | `Elf_check`, `Catalog_title_bound_to_elf`; echte Dateien: `reports/T-0752/freies-spiel-echte-dateien.png`, `quelle-cubes-demo.log` |
| M3 Probelauf startet beliebige Datei | `ResolveTrial`: Programm neu per `FindBuilt(build, ps2EntryRunner)`; es muss unter dem Build-Ordner liegen, gleich `programm` sein und gleich `programm_sha256`. Argument ist `elf_pfad` aus der Ergebnisdatei mit SHA-Abgleich. Sonst ist der Knopf gesperrt und es wird nichts gestartet | `Trial_target_checked`, `ConsolePort_Ui.Free_port_click_to_result` (cmd.exe eingetragen → gesperrt, kein Aufruf) |
| M4 zweiter Lauf mischt Code | Liste `ps2xRuntime/trendo-einsortiert.txt`: Dateien des letzten Laufs werden vor dem Einsortieren entfernt. Eine Datei von PS2Recomp wird nie überschrieben (Fehler). .obj in `*ps2EntryRunner*.dir` werden gelöscht. Neuer Schritt „Neue Code-Dateien anmelden“ (`cmake -S/-B`), insgesamt 8 Schritte. `einsortiert` steht in ergebnis.json | `Second_run_does_not_mix`, `Run_to_result` |
| N1 Ansicht wirft | Prerequisites fängt IO-Fehler je Zeile (✕ „lässt sich gerade nicht lesen“); Ergebnis und Protokoll werden mit Fehlerfang gelesen | `Locked_elf_does_not_throw`, UI-Test mit „{ kaputt“ |
| N2 Protokoll nur je Schritt | Atomar alle 3 s gesichert (`LogFlushEvery`), auch mitten im Schritt | `Log_flushed_during_step` |
| N3 0 Ticks = Erfolg | Status „unklar“ ohne `[run:tick]`, gelb | `Trial_without_ticks_is_unclear` |
| N4 Log verstümmelt | Anonymisierung per Skriptdatei (`C:\Users\<user>`) | `reports/T-0752/test-core.log` |
| N4 falsche Statuszeile | Im Schritt „Freies Spiel“ zeigt sie „BLOCKIERT – …“, den Fortschritt oder „Portieren fertig“ | UI-Test liest `prog-status-text` |
| N4 Hauptaktion unter dem Rand | „Portieren starten“ und der BLOCKIERT-Hinweis stehen jetzt oben | `freies-spiel-profi.png`, `freies-spiel-echte-dateien.png` |
| N4 cmake.cmd | `FreePort.FindExe` nimmt unter Windows nur `.exe` | `FindExe_only_exe` |

Geprüft nach der Korrektur:
- Core `FreePortTests|ConsolePortTests|NoFixedUserTests`: 24/24 (Rohlog `reports/T-0752/test-core.log`)
- `ConsolePort_Ui`: 4/4
- `GameLibrary_Ui`: 8/8
- `DesignPaket3_Ui`: 14/14
- `UebersichtEinfach_Ui`: 2/2
- Build 0 Fehler

Einschränkungen:
- BSD-Texte mit „All rights reserved.“ in der Copyright-Zeile werden jetzt abgewiesen; so verlangt es die Gegenprüfung.
- Eine umbenannte Disc-ELF ohne SYSTEM.CNF daneben erkennt „Eigenes Homebrew“ weiterhin nur über die Pflicht-Lizenzdatei im selben Projekt.

Der echte Portierlauf bleibt BLOCKIERT (PS2Recomp und CMake fehlen). Die echte `cubes_demo.elf` und `LICENSE.txt` wurden einmalig von der Original-Quelle geholt, um die Prüfsummen festzulegen, und danach gelöscht.

**Status:** Ablauf im Studio fertig und getestet (Werkzeuge ersetzt); echter Portierlauf BLOCKIERT.
