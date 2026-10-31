# T-0249 – Aufträge: Grenzen, Absturzerkennung, Maskierung (V110/V111)

- Freigabe: Manuel im Chat am 31.10.2026 („ziehe erst voll durch danach die große Prüfung mit Release“) – Roadmap docs/claude/audit_98_113/ROADMAP_PRIORISIERT.md
- Branch: `claude/T-0245-status-beweis` (Sammelstand für 0.9.23)
- Ziel: tools/trendo_ops.py: höchstens 3 gleichzeitig, Pfadliste Pflicht, keine Überschneidung, RUNNING ohne Prozess → INTERRUPTED, Geheimnisse in Ereignissen maskiert
- Dateien: tools/trendo_ops.py, tests/claude_handoff/test_package.py
- Abnahme: gezielte Tests grün, danach Volltest aller drei Testprojekte + Paket-Tests
