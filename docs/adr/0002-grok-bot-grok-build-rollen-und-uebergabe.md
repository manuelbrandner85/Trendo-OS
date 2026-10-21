# ADR-0002: Rollen Grok Bot / Grok Build und Task-Übergabe
**Status:** ACCEPTED (aus Spezifikation) · **Datum:** 2026-10-21 · **Quelle:** SYSTEMBIBEL §12, GROK_ARCHITEKTUR_AGENTEN_MCP.md

## Entscheidung
- Grok Bot = Projektleitung (Cloud, geteilter Rechner → keine Secrets/Forensik dort).
- Grok Build = lokaler Coding-/Test-Agent auf dem Windows-PC, Regeln in `AGENTS.md`.
- Kein anderer Coding-Agent als Voraussetzung.
- Übergabe zunächst **manuell** über `tasks/TASK-*.md`; Ergebnisse als Branch/PR mit Berichten unter `reports/`. Ein automatischer Taskbroker erst nach eigener Auth-/Safety-QA.
- Pflichtfelder je Auftrag: task_id, requirement_ids, approved_scope_paths, objective, constraints, allow_network, allow_mcp, human_approval_required, expected_outputs, acceptance_tests, expiry, rollback_plan.

## Konsequenzen
Grok-Build-Verfügbarkeit ist unverifiziert (GRK-001 NOT_RUN) → lokale Arbeit BLOCKED bis TASK-0001 zurückkommt.
