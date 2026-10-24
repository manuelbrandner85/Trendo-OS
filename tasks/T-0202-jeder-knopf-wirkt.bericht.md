# T-0202 Bericht: Jeder Knopf wirkt sichtbar

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC)

## Umgesetzt
- `tests/Trendo.App.Tests/ButtonProbe.cs`: klickt wie ein echter Klick (Häkchen/Auswahlpunkte schalten um), wartet bis 2 s auf verzögerte Wirkung (KI, Netz, Prüfung), zählt Seitenwechsel, Programmstart, Datei, Text im Fenster, Fenstertext und Neuaufbau nach Einstellung; laufende Uhren ausgenommen. Zusätzlich `RequireFresh` (frisches Fenster je Knopf).
- `EveryButton_Alltag` (bestehend, streng: jede Taste muss wirken) läuft mit dem verbesserten Prüfer.
- `EveryButtonAllPagesTests.cs`: Klick-Audit über **alle** Seiten (`PageKeys`), prüft hart: **kein Klick stürzt ab**; stumme Knöpfe als Bericht (`TRENDO_BUTTON_REPORT`). Läuft nur mit `TRENDO_ALL_BUTTONS=1` (ca. 35 min), sonst sichtbar übersprungen.

## Ergebnis Klick-Audit (erster Lauf, alter Prüfer)
0 Abstürze. 141 Treffer „ohne Wirkung“, überwiegend Messfehler des alten Prüfers (Hilfe-„?“ außerhalb des Fensterinhalts, Schalter ohne Umschalten, verzögerte Wirkung, Neuaufbau des Fensters) – mit dem neuen Prüfer behoben.

## Offen / nicht geprüft
- Der Lauf mit frischem Fenster je Knopf hing auf diesem PC nach ca. 2 h ohne CPU-Last (Ursache nicht gefunden); deshalb ist der Allseiten-Test auf „kein Absturz“ begrenzt und optional.
- Ein vollständiger neuer Allseiten-Lauf mit dem verbesserten Prüfer steht noch aus; die verbleibenden echten stummen Knöpfe sind daher nicht vollständig gelistet.

## Tests
Gesamtlauf Release am 2026-10-24 (Stand inkl. T-0218, Windows-PC): `dotnet build Trendo.sln -c Release` 0 Warnungen, 0 Fehler · Trendo.Api.Tests 15/15 bestanden · Trendo.Core.Tests 542 bestanden, 1 übersprungen (von 543) · Trendo.App.Tests 300 bestanden, 1 übersprungen (T-0202-Klick-Audit, nur mit TRENDO_ALL_BUTTONS=1), 24 min 28 s · Paket-Tests `tests/claude_handoff` 8/8 OK.
