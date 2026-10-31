# T-0254 – Automatische Sicherung und Wiederherstellung in Office (100)

- Freigabe: Manuel im Chat am 31.10.2026 („ziehe erst voll durch danach die große Prüfung mit Release“) – Roadmap docs/claude/audit_98_113/ROADMAP_PRIORISIERT.md
- Branch: `claude/T-0245-status-beweis` (Sammelstand für 0.9.23)
- Ziel: Sicherung alle 30 s in <Datenordner>/wiederherstellen, Banner Wiederherstellen/Verwerfen, Löschen nach Speichern
- Dateien: src/Trendo.App/MainWindow.OfficeRecovery.cs, ProgramShell.cs, Text/Tabelle/Präsentation, tests/Trendo.App.Tests/OfficeRecoveryUiTests.cs
- Abnahme: gezielte Tests grün, danach Volltest aller drei Testprojekte + Paket-Tests
