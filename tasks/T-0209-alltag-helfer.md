# T-0209: Kleine Helfer & Dateien – Abstürze und stumme Knöpfe beheben

**Ausführung:** Box-Agent (nicht Grok Build). Windows-Nachweise am PC über den Integrator.

**Ziel:** Die Alltagshelfer funktionieren ohne Vorwissen.
**Branch:** box/T-0209-alltag-helfer
**Dateien/Bereich:** `MainWindow.Sys02.cs`, `src/Trendo.Core/Utilities.cs`, Tests.
**Befund:** „Geld umrechnen“ und „Pipette“ stürzen bei leerem Feld ab (FormatException); ohne Wirkung: Umbenennen, Speicherplatz anzeigen, Fotos ordnen, Papierkorb anzeigen/leeren, Merken (Zwischenablage), Farbe kopieren, Bild drehen/verkleinern ohne gewähltes Bild.
**Fertig, wenn:**
- [ ] Kein Absturz bei leeren/falschen Eingaben (Test).
- [ ] Ordner/Bild per Dialog wählen; Vorschau, was umbenannt/sortiert wird, vor dem Ausführen.
- [ ] Papierkorb zeigt Liste bzw. klare Meldung „leer“ / „unter Linux nicht verfügbar“.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Am Ende:** Branch pushen, Bericht. PR über den üblichen Ablauf (kleiner PR, main grün).
