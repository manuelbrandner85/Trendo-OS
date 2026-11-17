# T-0698: D6a – „KI-Agenten bauen“ und „KI-Team“: Agenten laufen wirklich

**Ziel:** Ein Agent läuft in Trendo als Schleife Plan → Werkzeug → Ergebnis mit Protokoll, Grenzen, Stopp und Freigabe-Tor; Auslöser/Zeitplan über die vorhandenen Abläufe; KI-Team mit Datei-Übergabe und Prüfer-Rückgabe.
**Branch:** claude/T-0698-agenten (Basis claude/T-0685-b1-integration)
**Grundlage:** Audit-Teilbericht 4 (`docs/audit/2026-11-16-profi-audit/audit-4-ki-wissen.md` auf `claude/T-0674-buero-etappe`): STUDIO-AGENTS-01, CORE-03, SYS-05.
**Dateien/Bereich:** `src/Trendo.Core/W2/Agents.cs`, neu `src/Trendo.Core/W2/AgentRuns.cs`, `src/Trendo.Core/W2/Workflows.cs`, `src/Trendo.App/MainWindow.Agents.cs`, neu `src/Trendo.App/MainWindow.AgentRun.cs`, `src/Trendo.App/MainWindow.KreativStudios3.cs` (nur Teil „KI-Agenten bauen“), `src/Trendo.App/MainWindow.Workflows.cs`, Tests, `reports/T-0698/`.
**Fertig, wenn:**
- [x] Agent mit Ziel, Werkzeugen aus fester Liste, Modell (Haupt-KI / lokal / Claude-Abo), Grenzen (Schritte, Minuten) läuft; jede Werkzeugnutzung im Protokoll; Ergebnis als Datei im Projekt
- [x] Wirkungen nach außen nur über `ApprovalGate`; Stopp-Knopf
- [x] Schritt-Art „Agent ausführen“ in den Abläufen (Zeitplan, Neue-Datei-Auslöser) ohne zweiten Scheduler
- [x] Team: Datei-Übergabe, Prüfer kann zurückgeben, Prüfer parallel, Stopp
- [x] Tests mit ersetztem Modell (gekennzeichnet) und echte Läufe mit Ollama und Claude-Abo, Beleg unter `reports/T-0698/`
- [x] Oberfläche: Agenten-Liste, Bearbeiten, Lauf-Ansicht mit Live-Protokoll, goldene Hauptaktion „Agent starten“, erster Schritt im Leerzustand, im Bild geprüft
**Nicht tun:** `MainWindow.ProgramShell.cs`, Claude-Anbindung selbst, `SimpleWords.cs` ändern; zweites Agenten-Laufzeitsystem oder zweiter Scheduler; Volltest.
**Am Ende:** Branch pushen, Bericht `tasks/T-0698-agenten.bericht.md`.
