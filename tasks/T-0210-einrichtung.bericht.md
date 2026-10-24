# T-0210 Bericht: Einrichtung: Auswahl sichtbar, Reparieren mit Rückmeldung

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC)

## Stand
Auf `main`: Design-Wahl mit sofortiger Vorschau und „Gewählt“, Paket-Wahl in der Zusammenfassung, Reparieren meldet Ergebnis (Tests `SetupFeedbackTests`). „Ordner wählen …“ entsteht über die allgemeine Pfadfeld-Erkennung.
## Geändert
- Test `Folder_pick_in_wizard`: Ordnerdialog wird geöffnet, Wahl in Feld und Einstellung übernommen.
- Statuspunkte der Einrichtung zeigen bei „Status mit Zeichen“ ✓ ⚠ ✕ (T-0212).
Es wird nichts ohne Rückfrage installiert.

## Tests
Gesamtlauf Release am 2026-10-24 (Stand inkl. T-0218, Windows-PC): `dotnet build Trendo.sln -c Release` 0 Warnungen, 0 Fehler · Trendo.Api.Tests 15/15 bestanden · Trendo.Core.Tests 542 bestanden, 1 übersprungen (von 543) · Trendo.App.Tests 300 bestanden, 1 übersprungen (T-0202-Klick-Audit, nur mit TRENDO_ALL_BUTTONS=1), 24 min 28 s · Paket-Tests `tests/claude_handoff` 8/8 OK.
