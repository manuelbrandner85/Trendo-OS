# T-0100 Bericht: UI41-0 Baseline

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC) · **Prüfung gegen Ist-Stand:** `main` @ `979766e`, 2026-10-25
**Status:** ÜBERHOLT

## Befund
- Vorher-Bilder gibt es nur von der Linux-Box: `reports/design-41/ist-*.jpg` (start, studios, brief, ki, settings, system) und die Vergleiche in `reports/design-41/vergleich/`.
- Ein Vorher-Zustand am Windows-PC lässt sich nicht mehr aufnehmen. Design 41 ist auf `main` umgesetzt und ausgerollt (`tasks/UI-041-PREMIUM-EXPERIENCE.bericht.md`), und Startseite und „Alle Studios“ sind mit T-0218 erneut umgebaut.

## Versuch Startzeit/RAM am PC (Setup-Build 0.9.7)
- Arbeitsspeicher nach etwa 12 s: rund 304 MB bei einem Lauf.
- Ein erster Lauf zeigte nur 41 MB, vermutlich noch während des Starts. Deshalb nicht belastbar.
- `--startup-log` hat in beiden Läufen keine Datei geschrieben. Eine Startzeit ist deshalb **nicht belegt**. Ursache offen, siehe neuer Punkt im Fertigstellungsplan.

## Status
ÜBERHOLT – der Auftrag ist nicht mehr ausführbar. Die Messung von Startzeit und RAM ist offen.

## Tests (dieser Prüfauftrag)
Release-Gesamtlauf 2026-10-25 (Windows-PC): Build 0 Warnungen, 0 Fehler · Api 15/15 · Core 543 bestanden, 1 übersprungen (von 544) · App 301 bestanden, 1 übersprungen (von 302), 31 min 46 s · Paket-Tests OK.
