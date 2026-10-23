# ADR-0009: Claude Code als einziger Entwicklungsagent
**Status:** ACCEPTED · **Entscheidung:** 2026-10-23 durch Manuel · **Ersetzt:** ADR-0002

## Kontext
Bis 2026-10-23 wurde der Code von einem lokalen Agenten mit Cloud-Projektleitung geschrieben, gesteuert über `AGENTS.md`, eine eigene Rechtekonfiguration und ein Startskript. Ab jetzt wird Trendo OS nur noch mit Claude Code gebaut.

## Entscheidung
- Claude Code entwickelt, testet, reviewt (eigener QA-Agent) und pusht. Push auf `main` und `--force` sind erlaubt; `--force` nur mit Begründung im Bericht.
- Merge nach `main` und Releases macht Claude Code nur nach ausdrücklichem OK von Manuel im Chat, pro PR bzw. pro Release.
- Alle Projektregeln stehen in `CLAUDE.md`. `AGENTS.md`, die Rechtekonfiguration und das Startskript des Vorgängers sowie dessen Prompt- und Setup-Dokumente sind entfernt, ohne Sicherung.
- Branches heißen `claude/<task-id>-<kurz>`.

## Folgen
- Schutz liegt in `.claude/settings.json` (`permissions` + Hook `.claude/hooks/trendo_hook.py`). Diese Dateien darf Claude nicht selbst lockern; Änderungen daran wendet Manuel an.
- Die Prüfsummenlisten des früheren Übergabepakets und die 20 agentenspezifischen QA-Fälle (`GRK-001…020`) sind entfernt. Claude-spezifische Abnahmen stehen in `docs/claude/QA_RELEASE_MATRIX.md`.
- Testnachweise unter `reports/` und Einträge in `docs/build-log/` bleiben als Belege erhalten, weil `requirements.yaml` darauf verweist.
