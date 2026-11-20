# T-0749: Einscannen (Fotogrammetrie) – echter Lauf oder ehrlich BLOCKIERT

**Ziel:** Den Fotoscan (SPEC-09, Restliste Rang 26) mit einem echten Werkzeug auf diesem PC belegen – oder BLOCKIERT mit Voraussetzung melden.
**Branch:** claude/T-0749-einscannen
**Dateien/Bereich:** `src/Trendo.Core/W6/PhotoScan.cs`, `src/Trendo.App/MainWindow.Spec09Scan.cs`, `tests/Trendo.Core.Tests/PhotoScanTests.cs`, `tests/Trendo.App.Tests/T0749EinscannenEchtUiTests.cs`, `tools/echtlauf/einscannen/`, `reports/T-0749/`
**Fertig, wenn:**
- [x] Werkzeugsuche dokumentiert (COLMAP, Meshroom/AliceVision, OpenMVS …), nichts installiert
- [x] echter Lauf aus der App mit selbst erzeugtem Bildsatz (Blender, 48 Ansichten), Mesh im Projektordner
- [x] Beleg mit Rohlog, Laufzeit, Punkt- und Dreieckszahl, Gegenprobe im Fremdprogramm (Blender-Import)
- [x] Einrichten-Weg (Quelle, Version, Prüfsumme) geprüft, ohne Download
- [x] gefundene Fehler im Weg behoben (Abbruch, Fortschritt, Fehlertexte), Rot-Grün
- [x] betroffene Testklassen grün (kein Volltest – macht die Hauptsitzung)
**Nicht tun:** nichts installieren oder herunterladen, kein Volltest, kein Merge/Release, keine dichte Rekonstruktion vortäuschen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0749-einscannen.bericht.md`.
