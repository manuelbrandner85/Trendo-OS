# GODMODE 2.0 – Architektur und 12 genehmigte Verbesserungen

**Status:** autorisierte Entwicklungsanforderung, keine Behauptung einer produktiv integrierten Lösung.

| ID | System | Technische Umsetzung | Test-/Abnahmevertrag |
|---|---|---|---|
| 01 | Autonomous Master Director | Haupt-Chat koordiniert nach `CLAUDE.md`, Agentendefinitionen, Taskplan; kein separater LLM-Daemon | Delegation dokumentiert, nur beauftragte Agenten |
| 02 | Intelligent Agent Router | Agenten-Descriptions/`docs/claude/AGENTEN_MATRIX.md`, Maximalparallelität 3 | Klare Dateiscopes, kein gemeinsamer Write |
| 03 | Skills GODMODE | `.claude/skills/*/SKILL.md`, inkrementelles Laden | Mindestens 10 gültige Skills, jeder mit Ein-/Ausgaben |
| 04 | Hooks & Security | `.claude/settings.json`, Python-Hooks (PreToolUse / PostToolUse / SubagentStart/Stop / Stop) | Negativtests blockieren definierte Muster; Host-Freigaben bleiben Pflicht |
| 05 | Blender–Unreal Bridge | `tools/blender/`, `tools/unreal/`, Formate/Validierung | Echte `.blend`/glTF, Logs, UE-Import nur nach Engine-Test |
| 06 | Persistent Jobs | SQLite mit transaktionalen Statusübergängen, manueller Resume und JSONL-Ereignissen | Neustart → Historie erhalten, keine Fake-Ausführung |
| 07 | Worktree Isolation | Claude Frontmatter `isolation: worktree`, optionale lokale `git worktree`-CLI | Branch ungleich main, gleiche Datei nicht parallel |
| 08 | QA/Repair | xUnit, Web-Tests, Blender/Unreal-Prüfer, Diff/Logs, kontrollierte Korrekturschleifen | Kein TESTED ohne tatsächlich bestandene Tests |
| 09 | Live-Dashboard | lokaler read-only HTTP-Server 127.0.0.1, SQLite/JSONL Events | Keine Demo-Zahlen als Live, keine Internet-Freigabe |
| 10 | Model/Token | Sonnet Standard, Opus für komplexe Architektur/Sicherheit, Profile pro Agent | Keine garantierten Kosten/Verfügbarkeit, reale Limits gelten |
| 11 | Decision Lock | Delta 42–96, Sperrentscheidungen, Reconciliation statt stiller Migration | 23,25,34,86 NEIN; 35/97 OFFEN; Forensik unverändert |
| 12 | Release Guardian | Read-only Reviewer, reale Tests/Hashes und manuelle Freigabe | Build/Tests/Release-Checks dokumentiert, kein eigener Merge |

## Statusmodell
`PROPOSED -> APPROVED_SPEC -> PLANNED -> IN_PROGRESS -> IMPLEMENTED -> TESTED -> ACCEPTED -> RELEASED` mit `FAILED/BLOCKED`. `TESTED` erfordert echte Logs. Keine Simulation oder Modellvorhersage als tatsächliches Testergebnis ausgeben.

## Grenzen und absichtlich NICHT implementierte Versprechen
- Die Claude-Subagenten existieren als Agentendefinitionen, sind keine autonom laufenden Dienste.
- Dashboard erfasst Hook-/Jobereignisse in echten Sitzungen; es errät keine Tokenkosten oder laufende Arbeit in getrennten Claude-Desktop-Sitzungen.
- Job-CLI kann Prozesse **auf ausdrücklichen lokalen Aufruf** starten. Kein rund um die Uhr laufender Cloud-Dienst oder garantierte Weiterarbeit bei ausgeschaltetem PC.
- Blender/Unreal-CLI-Helfer sind lokale Prüfbefehle, kein universelles MCP für Remote-Steuerung; für echte tiefgreifende Integration zuerst lokale Versionen und erlaubte Adapter validieren.
- Projektregeln in Prompts/Hooks sind nicht gleichwertig mit OS-Sandboxing. Sicherheitsrelevante Freigaben müssen in Trendo selbst serverseitig erzwungen werden.

## Primäre Testreihenfolge
1. `python tools/trendo_ops.py doctor`
2. `python -m unittest discover -s tests/claude_handoff -v`
3. `dotnet build Trendo.sln -c Release`
4. `dotnet test Trendo.sln -c Release`
5. Blender-Smoke-Test nur bei nachgewiesener Installation
6. Unreal-Build/Automation nur mit echtem `.uproject`, Editor und autorisierten Pfaden
7. UI-/End-to-End-/Recovery-/Security-Negativtests auf Zielhardware.
