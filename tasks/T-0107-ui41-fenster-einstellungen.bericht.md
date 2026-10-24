# T-0107 Bericht: UI41-5 Fenster, Einstellungen, Leistungsprofile

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC) · **Prüfung gegen Ist-Stand:** `main` @ `979766e`, 2026-10-25
**Status:** ERLEDIGT (Leistungsprofile in diesem Auftrag ergänzt)

## Befund vorher
- Fenster mit weichen Schatten, aktiv/inaktiv deutlich (`RenderWindows`). Fenstertests grün.
- Einstellungen mit Bereichsnavigation: Test `Design41RolloutTests.Settings_sections`.
- „Bewegung reduzieren“ schaltet Übergänge ab: Test `SYS-12 (UI) … Bewegung aus = keine Übergänge`.
- **Es fehlte:** Leistungsprofile.

## Geändert
- `UserSettings.PowerProfile` (`auto` | `hoch` | `sparsam`) und `IsLowPower(arm)`: Bei `auto` gilt auf ARM (Raspberry Pi) automatisch „sparsam“.
- Bei „Sparsam“ entfallen Hintergrund-Unschärfe, alle Schatten und Effekte (auch die Fensterschatten) und alle Animationen (`MainWindow.LowPower`, `Motionless`).
- Auswahl unter Einstellungen → Bedienung: „Automatisch“, „Volle Effekte“, „Sparsam (Raspberry Pi)“.
- Tests:
  - `T0107_Leistungsprofil.Profile` (Core)
  - `Leistungsprofil_Ui.Sparsam` (App): keine Unschärfe, keine Effekte oder Übergänge auf der Startseite, Fenster ohne Schatten, Knöpfe in den Einstellungen

## Nicht geprüft
- Startzeit vorher/nachher am PC: `--startup-log` schrieb keine Datei (siehe T-0100).
- Echter Raspberry Pi.

## Tests (dieser Prüfauftrag)
Release-Gesamtlauf 2026-10-25 (Windows-PC): Build 0 Warnungen, 0 Fehler · Api 15/15 · Core 543 bestanden, 1 übersprungen (von 544) · App 301 bestanden, 1 übersprungen (von 302), 31 min 46 s · Paket-Tests OK.
