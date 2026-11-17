# T-0743: Großprojekte – Karte an Studio übergeben, Ergebnis zurück

**Ziel:** Eine Karte eines Großprojekts übergibt ihren Auftrag an ein Studio (Programm öffnet sich mit vorbereitetem Dokument), die Ergebnisdatei kommt verknüpft, mit Status „erledigt“ und Vorschau an die Karte zurück; Ketten sind frei zusammenstellbar.
**Branch:** claude/T-0743-grossprojekte
**Quelle:** `docs/audit/2026-11-16-profi-audit/d7-restliste.md` Rang 18 (X-01), Lücke laut `audit-4-ki-wissen.md`: 4 feste Ketten, Karten tun nichts.
**Dateien/Bereich:** `src/Trendo.Core/BigBoard.cs`, neu `src/Trendo.Core/BigBoardHandoff.cs`, `src/Trendo.App/MainWindow.W5Fusion.cs`, neu `src/Trendo.App/MainWindow.GrossprojektUebergabe.cs`;
gemeinsam (je eine Zeile, unvermeidbar für den Rückweg): `src/Trendo.App/MainWindow.ProgramShell.cs` (Export der Hülle), `src/Trendo.App/MainWindow.ClaudeBridge.cs` (Claude-Werkzeug „exportieren“);
Bildprüfung: `tools/Trendo.Render/Program.cs` (`TRENDO_BIG_DEMO`). Tests: neu `tests/Trendo.Core.Tests/BigBoardHandoffTests.cs`, `tests/Trendo.App.Tests/GrossprojektUebergabeUiTests.cs`.
**Fertig, wenn:**
- [x] Karte → „An Studio übergeben“ öffnet das gewählte Programm mit vorbereitetem Dokument (Titel, Aufgabe, Ergebnis der Karte davor als Eingabe), Auftrag als Übergabe im Projekt
- [x] Export im Studio (Knopf, Menü oder Claude „exportieren“) bringt die Datei zurück: Beleg mit SHA-256, Übergabe „ergebnis“, Status „erledigt“, Vorschau
- [x] Kette frei: Karten anlegen, Studio wählen, umsortieren (Knöpfe, Rechtsklick, Alt+↑/↓); die 4 Abläufe bleiben Vorlagen und bekommen ihre Studios
- [x] kein zweites Register: ProjectStore + ProjectHandoff + Programm-Hülle
- [x] Echtbeleg Text → PDF mit zwei Studios unter `reports/T-0743/`
- [x] berührte Testklassen grün (kein Volltest – macht die Hauptsitzung)
**Nicht tun:** keine neue Engine, kein zweites Projektregister, kein Merge/Release, kein Volltest.
**Am Ende:** Branch pushen, Bericht `tasks/T-0743-grossprojekte.bericht.md`.
