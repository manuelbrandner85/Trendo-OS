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
