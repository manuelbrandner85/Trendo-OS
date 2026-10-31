# T-0250 – Kein Doppelstart von Abläufen (V110)

- Freigabe: Manuel im Chat am 31.10.2026 („ziehe erst voll durch danach die große Prüfung mit Release“) – Roadmap docs/claude/audit_98_113/ROADMAP_PRIORISIERT.md
- Branch: `claude/T-0245-status-beweis` (Sammelstand für 0.9.23)
- Ziel: WorkflowEngine: Zeitgeber-Durchlauf nicht überlappend, je Ablauf Sperre (Prozess + Sperrdatei, verwaist nach 2 h)
- Dateien: src/Trendo.Core/W2/Workflows.cs, tests/Trendo.Core.Tests/W2Tests.cs
- Abnahme: gezielte Tests grün, danach Volltest aller drei Testprojekte + Paket-Tests
