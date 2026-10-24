# T-0105 Bericht: UI41-2c Studioübersicht

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC) · **Prüfung gegen Ist-Stand:** `main` @ `979766e`, 2026-10-25
**Status:** ERLEDIGT (mit T-0218)

## Befund
- „Alle Studios“ nach Referenzbild 2: Bildkarten für alle 54 Studios mit Studio-Symbol, Name, Satz und echtem Status. Dazu Bereichs-Chips und Suche.
- Tests:
  - `Design41RolloutTests.Studio_filter_and_search`
  - `StudioIconTests` (Symbol je Karte, deutsche Texte, Bereiche)
  - `StudioClickTests.Alle_Studios_Karten` (jede Karte öffnet das Studio)
  - der Tote-Tasten-Test
- Tastatur: Karten sind Buttons mit `IsTabStop`. Die Tab/Enter-Bedienung prüft QA-017 allgemein. Ein eigener Test „jede Karte per Tastatur“ existiert nicht.

## Status
ERLEDIGT. Offen: ein Tastatur-Einzeltest je Karte.

## Tests (dieser Prüfauftrag)
Release-Gesamtlauf 2026-10-25 (Windows-PC): Build 0 Warnungen, 0 Fehler · Api 15/15 · Core 543 bestanden, 1 übersprungen (von 544) · App 301 bestanden, 1 übersprungen (von 302), 31 min 46 s · Paket-Tests OK.
