# T-0253 – Storno statt Löschen in der Buchhaltung

- Freigabe: Manuel im Chat am 31.10.2026 („ziehe erst voll durch danach die große Prüfung mit Release“) – Roadmap docs/claude/audit_98_113/ROADMAP_PRIORISIERT.md
- Branch: `claude/T-0245-status-beweis` (Sammelstand für 0.9.23)
- Ziel: Ledger.Reverse (Gegenbuchung „Storno: “), Knopf „Stornieren“ statt Löschen
- Dateien: src/Trendo.Core (Ledger), src/Trendo.App/MainWindow.W5Money.cs, Tests
- Abnahme: gezielte Tests grün, danach Volltest aller drei Testprojekte + Paket-Tests
