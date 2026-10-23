# T-0102: UI41-2a Responsives Desktop-Layout statt Gesamtzoom

**Ziel:** Feste Designfläche ~1536×800 mit `LayoutTransformControl`/`ScaleTransform` und Spalten `192,*,322` durch echte adaptive Spalten ersetzen.
**Branch:** claude/T-0102-ui41-responsive
**Dateien/Bereich:** `src/Trendo.App/MainWindow.axaml`, `MainWindow.axaml.cs` (Layoutaufbau), `MainWindow.Windows.cs` nur falls für Arbeitsfläche nötig, Tests.
**Vorgehen:** Breakpoints (z. B. < 1280: linke Leiste nur Symbole, rechtes Panel ausklappbar); Schrift bleibt bei 1366×768 lesbar; Dock verdeckt nichts.
**Fertig, wenn:**
- [ ] kein globaler ScaleTransform mehr im Hauptlayout
- [ ] Headless-Test rendert 1024×768, 1366×768, 1920×1080, 2560×1440 ohne Überlappung (Bounds-Prüfung) + Screenshots im Bericht
- [ ] Fenster Drag/Resize/Snap/Min/Max und Taskleiste unverändert (bestehende Tests grün)
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Inhalte/Widgets nicht neu gestalten (T-0103), keine Fensterlogik ändern.
**Am Ende:** Branch pushen, Bericht. Keinen PR öffnen.
