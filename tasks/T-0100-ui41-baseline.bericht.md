# T-0100 Bericht: Vorher-Stand am Windows-PC

**Auftrag:** Screenshots und Messwerte der Windows-App, bevor Design 41 umgesetzt wird. Nur `reports/design-41/pc-vorher/`. Kein Code.
**Branch:** `cursor/bauplan-t0005-t0012-f36c`

Der Ordner `reports/design-41/pc-vorher/` fehlt. Diese Sitzung ist Linux und hat den Windows-PC nicht fotografiert. Kaltstart, Warmstart und RAM am PC sind hier nicht gemessen.

Design 41 liegt bereits auf `main` (Merge von PR #237, `2eef442`). Ein Vorher-Bild vom PC lässt sich danach nicht mehr nachholen, ohne den alten Stand extra zu bauen. Das ist nicht geschehen.

## Geänderte Dateien

Keine für die Vorher-Bilder. Dieser Bericht.

## Testergebnis

Die Suite aus T-0005 prüft diesen Auftrag nicht. Api 15 grün, Core 531 grün plus 10 übersprungene `WinRealTests`, App 222 grün. Zahlen vor dem letzten Hinweistext im 3D-Studio.

## Offene Punkte

- Sieben Ansichten in 1366×768 und 1920×1080, DPI wie am PC, Startzeit und RAM mit Hardwareangabe fehlen.
- Kein Status auf `TESTED` oder `PC_VERIFIED`.

## Nicht geprüft

- Kein Windows-PC, kein `--screenshot` auf dem PC, keine StartupTrace, kein RAM-Wert.
