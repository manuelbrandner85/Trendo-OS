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

## Nachtrag D7 (2026-10-25): Startzeit und RAM gemessen
- **Warum keine Datei entstand:** `--startup-log` wurde nur beim ersten gezeichneten Bild geschrieben. Der erste Messversuch hat das Programm vorher beendet. Direkt nach einem Neubau kam das erste Bild erst nach 12–16 s.
- **Behoben:** Das Protokoll wird jetzt auch beim Öffnen des Fensters, nach 5 s und beim Schließen geschrieben. Test `StartupTraceTests`.
- **Messung** (Ryzen 7 5700X, 32 GB, RTX 5070, Windows 11), Rohdaten und SHA-256 in `reports/d7-startmessung/`, Messskript `tools/messung/startmessung.ps1`:
  - Setup-Inhalt 0.9.7 (ReadyToRun): erstes Bild nach **1,3 / 1,4 / 2,3 s**, Arbeitsspeicher etwa 245 MB (Working Set), 500–580 MB privat.
  - Entwickler-Build: 2,2 / 2,3 / 2,5 s.
  - Ausreißer direkt nach einem Neubau: 12,7 und 16,5 s. Fast die ganze Zeit vergeht vor dem ersten Programmcode (`main` erst bei 14 s). Das passt zur Prüfung neuer, unsignierter Dateien durch Windows. Belegt ist es nicht; beim nächsten Start ist es weg.
- **Ziel 1 s:** nicht erreicht. Warm liegen die Werte bei 1,3–2,3 s.
- **Nicht gemessen:** ein echter Kaltstart nach Neustart des PCs und die über das Setup installierte Version.

## Tests (dieser Prüfauftrag)
Release-Gesamtlauf 2026-10-25 (Windows-PC): Build 0 Warnungen, 0 Fehler · Api 15/15 · Core 543 bestanden, 1 übersprungen (von 544) · App 301 bestanden, 1 übersprungen (von 302), 31 min 46 s · Paket-Tests OK.
