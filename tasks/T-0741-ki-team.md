# T-0741: KI-Team – Stopp, Fortschritt je Rolle, Werkzeuge

**Ziel:** Ein laufendes KI-Team (CORE-03, Programm „ki-rollen“) lässt sich jederzeit stoppen, zeigt je Rolle wartet/arbeitet/fertig/Fehler mit Zwischenergebnis, hält das Erarbeitete sichtbar und speicherbar, lässt sich nach dem Stopp (auch nach Neustart) fortsetzen; die Rollen arbeiten mit echten Werkzeugen über den vorhandenen Agentenweg (AgentCenter/AgentRun, ApprovalGate).
**Quelle:** `docs/audit/2026-11-16-profi-audit/d7-restliste.md` Rang 14, `audit-4-ki-wissen.md` Teil B „CORE-03“.
**Branch:** claude/T-0741-ki-team
**Dateien/Bereich:** `src/Trendo.Core/W2/AgentTeam.cs` (neu, Team-Teil von `AgentCenter`), `src/Trendo.Core/W2/AgentRuns.cs` (Team-Block dorthin verschoben, eine Zeile im Werkzeug-Fehlerweg), `src/Trendo.App/MainWindow.AgentRun.cs`, `src/Trendo.App/MainWindow.Agents.cs`, Tests, `reports/T-0741/`.
**Fertig, wenn:**
- [x] Stopp beendet das Team; fertige Rollen, Dateien und Zwischenstand der gestoppten Rolle bleiben sichtbar und lassen sich als Datei speichern
- [x] Fortschritt je Rolle live (wartet, arbeitet, fertig, Fehler, gestoppt) mit Zwischenergebnis und Übergabe-Datei
- [x] Rollen nutzen Werkzeuge: Dateien lesen, Notiz/Datei schreiben (nur Rollen mit Schreibrecht), im Internet nachlesen (nur Recherche-Rollen, jeder Abruf nur nach Freigabe über ApprovalGate)
- [x] Fortsetzen nach Stopp, auch nach Neustart, ohne fertige Rollen zu wiederholen
- [x] Echtlauf mit lokalem Ollama-Modell über die Oberfläche, Beleg unter `reports/T-0741/` mit SHA-256
- [x] betroffene Testklassen grün (kein Volltest – macht die Hauptsitzung)
**Nicht tun:** kein zweites Agentensystem, keine Änderungen an IntelligenceCore/ApprovalGate/Abläufen (Rang 17 baut parallel auf Agenten-Schritten auf), kein Volltest, kein Merge.
**Am Ende:** Branch pushen, Bericht `tasks/T-0741-ki-team.bericht.md`.
