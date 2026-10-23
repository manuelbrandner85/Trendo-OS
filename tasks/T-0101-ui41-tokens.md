# T-0101: UI41-1 Design-Tokens und Premium-Grundbausteine

**Ziel:** Zentrale Avalonia-Ressourcen (Farben, Radien, Schatten, Abstände, Schrift) und wiederverwendbare Bausteine (Fläche, Karte, Titelleiste, Statuskachel, Aktionsknopf, Panel) statt im Code erzeugter Brushes.
**Branch:** claude/T-0101-ui41-tokens
**Dateien/Bereich:** `src/Trendo.App/App.axaml`, neue `src/Trendo.App/Themes/*.axaml`, neue Controls unter `src/Trendo.App/Controls/`, `MainWindow.Look.cs` (nur Anbindung an Tokens), Tests unter `tests/Trendo.App.Tests/`.
**Vorgehen:** Tokens laut §4 (Startwerte #050B17 … #51C5FF, Radius 10/14/18) als DynamicResource; bestehende Hell/Dunkel/Hochkontrast + Akzentwahl weiterverwenden, keine zweite Theme-Engine. Jeder Baustein mit Hover/Pressed/Disabled/Fokus/Fehler/Hochkontrast.
**Fertig, wenn:**
- [ ] Theme-Wechsel (Dunkel/Hell/Kontrast, Akzent) wirkt ohne Neustart des Fensters, Test vorhanden
- [ ] Bausteine haben sichtbaren Fokusring (Test prüft Fokus-Style)
- [ ] keine Regression: Kachel-/Karten-Klicktests grün
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Layout des Desktops noch nicht umbauen, kein Logo ändern, keine Studionamen ändern.
**Am Ende:** Branch pushen, Bericht `tasks/T-0101-ui41-tokens.bericht.md`. Keinen PR öffnen.
