# T-0105: UI41-2c Studioübersicht mit Premium-Karten nach Referenz 03

**Ziel:** „Alle Studios“ mit großen Karten (großes Studio-Symbol aus `StudioIcons`), Filter-Chips nach den 9 Bereichen, Suchfeld, echter Status („Bereit“/„Braucht ein Zusatzprogramm“/„Noch nicht verfügbar“).
**Branch:** grok/T-0105-ui41-studiokarten
**Dateien/Bereich:** Studioübersicht in `MainWindow.axaml.cs`, `StudioCatalog.cs` (nur lesen/Hilfsmethoden), `StudioIcons.cs`, Tests.
**Fertig, wenn:**
- [ ] alle 54 Karten per Maus UND Tastatur öffnen (bestehender Klicktest erweitert)
- [ ] Filter und Suche getestet
- [ ] Screenshot neben Ref 03 im Bericht
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** keine Katalognamen ändern, keine „Unreal Studio“/„Blender Studio“-Karten anlegen, SPEC-07 nicht ausbauen.
**Am Ende:** Branch pushen, Bericht. Keinen PR öffnen.
