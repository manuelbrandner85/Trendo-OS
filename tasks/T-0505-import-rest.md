# T-0505: Offene Import-Lücken aus T-0504 schließen

**Ziel:** Die im Bericht T-0504 genannten, älteren Lücken beim Öffnen von Office- und 3D-Dateien schließen, damit präparierte Dateien Trendo nicht lahmlegen.
**Branch:** claude/T-0505-import-rest (Basis: claude/T-0504-import-budget, noch nicht in main)
**Dateien/Bereich:** `src/Trendo.Core/Office/` (OfficeZip, SheetFiles, DocxFormat, SlideRead), `src/Trendo.Core/Sys/Sys10.cs`, `src/Trendo.Core/MeshIO.cs`, `src/Trendo.App/MainWindow.W4Studio3D.cs`, Tests in `tests/Trendo.Core.Tests/ImportTempoTests.cs`
**Fertig, wenn:**
- [ ] Excel: Auswahllisten, Spaltenbreiten, Knotenzahl begrenzt; `Sys10.ReadXlsx` nutzt die ZIP-Prüfung
- [ ] DOCX, ODT, ODS, PPTX, ODP: Tiefe und Knotenzahl begrenzt, verschachtelte Texte (DOCX/PPTX) einmal, ODS-Wiederholungen begrenzt
- [ ] OBJ: `mtllib` bleibt im Ordner der Datei und ist in der Größe begrenzt
- [ ] STL, GLB, glTF, FBX: Grenzen beim Einlesen, keine Pfade außerhalb des Ordners
- [ ] Große 3D-Dateien blockieren die Oberfläche nicht
- [ ] Für jeden Punkt ein Negativtest mit präparierter Datei
- [ ] Normale Dateien ergeben dieselben Daten wie vorher
- [ ] alle drei Testprojekte und Paket-Tests grün (Zahlen in den Bericht)
**Nicht tun:** Rust einführen, neue Funktionen, nach main mergen ohne OK.
**Am Ende:** Branch pushen, Bericht `tasks/T-0505-import-rest.bericht.md`.
