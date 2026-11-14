# T-0650: Spiele-Studio Stufe 3 – Prüfung und Anbindung

**Ziel:** E1, E5, E6, E7 und D5 aus `docs/nachbau/spiele-ki/anforderungen.md` im Programm „Spiele bauen“ umsetzen.
**Branch:** claude/T-0650-spiele-stufe3
**Dateien/Bereich:** `src/Trendo.Core/Programs/GameEngineSetup.cs`, `GameShotCheck.cs`, `GameRuns.cs`, `GameServices.cs`, `GameReferences.cs` (neu), `UnrealGame.cs` (Startwert), `src/Trendo.App/MainWindow.ProgGameStage3.cs` (neu), `MainWindow.ProgGame.cs`, `MainWindow.ProgGameUnreal.cs`, `MainWindow.ProgGameAssets.cs`, `MainWindow.ProgGameDesign.cs`, zugehörige Tests, `docs/nachbau/spiele-ki/`.
**Fertig, wenn:**
- [ ] E1: Engine-Stand erkannt, Projekt, Öffnen und Bauen; „geprüft“ nur mit bestandenem Bericht; fehlt etwas, genau ein Schritt, kein Download
- [ ] E5: Bilder aus dem Testlauf auf Schwarzbild, einfarbiges Bild, fehlende Texturen und Abweichung zum Vergleichslauf geprüft; jeder Befund mit Bild und Messwert; gutes Bild ohne Fehlalarm
- [ ] E6: Testlauf mit Startwert, gespeichert mit Messwerten und Bildern; Vergleich meldet Verschlechterungen; gleicher Startwert und gleiche Ausgabe ergeben gleiche Werte
- [ ] E7: eine Auswahl für Bild, 3D, Ton, Musik und Sprache; ohne Bezahl-Dienst nutzbar; jedes eingefügte Asset mit Lizenz
- [ ] D5: ähnliche Spiele nur mit Beleg; ohne Beleg „keine Referenz gefunden“
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Engines oder Modelle herunterladen; Bezahl-Dienste voraussetzen; fremde Inhalte übernehmen; Merge oder Release ohne OK des Nutzers.
**Am Ende:** Branch pushen, Bericht `tasks/T-0650-spiele-stufe3.bericht.md`.
