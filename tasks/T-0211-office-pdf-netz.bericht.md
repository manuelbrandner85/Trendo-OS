# T-0211 Bericht: PDF, Druckvorschau, Downloads

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC)

## Stand vorher
PDF-Felder lesen/ausfüllen, Downloads mit Fortschritt/Anhalten/Weiter, Webseite offline/als PDF mit Offline-Hinweis waren auf `main` vorhanden (Tests `OfficeNetzFeedbackTests`).
## Geändert
- Druckvorschau zeigt Seitenbilder der ersten 4 Seiten (Poppler `pdftoppm`), sonst klarer Hinweis.
- „Ausgefüllt speichern“ öffnet die ausgefüllte Kopie.
- Test `Print_preview_pages`.

## Tests
Gesamtlauf Release am 2026-10-24 (Stand inkl. T-0218, Windows-PC): `dotnet build Trendo.sln -c Release` 0 Warnungen, 0 Fehler · Trendo.Api.Tests 15/15 bestanden · Trendo.Core.Tests 542 bestanden, 1 übersprungen (von 543) · Trendo.App.Tests 300 bestanden, 1 übersprungen (T-0202-Klick-Audit, nur mit TRENDO_ALL_BUTTONS=1), 24 min 28 s · Paket-Tests `tests/claude_handoff` 8/8 OK.
