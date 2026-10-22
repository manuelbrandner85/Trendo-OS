# T-0107: UI41-5 Fenstertiefe, Bewegung, Einstellungen, Leistungsprofile

**Ziel:** Fenster mit weichen Schatten und klar aktiv/inaktiv (Ref 06/10); Einstellungen mit Bereichsnavigation und Theme-Presets (Ref 07/09); Bewegung 120–240 ms mit „Bewegung reduzieren“; Leistungsprofile High-End/Standard/Pi-Low-Power; sanfter Start ohne künstliche Wartezeit.
**Branch:** grok/T-0107-ui41-fenster-einstellungen
**Dateien/Bereich:** `MainWindow.Windows.cs` (nur Darstellung), Einstellungsansicht, `UserSettings.cs` (neue Einstellungen), Themes aus T-0101, Tests.
**Fertig, wenn:**
- [ ] Fensterfunktionen (Drag/Resize/Snap/Min/Max/Close/Taskleiste) unverändert grün
- [ ] „Bewegung reduzieren“ schaltet Animationen ab (Test)
- [ ] Low-Power-Profil ohne Blur/Schatten (Test)
- [ ] Startzeit vorher/nachher am PC gemessen, nicht schlechter als 1 s Ziel
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Fensterlogik nicht neu schreiben, kein Logo.
**Am Ende:** Branch pushen, Bericht. Keinen PR öffnen.
