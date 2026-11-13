# T-0640: Spiele-Studio Stufe 2 – Team, Ablauf und Prüfstufe

**Ziel:** E2, E3 und E4 aus `docs/nachbau/spiele-ki/anforderungen.md` im Programm „Spiele bauen“ umsetzen.
**Branch:** claude/T-0640-spiele-ablauf
**Dateien/Bereich:** `src/Trendo.Core/Programs/GameTeam.cs`, `GameFlow.cs`, `GameBoard.cs`, `GameDirector.cs`, `src/Trendo.App/MainWindow.ProgGamePro.cs`, `MainWindow.ProgGameBoard.cs`, `MainWindow.ProgGameUgs.cs`, zugehörige Tests, `docs/nachbau/spiele-ki/`.
**Fertig, wenn:**
- [ ] E2: Rollenbaum mit drei Ebenen; Widerspruch landet bei der gemeinsamen Leitung; Entscheidungen mit 2 bis 4 Antworten, ohne Wahl keine Änderung
- [ ] E3: Stories mit Abnahmekriterien aus der Spielbibel auf dem Board; fertig, mit Hinweisen, blockiert; Phasentore mit bestanden, Bedenken, nicht geprüft, durchgefallen
- [ ] E4: Prüfstufe schlank, normal, streng; je Stufe genau die vorgesehenen Prüfungen; Wechsel erhält alle Inhalte
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Stufe 3 (E1, E5 bis E7, D5); Pflicht-Auswahl, die die KI-Automatik bremst; Merge oder Release ohne OK des Nutzers.
**Am Ende:** Branch pushen, Bericht `tasks/T-0640-spiele-ablauf.bericht.md`.
