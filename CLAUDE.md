# Trendo OS – Claude Code GODMODE 2.0 – Projektregeln

## Zweck und Start
Weiterentwicklung von `manuelbrandner85/Trendo-OS`, kein Rewrite. Haupt-Chat fungiert als **Master Director**. 20 Spezialagenten unter `.claude/agents/` nur bedarfsabhängig delegieren (normalerweise 1–3). Beginne jede neue Sitzung mit dem **read-only Audit** `/trendo-audit`. Danach `/trendo-plan` für kleine, genehmigte Aufträge.

## Repositorium / Verifikation
Im geprüften öffentlichen GitHub `main` sind `.NET 10`, `Avalonia`, `Trendo.sln`, `src/Trendo.App`, `src/Trendo.Core`, `src/Trendo.Api`, `src/Trendo.Companion`, `tests/`, `docs/spec/`, `requirements.yaml`, `AGENTS.md` (alt: Grok-spezifisch) und `tasks/` vorhanden. **Alles bei Laufzeit erneut prüfen**. Es gibt drei Editionen: Windows, Linux USB/SSD, Pi 4 8GB + Windows-PC.

## Entscheidungsregister
- Priorität: reale Codebasis (Iststand) -> kanonische `docs/spec/` und `requirements.yaml` -> neueste ausdrückliche Nutzerfreigaben in `docs/claude/TRENDO_GODMODE_ENTSCHEIDUNGEN_2026-10-23.md` -> `handoff/`. Widersprüche nicht eigenmächtig in `decisions.yaml` überschreiben, sondern Decision-Delta vorlegen.
- Bestätigt: Verbesserungen 42–85 und 87–96. Verbesserung 86 NEIN. Vorher 23,25,34 NEIN, 35 OFFEN, Language Studio 2.0 NEIN. Verbesserung 97 OFFEN. 120-Stunden-Forensikregel niemals ohne genaue vorhandene Definition umdeuten.
- `APPROVED_SPEC` bedeutet nicht `IMPLEMENTED`, `TESTED`, `ACCEPTED` oder `RELEASED`.

## Architekturgrundsätze
- Kein zweiter KI-Kern, Workflow-Scheduler, Projektregister, Agenten-Laufzeitsystem, Sicherheitsbroker, StudioCatalog oder QA-System. Bestehende Module finden, dann erweitern. `PromptLibrary`, `Intel.AskAsync` usw. **nur wenn im tatsächlichen Code nachgewiesen**.
- Ziel: ONE Workspace je Studio, deutsche verständliche Texte, höchstens zwei Navigationsebenen. Erst echter funktionsfähiger Vertical Slice mit editierbarem Artefakt, dann UI-Politur.
- Kette: Prompt -> validierter Auftrag -> Capability -> Rechteprüfung -> geplante Aufgaben -> Zustimmung bei sensiblen Aktionen -> echter Fachadapter -> echte Dateien -> QA -> Vorschau -> Export.
- Secrets niemals loggen oder ins öffentliche GitHub committen. Projektgrenzen, Nutzerrechte, OS-Prüfungen und Freigaben serverseitig durchsetzen. Hooks sind zusätzliche Leitplanken und kein Sicherheitssandbox-Ersatz.
- Claude Code arbeitet mit vorhandenem Claude-Zugang. Kein kostenpflichtiger API-Zukauf oder externe Anbieter ohne explizite Freigabe. Lokale Modelle nur wenn nachweislich verfügbar.
- Build: `dotnet build Trendo.sln -c Release`, `dotnet test Trendo.sln -c Release`. Tests zuerst auf gültiger lokaler Umgebung, keine erfundenen Ausgaben.

## Multi-Agent-Workflow
1. Master Director erstellt `/trendo-plan`: kleine Aufträge, definierte Datei-Zuständigkeiten, Abhängigkeiten, QA, Berechtigungen.
2. Requirements- und Security-Agent prüfen Spezifikation read-only.
3. Implementierungsagenten arbeiten möglichst in Claude-Code-Worktrees (`isolation: worktree`), maximal 2–3 parallel, nie gemeinsam an einer Datei.
4. Fachagenten liefern reale Git-Diffs, Dateien, Builds und Testprotokolle. Hauptagent integriert seriell und führt Tests erneut aus.
5. QA-/Security-Agenten prüfen unabhängig; Release-Agent erstellt nur Freigabeempfehlung.
6. Keine autonomen Pushes, Merges, Releases, Veröffentlichungen oder Geräte-/OS-/Maschinenaktionen. Freigaben müssen über echte Produkt- und Hostgrenzen gelten.

## Betriebswerkzeuge
- Python ≥3.10 vorausgesetzt für unsere optionalen Projekt-Tools. Installation über `python tools/trendo_ops.py doctor` prüfen.
- Job-System `python tools/trendo_ops.py job ...`: SQLite-Status und JSONL-Ereignisse in `.trendo-local/` (lokal, nie commiten). Kein Job läuft durch das Vorhandensein einer Datei automatisch.
- `python tools/trendo_dashboard.py` startet eine **read-only Ansicht** auf 127.0.0.1:8765 und zeigt nur lokal erfasste Ereignisse. Kein erfundener Live-Agentenstatus.
- `.mcp.json` enthält ausschließlich unseren lokal eingebauten READ-ONLY MCP-Adapter, keine behaupteten Blender-/Unreal-MCP-Verbindungen.
- `tools/blender/` und `tools/unreal/` sind echte CLI-Prüfhilfen, **kein** bereits getesteter End-to-End-MCP-Server. Blender/Unreal erst nach Installation und Lizenz-/Capability-Check verbinden.

## Migration
`AGENTS.md` enthält weiterhin **Grok-Build-spezifische** Regeln und einen früheren Merge-/Freigabeverantwortlichen. Nicht blind überschreiben. Deren Security-/Forensik-/QA-Regeln respektieren, Unterschiede als explizite Migration dokumentieren. Bei unklarem Besitzer oder Konflikt NICHT selbst freigeben.

## Abschlussbericht
Immer: Aufgabe, Branch/Worktree, geänderte Dateien, Kommandos, wirklich ausgeführte Tests mit Exitcodes, Artefaktpfade, offene Punkte, Risiken und Status. Keine erdachten Ergebnisse.
