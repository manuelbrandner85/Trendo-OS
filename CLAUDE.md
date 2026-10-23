# Trendo OS – Claude Code GODMODE 2.0 – Projektregeln

## Zweck und Start
Weiterentwicklung von `manuelbrandner85/Trendo-OS`, kein Rewrite. Haupt-Chat fungiert als **Master Director**. 20 Spezialagenten unter `.claude/agents/` nur bedarfsabhängig delegieren (normalerweise 1–3). Beginne jede neue Sitzung mit dem **read-only Audit** `/trendo-audit`. Danach `/trendo-plan` für kleine, genehmigte Aufträge. Antworten und UI-Texte auf Deutsch.

## Repositorium / Verifikation
- Code: `src/` (Trendo.Core, Trendo.App, Trendo.Api, Trendo.Companion), Tests `tests/`, Spezifikation `docs/spec/` (Kataloge `docs/spec/kataloge/`), Status `requirements.yaml`, Entscheidungen `docs/spec/decisions.yaml`, Belege `reports/`, Build-Log `docs/build-log/<datum>-<modul>.md` (eine Datei pro Eintrag), Aufträge `tasks/`. **Alles bei Laufzeit erneut prüfen**.
- Drei Editionen: Windows, Linux USB/SSD, Pi 4 8GB + Windows-PC.
- Build/Test: `dotnet build Trendo.sln -c Release`, `dotnet test Trendo.sln -c Release` (deckt Core-, App- und Api-Tests ab; Ausgabe deutsch „Bestanden!/Fehler!“). Paket-Tests: `python -m unittest discover -s tests/claude_handoff`. Keine erfundenen Ausgaben.
- Der Projektordner enthält ein Leerzeichen: Pfade immer in Anführungszeichen.

## Rollen und Ablauf (M2, festgelegt von Manuel am 2026-10-23)
| Aufgabe | Wer | Bedingung |
|---|---|---|
| Code schreiben | Claude Code | Eigener Branch `claude/<task-id>-<kurz>` pro Auftrag, kleine Commits |
| Push | Claude Code | Nur nach grünen Tests (alle drei Testprojekte + Paket-Tests). Push auf `main` und `--force` sind erlaubt; `--force` nur mit Begründung im Bericht, bevorzugt `--force-with-lease` |
| Review | Claude-QA | QA-Agent (`19-qa-verification`) und bei sicherheitsrelevanten Änderungen Security-Agent (`03-security-permissions`) prüfen unabhängig vom Implementierer und schreiben das Ergebnis in den PR |
| Merge nach `main` | Claude Code | **Nur nach ausdrücklichem OK von Manuel im Chat, pro PR** |
| Release (`installer/release.sh`, Setup) | Claude Code | **Nur nach ausdrücklichem OK von Manuel im Chat, pro Release**, nur aus grünem `main` |

Ablauf pro Auftrag: Auftrag in `tasks/T-<nr>-<kurz>.md` (Vorlage `tasks/VORLAGE.md`) → Branch → Umsetzung → Tests → Bericht `tasks/T-<nr>-<kurz>.bericht.md` (mitcommitten) → Push → PR → Claude-QA-Review → Merge nach OK.

## Entscheidungsregister
- Priorität: reale Codebasis (Iststand) -> kanonische `docs/spec/` und `requirements.yaml` -> neueste ausdrückliche Nutzerfreigaben in `docs/claude/TRENDO_GODMODE_ENTSCHEIDUNGEN_2026-10-23.md` -> `handoff/`. Widersprüche nicht eigenmächtig in `decisions.yaml` überschreiben, sondern Decision-Delta vorlegen. `decisions.yaml` und `requirements.yaml` ändert nur Manuel oder ein ausdrücklich freigegebener Auftrag.
- Bestätigt: Verbesserungen 42–85 und 87–96. Verbesserung 86 NEIN. Vorher 23,25,34 NEIN, 35 OFFEN, Language Studio 2.0 NEIN. Verbesserung 97 OFFEN. SPEC-07 abgelehnt – nicht bauen. Das Logo bleibt ein nicht freigegebenes Konzept.
- 120-Stunden-Forensikregel: Forensik-Kopien und Derivate höchstens 120 h, 24 h vorher Warnung, keine dauerhaften Offline-USB-Kopien, kein Browser-/Tor-READ-ONLY-Bypass, keine forensischen Beweiskopien in Cloud-Diensten. Niemals umdeuten (im Code: `ForensicsLab.MaxLife`).
- `APPROVED_SPEC` bedeutet nicht `IMPLEMENTED`, `TESTED`, `ACCEPTED` oder `RELEASED`.

