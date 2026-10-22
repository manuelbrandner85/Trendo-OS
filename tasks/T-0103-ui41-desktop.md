# T-0103: UI41-2b Premium-Desktop nach Referenz 01 (Vertical Slice)

**Ziel:** Desktop nach `docs/spec/design-41/referenzen/01_DESKTOP_PREMIUM.png`: ruhige Mitte mit zentralem Suchfeld, schlanke linke Leiste (Start, Studios, KI, Dateien, Einstellungen + „Zuletzt verwendet“ aus echten Daten), rechtes Panel (Datum/Uhr, echte CPU/RAM/GPU/SSD, Netzwerk-Durchsatz), schlankes Dock.
**Branch:** grok/T-0103-ui41-desktop
**Dateien/Bereich:** Desktop-Aufbau in `MainWindow.axaml.cs`, `MainWindow.DesktopMenu.cs`, `SystemMetrics.cs` (nur Netzwerk-Messung ergänzen), Tests. Setzt T-0101/T-0102 voraus.
**Vorgehen:** Namen aus `StudioCatalog`; Wetter nur falls echte Quelle vorhanden, sonst weglassen; GPU ohne Quelle „Nicht verfügbar“; Dock nur echte installierte Programme; bisherige Desktop-Funktionen (Kacheln, Schnell-Tools, Projekte) bleiben über Studios/Startmenü erreichbar.
**Fertig, wenn:**
- [ ] Vorher/Nachher-Screenshot neben Referenz 01 im Bericht, Abweichungen begründet
- [ ] Suchfeld findet Studios/Funktionen (Test)
- [ ] keine Fantasiewerte (Test: Metriken kommen aus SystemMetrics)
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** kein Logowechsel, Bildtexte (z. B. „Deine Ideen. Kein Limit.“, „Trendo Radio“) nicht übernehmen, andere Studios nicht umbauen.
**Am Ende:** Branch pushen, Bericht. Keinen PR öffnen.
