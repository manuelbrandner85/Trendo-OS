# T-0502: OBJ- und Excel-Import schneller und speichersparender

**Ziel:** Die bei der Leistungsmessung vom 08.11.2026 langsamsten Importe (Excel lesen, OBJ lesen) in C# beschleunigen, ohne das Ergebnis zu verändern.
**Branch:** claude/T-0502-import-tempo
**Dateien/Bereich:** `src/Trendo.Core/MeshIO.cs`, `src/Trendo.Core/Office/SheetFiles.cs`, `src/Trendo.Core/Office/OfficeZip.cs`, neue Tests in `tests/Trendo.Core.Tests/`
**Fertig, wenn:**
- [ ] OBJ lesen braucht messbar weniger Zeit und Zwischenspeicher als vorher
- [ ] Excel lesen braucht messbar weniger Zeit und Zwischenspeicher als vorher
- [ ] Vorher und nachher liefern dieselben Daten (Prüfsumme über dieselben Dateien)
- [ ] Die Schutzregeln beim Öffnen (DTD, Größengrenzen) gelten auch beim gestreamten Lesen
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Rust einführen, Dateiformate erweitern, neue Funktionen, ODS/DOCX/PPTX anfassen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0502-import-tempo.bericht.md`.
