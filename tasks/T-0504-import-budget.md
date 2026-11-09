# T-0504: Restliche Grenzen beim Excel- und OBJ-Import

**Ziel:** Die von der Sicherheitsprüfung zu T-0503 gefundenen, älteren Lücken schließen, damit präparierte Excel- und OBJ-Dateien Trendo nicht lahmlegen.
**Branch:** claude/T-0504-import-budget (Basis: claude/T-0503-import-grenzen, PR 283 noch nicht in main)
**Dateien/Bereich:** `src/Trendo.Core/Office/SheetFiles.cs`, `src/Trendo.Core/MeshIO.cs`, Tests in `tests/Trendo.Core.Tests/ImportTempoTests.cs`
**Fertig, wenn:**
- [ ] `ReadCharts`: Beziehungen über ein Wörterbuch, jedes Diagramm-Ziel wird nur einmal geladen, Anzahl der Anker begrenzt
- [ ] Gemeinsames Budget je `ReadXlsx` für alle Lesevorgänge in `ReadExtras` und `ReadCharts`
- [ ] Verschachtelte Texte in Diagrammen (Titel, `c:f`) werden linear und nur einmal gelesen
- [ ] `ReadObjParts`: Gesamtgrenze über alle Teile, geprüft beim Einlesen
- [ ] Für jeden Punkt ein Negativtest mit präparierter Datei
- [ ] Normale Dateien ergeben dieselben Daten wie vorher
- [ ] alle drei Testprojekte und Paket-Tests grün (Zahlen in den Bericht)
**Nicht tun:** Rust einführen, neue Funktionen, andere Office-Formate (DOCX, ODS, PPTX) ändern, sie nur prüfen und im Bericht nennen. Nicht ohne OK nach main mergen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0504-import-budget.bericht.md`.
