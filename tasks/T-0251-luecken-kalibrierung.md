# T-0251 – Messlücken und Kalibrierprüfung

- Freigabe: Manuel im Chat am 31.10.2026 („ziehe erst voll durch danach die große Prüfung mit Release“) – Roadmap docs/claude/audit_98_113/ROADMAP_PRIORISIERT.md
- Branch: `claude/T-0245-status-beweis` (Sammelstand für 0.9.23)
- Ziel: DigitalTwin2: Lücken > 3× Median-Abstand zählen nicht; Vision.PixelToTable lehnt Seitenverhältnis-Abweichung > 10 % ab
- Dateien: src/Trendo.Core/DigitalTwin2.cs, src/Trendo.Core/Robotics.cs, src/Trendo.App/MainWindow.Spec10.cs, tests/Trendo.Core.Tests/W6Tests.cs
- Abnahme: gezielte Tests grün, danach Volltest aller drei Testprojekte + Paket-Tests
