# T-0201: Echte Dateiauswahl und Drag & Drop überall statt Pfad eintippen

**Ausführung:** Box-Agent (nicht Grok Build). Windows-Nachweise am PC über den Integrator.

**Ziel:** Ich muss nie einen Dateipfad abtippen.
**Branch:** box/T-0201-dateiauswahl
**Dateien/Bereich:** `src/Trendo.App/MainWindow.W6.cs` (`FachTool`-Felder mit Schlüssel `file`), `MainWindow.W4Media.cs`, `MainWindow.W1Letter.cs`, `MainWindow.W5Finance.cs`, `MainWindow.W5Fusion.cs`, `MainWindow.Setup.cs`, gemeinsame Hilfsmethode, Tests.
**Befund:** 36 Dateifelder sind freie Textfelder („Pfad zur Datei“); die App nutzt nirgends `StorageProvider`-Dialoge oder Drag & Drop.
**Fertig, wenn:**
- [ ] Jedes Datei- oder Ordnerfeld hat „Durchsuchen …“ (Avalonia `StorageProvider`) mit passendem Dateityp-Filter.
- [ ] Ich kann eine Datei aus dem Explorer auf das Feld oder das Studio ziehen.
- [ ] Zuletzt benutzte Dateien des Studios stehen als Auswahl bereit.
- [ ] Test: jedes Feld mit Schlüssel `file`/`folder` hat einen Durchsuchen-Knopf.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Berechnungslogik nicht ändern.
**Am Ende:** Branch pushen, Bericht. PR über den üblichen Ablauf (kleiner PR, main grün).
