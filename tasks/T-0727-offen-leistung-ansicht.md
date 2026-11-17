# T-0727: Offene Probleme Gruppe B – Leistung und Ansicht

**Ziel:** Die offenen Punkte B1–B7 aus den Paketberichten T-0680 … T-0724 prüfen und beheben, was ohne Risiko und ohne sichtbaren Qualitätsverlust geht – Leistung erst messen, dann ändern.
**Branch:** claude/T-0727-offen-leistung-ansicht (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** 3D-Ansicht (`CadView`, `MainWindow.Studio3DViewport.cs`, `MainWindow.W4Studio3D.cs`), Spuren-Mischer (`MainWindow.MusikSpuren.cs`), Test-Hilfen (`UiTests.cs`, `ClaudeOperateUiTests.cs`, Echtläufe Assist/Code-KI), Render-Werkzeug, Belege `reports/T-0727/`.
**Punkte:**
- B1 Kaltstart 11,5 s nach dem Auspacken – Ursache messen, beheben was ohne Risiko geht
- B2 3D-Ansicht: Aufbau nach dem Import 1,7–2,2 s (40 000 Dreiecke)
- B3 3D-Ansicht bei 900 px Fensterhöhe nur ca. 100 px hoch – im Bild bei 1366×768, 1600×900, 1600×1000 (Einfach/Profi)
- B4 Videoschnitt: Standbild in Überblendungen 0,4–0,5 s statt 0,3 s
- B5 Musik: ohne Demucs „Einrichten“ statt Mischer, obwohl die Spuren vorliegen
- B6 Render-/Testumgebung wählt ohne Ollama-Liste qwen2.5:0.5b
- B7 Roter Test ClaudeOperate_Ui.Ribbon_and_context
**Fertig, wenn:**
- [x] je Punkt Ursache mit Messung/Beleg, Behebung oder ehrliche Begründung, vorher/nachher-Zahlen mit Rohlog unter `reports/T-0727/`
- [x] Rot-Grün für die Code-Änderungen; Pflichtklassen grün (Zahlen im Bericht)
**Nicht tun:** Punkte der Gruppen A (T-0725/T-0726) und C (T-0728); kein Volltest; keine Ziele erfinden; Rust nur bei gemessenem Vorteil.
**Am Ende:** Branch pushen, Bericht `tasks/T-0727-offen-leistung-ansicht.bericht.md`.
