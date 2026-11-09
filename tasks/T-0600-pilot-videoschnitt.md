# T-0600: Pilot 4.0 Videoschnitt

**Ziel:** Das Programm Videoschnitt (CORE-08) nach Rebuild 4.0 ausbauen und als erstes Programm abnehmen, ohne Funktionsverlust.
**Branch:** claude/T-0600-pilot-videoschnitt (eigener Ordner)
**Dateien/Bereich:** MainWindow.Video*.cs, Media/VideoEdit.cs, Media/VideoPro.cs, neue Dateien; zugehörige Tests
**Fertig, wenn:**
- [ ] V1 flüssige Vorschau mit Ton
- [ ] V2 beliebig viele Spuren, alte Projekte öffnen unverändert
- [ ] V3 framegenau nach Projekt-Bildrate
- [ ] V4 Einrasten, Ripple, Slip, Rolling
- [ ] V5 Keyframes
- [ ] V6 Effektstapel
- [ ] V7 Farbräder, Waveform, Vektorskop
- [ ] V8 Mischpult
- [ ] V9 Rechtsklick-Menüs, ziehbare Bereiche
- [ ] V10 Abnahme mit mindestens 20 eigenen Testfällen
- [ ] alle drei Testprojekte und Paket-Tests grün
**Nicht tun:** Kein Merge nach main ohne OK des Nutzers. Kein Release. Keine Änderung der Programm-Hülle für Andock-Panels ohne Freigabe.
**Am Ende:** Branch pushen, Bericht `tasks/T-0600-pilot-videoschnitt.bericht.md`.
