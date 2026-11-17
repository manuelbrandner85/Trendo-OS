# T-0728: Offene Probleme Gruppe C – Bedienung

**Ziel:** Die offenen Bedienungs-Punkte C1–C8 aus den Paketberichten T-0703 … T-0723 beheben („wichtig auch offene probleme beheben“, 16.11.2026).
**Branch:** claude/T-0728-offen-bedienung (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** Menüleiste (`MainWindow.MenuBar.cs`, `ProgramSpec`/`ProgramTool`), Videoschnitt und Bildbearbeitung (nur Menü/Kürzel), Auftragszeile (`MainWindow.PromptAction.cs`, nur „Code mit KI“), Claude-Brücke (Rückgängig), Tagesplaner, Bereichsname, `LiveCalc`, Modellwahl auf „Trendo KI (lokal)“.
**Fertig, wenn:**
- [ ] C1 Fachkürzel der Programme stehen in der Menüleiste (über `ProgramSpec.Menus` und die Menüband-Beschriftung) und wirken als echte Taste; `ProgramMenu.Conflicts` bleibt ohne Konflikte
- [ ] C2 Auftragszeile „Los“ in „Code mit KI“ nutzt „Mit KI ändern“
- [ ] C3 ein Claude-Auftrag über die Brücke ist EIN Rückgängig-Schritt
- [ ] C4 Tagesplaner: Wochenansicht; „Tag planen“ behält von Hand verschobene Blöcke
- [ ] C5 Bereich „3D & Spiele“ statt „3D, Spiele & VR“
- [ ] C6 LiveCalc-Ergebnisse in Gold
- [ ] C7 lange Modell-Knöpfe auf „Trendo KI (lokal)“ nicht abgeschnitten
- [ ] C8 Datei-Ablegen aus dem echten Explorer nur, wenn mit vorhandenen Mitteln prüfbar
- [ ] Pflicht-Testklassen grün, neue Tests Rot→Grün, Bilder angesehen
**Nicht tun:** Punkte aus Gruppe A (T-0726) und B (T-0727) anfassen; kein Volltest; nicht nach main oder auf den Integrationszweig pushen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0728-offen-bedienung.bericht.md`.
