# T-0690: C1 – Spiele-Studio produziert echte spielbare Spiele

**Ziel:** Aus „Spiele bauen“ entsteht ein frei gestaltetes, wirklich spielbares Spiel (Browser und Godot), echt geprüft; Unreal-Texte passen zur Leistung.
**Branch:** claude/T-0690-spiele
**Dateien/Bereich:** `src/Trendo.Core/GameStudio.cs`, `src/Trendo.Core/Programs/{GameDesign,GameDirector,GamePro,GameMenus,PlatformSim,EngineTargets}.cs`, `src/Trendo.Core/PromptAction/GamePromptAdapter.cs`, `src/Trendo.App/MainWindow.ProgGame*.cs`, `MainWindow.ClaudeGame.cs`, `MainWindow.W4Game.cs` (nur ein Knopftext), `LevelCanvas.cs`, `PlaytestCanvas.cs`, `WebsitePreview.cs` (neue Funktion `PlayAsync`), `tools/Trendo.Render/Program.cs` (Schalter `TRENDO_GAME_VIEW`), Tests, `reports/T-0690/`.
**Fertig, wenn:**
- [x] Kachelspiel frei gestaltbar: Blick von der Seite oder von oben, eigene Level, Gegner (laufen/verfolgen), Regeln mit Sieg und Niederlage, eigene Bilder je Objektart und Töne, mehrere Level, Speichern
- [x] Export als HTML5-Spiel in echtem Chrome mit echten Tastendrücken durchgespielt (gewinnen und verlieren), als Test, übersprungen ohne Browser
- [x] Godot-4-Export für jedes Kachelspiel (beide Blickrichtungen) mit echtem Lauf und Durchspiel-Prüfung, als Test, übersprungen ohne Godot
- [x] Unreal: Texte ehrlich; Lauf NICHT GETESTET mit Grund
- [x] Spiel aus einem Satz legt ein sofort spielbares, auf der Mal-Fläche änderbares Spiel an (ein Rückgängig-Schritt)
- [ ] alle drei Testprojekte grün – Volltest macht die Hauptsitzung; gezielt gelaufen: siehe Bericht
**Nicht tun:** `MainWindow.ProgramShell.cs`, 3D-Studio/CAD, `SimpleWords.cs`, Konsolen-Studio; keine Engine installieren, kein laufendes Unreal anfassen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0690-spiele.bericht.md`.
