# TRENDO OS – Design-Referenzpaket zu Verbesserung 41

**AKTUALISIERUNG v2:** Jedes Studio muss direkt aus dem Trendo-Startmenü wie unter Windows als eigenes Trendo-Arbeitsfenster starten, mit eigener Taskleistenrepräsentation. Die ergänzte verbindliche Spezifikation **UI41-SM-01** steht im Masterprompt. Kein bloßer Sprung zur Studioübersicht. Die Referenzbilder selbst sind unverändert.

Dieses Paket enthält **zwei große, neu erzeugte Designatlanten**, **zehn einzeln zugeschnittene Referenzansichten** und **einen vollständigen technischen Claude-Code-Auftrag als Markdown**.

## Übergabe an Claude Code

1. Die ZIP-Datei für Claude Code im Repo bereitstellen und entpacken lassen bzw. Claude Code die entpackten Dateien zugänglich machen.
2. `CLAUDE_CODE_DESIGN_41_MASTERPROMPT.md` vollständig lesen lassen.
3. Claude Code soll **alle** Dateien im Ordner `referenzen/` als **visuelle Soll-Referenzen** verwenden.
4. Der Entwicklungsauftrag bezieht sich auf das bestehende GitHub-Repository: https://github.com/manuelbrandner85/Trendo-OS
5. Claude Code soll zunächst einen **realen testbaren Windows-/Avalonia-Design-Vertical-Slice** liefern.
6. **Nach dem ersten Desktop- und Briefstudio-Design unbedingt die gesonderte JA/NEIN-Freigabe einholen, bevor alle Studios geändert werden.**

## Dateien

- `CLAUDE_CODE_DESIGN_41_MASTERPROMPT.md` – kompletter ausführbarer Auftrag, inkl. 20 bestätigter Designfunktionen und Testplan.
- `referenzen/00_DESIGN_ATLAS_A.png` – Gesamtbild A.
- `referenzen/00_DESIGN_ATLAS_B.png` – Gesamtbild B.
- `referenzen/01_DESKTOP_PREMIUM.png` bis `10_DATEIMANAGER.png` – Einzelreferenzen.
- `REFERENZEN_MANIFEST.json` – Dateinamen und Beschreibungen.

**Wichtig:** Die Referenzbilder sind seit 2026-10-24 mit Claude Code gebaut: Oberflächen als HTML (`tools/design/views.py`, Rendern mit `python tools/design/build.py`), Hintergründe lokal mit Bild-KI (FLUX.2 klein, Herkunft in `docs/design/HINTERGRUENDE.json`). Sie sind Soll-Bilder, keine Screenshots des realen Trendo OS. Studionamen und Symbole kommen aus `StudioCatalog.cs`/`StudioIcons.cs`; Beispieldaten nicht übernehmen.

### Kurzer Satz zur Übergabe

> „Claude Code: Öffne `CLAUDE_CODE_DESIGN_41_MASTERPROMPT.md` und nutze alle Bilder im Ordner `referenzen/`. Beauftrage Claude Code mit UI41-0 bis UI41-6 im echten bestehenden Trendo-OS-Repository. Baue und teste echte Avalonia-Oberflächen. Stelle mir danach die Vorher-/Nachher-Screenshots zur gesonderten JA/NEIN-Designabnahme vor.“
