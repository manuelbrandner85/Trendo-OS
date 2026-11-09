# T-0503: Grenzen beim Excel- und OBJ-Import

**Ziel:** Drei Schwachstellen aus der Sicherheitsprüfung von T-0502 absichern, damit präparierte Dateien Trendo nicht lahmlegen.
**Branch:** claude/T-0503-import-grenzen
**Dateien/Bereich:** `src/Trendo.Core/Office/SheetFiles.cs`, `src/Trendo.Core/Office/OfficeZip.cs`, `src/Trendo.Core/MeshIO.cs`, Tests in `tests/Trendo.Core.Tests/ImportTempoTests.cs`
**Fertig, wenn:**
- [ ] Verschachtelte Texte in Excel-Notizen zählen einmal und kosten keine vervielfachte Arbeit
- [ ] Viele Blätter auf demselben Eintrag sind in Anzahl und Gesamtgröße begrenzt
- [ ] Der einfache OBJ-Import hat eine Obergrenze, große Scans gehen weiter
- [ ] Normale Dateien ergeben dasselbe wie vorher
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Rust einführen, andere Office-Formate anfassen, neue Funktionen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0503-import-grenzen.bericht.md`.
