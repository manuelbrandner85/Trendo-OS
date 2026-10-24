# T-0203: Externe Programme direkt öffnen – mit der aktuellen Datei

**Ausführung:** Box-Agent (nicht Claude Code). Windows-Nachweise am PC über den Integrator.

**Ziel:** Wo ein Studio ein externes Programm nutzt, öffnet ein Klick das Programm mit meiner aktuellen Datei.
**Branch:** box/T-0203-programme-direkt-oeffnen
**Dateien/Bereich:** `MainWindow.W4Studio3D.cs` (nur Öffnen-Knopf, falls T-0200 noch offen), `MainWindow.W4Game.cs`, `MainWindow.W4Dev.cs`, `MainWindow.W4Media.cs`, `MainWindow.Spec15.cs`, `MainWindow.W1Office.cs`, `src/Trendo.Core/Integrations.cs`, Tests.
**Fertig, wenn:**
- [ ] Knöpfe „In Blender öffnen“, „In Unreal öffnen“, „In VS Code öffnen“ (bzw. Standard-Editor), „In LibreOffice öffnen“, „In OBS öffnen“ übergeben die Datei bzw. das Projekt.
- [ ] Fehlt das Programm: ein Knopf „Einrichten“ mit Rückfrage; danach ist der Öffnen-Knopf ohne Neustart aktiv.
- [ ] Eine zentrale Liste „Programme“ zeigt alle erkannten externen Programme mit „Starten“.
- [ ] Test mit Fake-Launcher prüft Programmpfad UND Dateiargument.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** nichts ungefragt installieren.
**Am Ende:** Branch pushen, Bericht. PR über den üblichen Ablauf (kleiner PR, main grün).
