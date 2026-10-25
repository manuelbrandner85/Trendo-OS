# T-0005: Not-Stopp für alle lokalen Automationen und Änderungsvorschau im Freigabe-Gate (IMP-19)

**Ziel:** Ein Klick hält alles an, was Trendo selbst tut. Jede Freigabe zeigt vorher, was genau passiert.
**Branch:** claude/T-02xx (Sammelbranch)
**Auftrag:** Fertigstellungsplan C5, Bauplan V2 T-0005. Manuel: „ok weiter“ (2026-10-25), nach D7.
**Dateien/Bereich:** `src/Trendo.Core/ApprovalGate.cs` (vorhandenes Gate erweitern, kein zweites System), `src/Trendo.Core/W2/Workflows.cs`, `src/Trendo.Core/PromptAction/StudioWorkflow.cs`, `src/Trendo.App/MainWindow.NotStopp.cs` (neu), Kopfleiste, Freigabeseite, Abläufe, Tests.
**Fertig, wenn:**
- [ ] Not-Stopp (Knopf in der Kopfleiste, Pause-Taste) verwirft offene und erteilte Freigaben. Bis zum Fortsetzen wird nichts freigegeben und nichts ausgeführt.
- [ ] Laufende Abläufe brechen zwischen den Schritten ab, zeitgesteuerte Abläufe ruhen. Download, Freigabe im Netz, Sendung und Roboterarm stoppen.
- [ ] Stoppen darf jeder, Fortsetzen nur mit Freigaberecht. Beides steht im Audit-Log.
- [ ] Freigaben zeigen eine Änderungsvorschau: bei Abläufen den Probelauf, bei Prompt-Aufträgen die Adapter-Vorschau.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Keine Fensterlogik ändern, keinen eigenen Sicherheitsbroker bauen, Verworfenes beim Fortsetzen nicht wieder freigeben.
**Am Ende:** Branch pushen, Bericht `tasks/T-0005-notstopp.bericht.md`.
