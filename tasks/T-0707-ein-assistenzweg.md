# T-0707: Ein Assistenzweg (Paket B5)

**Ziel:** Ein einziger Weg, über den Claude (nur eigenes Abo) oder ersatzweise die KI auf diesem PC in jedem Programm der Programm-Hülle eine Fachaufgabe im offenen Dokument ausführt: Auftrag → Plan → Vorschau Ja/Nein → Änderung als EIN Rückgängig-Schritt → echte Datei. Programme außerhalb der Hülle melden einheitlich „Noch nicht im Dokument möglich“.
**Branch:** claude/T-0707-ein-assistenzweg (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** `src/Trendo.Core/Programs/AssistChain.cs` (neu), `src/Trendo.Core/Programs/Assist.cs`, `src/Trendo.App/MainWindow.AssistChain.cs` (neu), `MainWindow.Assist.cs`, `MainWindow.PromptAction.cs`, `MainWindow.ClaudeBridge.cs`, `MainWindow.KreativStudios.cs`, `MainWindow.KreativStudios2.cs`, Tests `AssistChainTests`, `AssistChain_Ui`.
**Fertig, wenn:**
- [ ] die vier Wege im Code benannt und auf eine Weiche/Kette geführt sind, keine Funktion entfällt (`tests/claude_handoff` grün, `UebersichtEinfach_Ui` grün)
- [ ] Ergebnis im offenen ProgramDocument als ein Rückgängig-Schritt, danach echte Datei
- [ ] Programme außerhalb der Hülle: Rückmeldung „Noch nicht im Dokument möglich“ + Liste
- [ ] Test vom Auftrag bis zur Datei in drei Programmen aus verschiedenen Fächern, mit echter lokaler KI (Ollama), Rückgängig im Test
- [ ] Claude-Abo ohne Anmeldung ehrlich als NICHT GETESTET gemeldet
**Nicht tun:** ApprovalGate/Rbac-Umbau, Selbst-Freigaben (B4), Umzug von Programmen in die Hülle (B6), die sieben Büro-Studios (T-0705), `MainWindow.ProgramShell.cs` nur falls für den Brücken-Anschluss nötig. Kein Volltest (macht die Hauptsitzung).
**Am Ende:** Branch pushen, Bericht `tasks/T-0707-ein-assistenzweg.bericht.md`.