## Status und Nachweise
- `TESTED`/`PC_VERIFIED` nur mit Rohlog + SHA-256 unter `reports/`. Statuswechsel nur mit echten Artefakten und Logs.
- QA-017, QA-019 und die Codesignatur nie als bestanden markieren.
- W1-Issues (#14, #15, #17–#22) und W7-Issues (#64–#66) bleiben offen, bis die Module fertig sind.

## Architekturgrundsätze
- Kein zweiter KI-Kern, Workflow-Scheduler, Projektregister, Agenten-Laufzeitsystem, Sicherheitsbroker, StudioCatalog oder QA-System. Bestehende Module finden, dann erweitern (`IntelligenceCore`/`Intel.AskAsync`, `PromptLibrary`, `AgentCenter`, `WorkflowEngine`, `ApprovalGate`, `Rbac`, `AuditLog`, `ProjectStore`, `StudioRegistry`, `StudioCatalog`) – vor Nutzung im Code nachweisen.
- Ziel: ONE Workspace je Studio, deutsche, alltagsnahe Texte, höchstens zwei Navigationsebenen. Studio-Namen kommen zentral aus `src/Trendo.App/StudioCatalog.cs`. Erst echter funktionsfähiger Vertical Slice mit editierbarem Artefakt, dann UI-Politur.
- Kette: Prompt -> validierter Auftrag -> Capability -> Rechteprüfung -> geplante Aufgaben -> Zustimmung bei sensiblen Aktionen -> echter Fachadapter -> echte Dateien -> QA -> Vorschau -> Export.
- Projektgrenzen, Nutzerrechte, OS-Prüfungen und Freigaben serverseitig durchsetzen. Hooks und `permissions` sind zusätzliche Leitplanken, kein Sandbox-Ersatz.
- Kostenpflichtige KI-APIs bleiben optional; Trendo darf sie nie voraussetzen. Claude Code arbeitet mit vorhandenem Claude-Zugang, kein API-Zukauf ohne Freigabe. Lokale Modelle nur wenn nachweislich verfügbar.

## Arbeitsweise
1. Kleine, gezielte Änderungen; nur die im Auftrag genannten Dateien/Bereiche anfassen. Implementierungsagenten möglichst in Worktrees (`isolation: worktree`), maximal 2–3 parallel, nie gemeinsam an einer Datei.
2. Fachagenten liefern reale Git-Diffs, Dateien, Builds und Testprotokolle. Der Hauptagent integriert seriell und führt die Tests erneut aus.
3. Wird ein Befehl abgelehnt oder braucht er eine Freigabe: nicht wiederholen, die erlaubte Form nehmen oder aufhören und den Befehl im Bericht unter „Offene Punkte“ nennen.
4. Rote Tests: nicht pushen, Ursache berichten.

## Verboten
- Admin-Rechte, systemweite Installationen, `winget`/`msiexec`/`choco`, Registry-Änderungen, Dienste, Treiber, Firmware, Datenträger, Windows-Einstellungen.
- Blender, Unreal oder Visual Studio deinstallieren/ändern.
- Secrets in Dateien/Commits; Benutzernamen, Rechnernamen oder Pfade mit Benutzernamen im öffentlichen Repo (in Logs durch `C:\Users\<user>` / `<pc>` ersetzen).
- Nachrichten senden, etwas kaufen, Formulare absenden. Merge und Release nur nach OK (siehe Rollen).
- Dateien außerhalb dieses Repos ändern oder löschen; Ausnahme: eigene Temp-Ordner `%TEMP%\trendo*`, am Ende aufräumen.
- Gefährliche Realwelt-Aktoren (Maschinen, Fahrzeuge, Geräte) ohne eindeutige aufgabenbezogene Freigabe.

## Betriebswerkzeuge
- Python ≥3.10 für die Projekt-Tools; Prüfung mit `python tools/trendo_ops.py doctor`.
- Job-System `python tools/trendo_ops.py job ...`: SQLite-Status und JSONL-Ereignisse in `.trendo-local/` (lokal, nie committen). Kein Job läuft durch das Vorhandensein einer Datei automatisch.
- `python tools/trendo_dashboard.py` startet eine **read-only Ansicht** auf 127.0.0.1:8765 mit nur lokal erfassten Ereignissen.
- `.mcp.json` enthält ausschließlich den lokalen READ-ONLY MCP-Adapter, keine behaupteten Blender-/Unreal-MCP-Verbindungen.
- `tools/blender/` und `tools/unreal/` sind CLI-Prüfhilfen, **kein** getesteter End-to-End-MCP-Server. Blender/Unreal erst nach Installation und Capability-Check verbinden.

## Abschlussbericht (`tasks/<task-id>.bericht.md`)
Auftrag · Branch/Worktree · geänderte Dateien · Kommandos · Testergebnis je Projekt (Zahlen aus der Ausgabe, Exitcodes) · Artefaktpfade · offene Punkte/Risiken · was NICHT geprüft wurde · Status. Keine erdachten Ergebnisse.

## Herkunft
Seit 2026-10-23 wird Trendo OS ausschließlich mit Claude Code entwickelt. Die früheren Projektregeln (`AGENTS.md`) sind vollständig hier übernommen, die Werkzeuge des Vorgängers entfernt (siehe `docs/claude/MIGRATION_ZU_CLAUDE.md` und `docs/adr/0009-claude-code-als-einziger-entwicklungsagent.md`).
