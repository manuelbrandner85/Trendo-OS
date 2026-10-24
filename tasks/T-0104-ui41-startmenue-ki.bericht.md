# T-0104 Bericht: UI41-3 Startmenü und KI

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC) · **Prüfung gegen Ist-Stand:** `main` @ `979766e`, 2026-10-25
**Status:** ERLEDIGT

## Befund
- Startmenü mit Suche, Angeheftet, Zuletzt, A–Z, Kategorien und Nutzer/Power: `MainWindow.StartMenu.cs`.
- Startmenü-Tests (Win/Esc/Suche/Pins) grün; nach T-0218 neu ausgerichtet, sodass das Menü wieder über der ganzen Fläche liegt.
- KI in drei Spalten mit Eingabe unten (UI-041). Status nur echt: Test `Design41RolloutTests.Ki_reopen_keeps_status` („Nicht verbunden“).
- Die Antwort mit lokalem Ollama ist am PC über `KiEnterTests` abgedeckt. Kein Modellname „GPT-4o“, keine erfundenen Unterhaltungen.

## Status
ERLEDIGT. Vergleichsbilder in `reports/design-41/vergleich/`.

## Tests (dieser Prüfauftrag)
Release-Gesamtlauf 2026-10-25 (Windows-PC): Build 0 Warnungen, 0 Fehler · Api 15/15 · Core 543 bestanden, 1 übersprungen (von 544) · App 301 bestanden, 1 übersprungen (von 302), 31 min 46 s · Paket-Tests OK.
