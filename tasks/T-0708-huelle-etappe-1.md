# T-0708: B6 „Programm-Hülle für alle“ – Etappe 1

**Ziel:** Programme ohne Autosave an Speicher-, Rückgängig- und Wiederherstellungssystem der Programm-Hülle anschließen, ohne Funktion zu verlieren und ohne zweites System; vollständige Umzugsliste aller Programme außerhalb der Hülle.
**Branch:** claude/T-0708-huelle-etappe-1 (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** `src/Trendo.Core/Programs/` (neu `ProgramLink.cs`, `ProgramDocument.MarkClean`), Briefe (`MainWindow.W1Office.cs`, `MainWindow.LetterPro.cs`, neu `MainWindow.ProgramLinks.cs`, `Letters.cs` atomar), V-14-Gruppe in CAD, 3D-Modelle, Code-Editor; eine Zeile in `MainWindow.ProgramShell.cs` (Takt); `docs/audit/2026-11-16-profi-audit/huelle-umzug.md`.
**Fertig, wenn:**
- [x] Liste aller Programme außerhalb der Hülle mit Speicherart, Autosave, Fach und Etappe (`huelle-umzug.md`)
- [x] Dateiformat mit Version und Migration festgelegt, alte Datei lesbar (Test)
- [x] 3D-Studio, CAD, Code-Editor, Briefe: Speichern · Rückgängig · Wiederholen (· Exportieren) links oben (V-14)
- [x] Briefe: Rückgängig über mehrere Schritte, Notsicherung im Takt der Hülle, Wiederherstellen-Karte; echter Abbruchtest
- [x] gezielte Testklassen grün, UebersichtEinfach_Ui und ProgramHeadDesign_Ui grün, Funktions-Baseline grün
**Nicht tun:** ApprovalGate (B4), Assistenzweg/Claude-Brücke (B5), sieben Büro-Studios (T-0705); kein Volltest, kein Merge, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0708-huelle-etappe-1.bericht.md`.
