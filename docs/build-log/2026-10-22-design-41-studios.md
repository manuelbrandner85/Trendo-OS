# 2026-10-22 – Design 41 auf die übrigen Studios

Nach dem JA des Besitzers gilt die geprüfte Hülle für die übrigen Seiten: Glas, Katalogname mit Symbol und Satz, Glas-Reiter, eigenes Trendo-Fenster mit Taskleisten-Eintrag, Startmenü wie UI41-SM-01.

Neu im Look, Funktion unverändert: Alle Studios (Suche und Bereichs-Chips), Einstellungen (linke Abschnitte), KI (drei Spalten, Status „Nicht verbunden“), Leistung (echte CPU/RAM/GPU-Karten). Die Fachseiten behalten ihre Felder und Knöpfe.

Vergleich: `reports/design-41/vergleich/gruppen/` (1920×1080 und 1366×768). Bericht: `tasks/UI-041-PREMIUM-EXPERIENCE.bericht.md`.

`dotnet test Trendo.sln -c Release` auf dem fertigen Stand: Api Failed 0, Passed 15, Duration 326 ms. Core Failed 0, Passed 471, Skipped 10, Total 481, Duration 6 s. App Failed 0, Passed 206, Skipped 0, Total 206, Duration 11 m 57 s. Die 10 Übersprungenen sind `WinRealTests`.
