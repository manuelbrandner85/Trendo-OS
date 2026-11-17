# T-0706: Freigaben in den Kern (Paket B4)

**Ziel:** Wirkungen mit Folgen (installieren, laden, Programme starten, Daten nach außen geben, Abläufe mit Dateiänderungen) laufen nur über Kern-Dienste mit Freigabe-Tor (`ApprovalGate`, `Rbac`, `AuditLog`); die Freigabe ist ein eigener, bewusster Schritt der angemeldeten Person.
**Branch:** claude/T-0706-freigaben-kern (Basis: claude/T-0685-b1-integration)
**Dateien/Bereich:** `src/Trendo.Core` (ApprovalGate, Rbac, Dependencies, Integrations, W2/KiSources), `src/Trendo.Api/LocalApi.cs`, `src/Trendo.App` (neuer Baustein `MainWindow.Freigabe.cs` und die Stellen mit Selbst-Freigabe), Tests.
**Fertig, wenn:**
- [ ] Die 28 Stellen `Approvals.Approve(…)` in `src/Trendo.App` sind geprüft; Selbst-Freigaben im selben Klick sind durch eine eigene Rückfrage (Dialog oder Rückfrage-Karte) ersetzt, die Wirkung läuft nur über `ApprovalGate.Execute` mit genau dieser Freigabe.
- [ ] Freigaben erteilt die angemeldete Person (`CurrentPrincipal` aus dem Profil); ein Kinderprofil (Rolle Benutzer) kann im Kern nicht freigeben.
- [ ] Kern-Tests je umgebauter Wirkungsart (ohne Freigabe verweigert, mit Freigabe genau einmal, nicht wiederverwendbar, im Protokoll), UI-Test „Klick allein führt nichts aus“, Architekturtest gegen neue Selbst-Freigaben.
- [ ] Gezielte Testklassen grün (kein Volltest – macht die Hauptsitzung).
**Nicht tun:** `MainWindow.ProgramShell.cs`, Programm-Hülle, Claude-Anbindung/Assistenzweg (B5/B6), die sieben Büro-Studios (T-0705). Kein zweiter Sicherheitsbroker.
**Am Ende:** Branch pushen, Bericht `tasks/T-0706-freigaben-kern.bericht.md`.
