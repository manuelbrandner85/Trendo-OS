# T-0212 Bericht: Schrift & Farben: Schalter wirken

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC)

## Stand vorher
Schalter mit Ein/Aus-Zustand, einfacher Modus (weniger Kacheln, größere Knöpfe), Bewegung reduzieren (keine Übergänge) waren auf `main` vorhanden; „Status mit Zeichen“ änderte nur den Beispieltext.
## Geändert
- „Status mit Zeichen“ setzt ✓ ⚠ ✕ an echte Statuspunkte (Einrichtung/Status, Geräte überwachen).
- Tests `Symbols_on_real_status`, `Simple_mode_bigger_buttons`.
- Hinweis: Schalter wirken über einen Neuaufbau des Fensters (`Reopen`) – für Nutzer sofort sichtbar.

## Tests
Gesamtlauf Release am 2026-10-24 (Stand inkl. T-0218, Windows-PC): `dotnet build Trendo.sln -c Release` 0 Warnungen, 0 Fehler · Trendo.Api.Tests 15/15 bestanden · Trendo.Core.Tests 542 bestanden, 1 übersprungen (von 543) · Trendo.App.Tests 300 bestanden, 1 übersprungen (T-0202-Klick-Audit, nur mit TRENDO_ALL_BUTTONS=1), 24 min 28 s · Paket-Tests `tests/claude_handoff` 8/8 OK.
