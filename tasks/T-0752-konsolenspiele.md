# T-0752: Konsolenspiele – fester Ablauf „Freies Spiel portieren“

**Ziel:** Im Studio STUDIO-CONSOLE-01 gibt es einen nachvollziehbaren Ablauf, der einen frei lizenzierten PS2-Homebrew-Titel mit dem externen PS2Recomp zu einem Windows-Programm übersetzt. Dazu gehört ein echter Beleglauf oder ein ehrliches BLOCKIERT (Restliste d7, Rang 28, freigegeben 23.11.2026).
**Branch:** claude/T-0752-konsolenspiele
**Dateien/Bereich:** `src/Trendo.Core/ConsolePort/FreePort.cs` (neu), `src/Trendo.App/MainWindow.FreePort.cs` (neu), `src/Trendo.App/MainWindow.ConsolePort.cs`, `tools/Trendo.Render/Program.cs` (Vorschau-Schalter `TRENDO_CONSTEP`), Tests `FreePortTests`, `ConsolePort_Ui`, `THIRD_PARTY.md`, `reports/T-0752/`.
**Fertig, wenn:**
- [x] Quelle und Lizenz sichtbar, Lizenzdatei wird geprüft (MIT/BSD/zlib/Apache/Unlicense/CC0), Kaufspiel-ELF und Fremdformate werden abgewiesen
- [x] Voraussetzungen (PS2Recomp, kurzer Pfad, CMake, Compiler) werden vor dem ersten Schritt geprüft; fehlt etwas: BLOCKIERT mit Einrichten-Hinweis, nichts wird aufgerufen
- [x] 7 Schritte mit Fortschritt, Protokoll (`protokoll.txt`), Ergebnisdatei (`ergebnis.json` mit SHA-256), Abbrechen, Probelauf mit Hänger-Erkennung
- [x] Tests vom Klick bis zur Ergebnisdatei (Werkzeuge ersetzt), dazu echte Prüfung auf diesem PC und Probelauf mit einem echten Prozess
- [ ] echter Portierlauf – BLOCKIERT: PS2Recomp, CMake und cubes_demo.elf fehlen auf diesem PC
**Nicht tun:** PS2Recomp oder GPL-Code ins Repo/Setup; Kaufspiele, BIOS; Installationen; Volltest.
**Am Ende:** Branch pushen, Bericht `tasks/T-0752-konsolenspiele.bericht.md`.
