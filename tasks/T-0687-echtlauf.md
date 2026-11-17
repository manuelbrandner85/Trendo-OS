# T-0687: B2+B3 – Ehrlicher Status und Echtlauf-Station

**Ziel:** Belegen statt behaupten: jedes externe Werkzeug auf dem PC echt über Trendos Code laufen lassen, Testschichten sichtbar machen, falsche Katalogaussagen berichtigen, Statusänderungen nur vorschlagen.
**Branch:** claude/T-0687-echtlauf (von claude/T-0685-b1-integration)
**Grundlage:** Audit 2026-11-16, Teilbericht 6 (Abschnitte 3, 4, 7, Maßnahmen 8–10) und „Auffälligkeiten“ der Teilberichte 1–5.
**Dateien/Bereich:** `tools/` (neu `tools/echtlauf/`, Katalog-Teil von `tools/Trendo.Render/Program.cs`), `scripts/`, `tests/` (sechs stille Node/FFmpeg-Prüfungen), `reports/echtlauf/`, `src/Trendo.Core/PromptAction/CapabilityCatalog.cs` und `StudioKnowledge.cs` (nur falsche Aussagen), `docs/programme/katalog.json` + `KATALOG.md`, `docs/improvement-42/studio-capability-matrix.md` (erzeugt), Vorschlag `docs/audit/2026-11-16-profi-audit/requirements-delta.md`.
**Fertig, wenn:**
- [ ] `python tools/echtlauf/echtlauf.py` findet die Werkzeuge über Trendos Suchwege, fährt je Werkzeug einen echten Fachlauf und schreibt `reports/echtlauf/<datum>/` (Rohlog je Werkzeug, Version, Dauer, ECHT-GRÜN/ROT/BLOCKIERT, SHA256SUMS, ohne Benutzerpfade); einmal wirklich gelaufen und committet.
- [ ] Setup-Rundlauf als Skript (`scripts/setup-rundlauf.ps1`), ohne Eingriff in die echte Installation – oder ehrlich NICHT GETESTET mit Anleitung.
- [ ] `reports/echtlauf/testschichten.md` automatisch aus dem Quelltext (`tools/echtlauf/testschichten.py`); sechs stille `if (vorhanden)`-Prüfungen überspringen ehrlich.
- [ ] Falsche Aussagen in `CapabilityCatalog`/`StudioKnowledge` berichtigt; Katalog: entfernte Studios nicht als geprüft, Tests je Programm, Formate je Programm; Katalog und Matrix neu erzeugt.
- [ ] `requirements-delta.md` als Vorschlag (nicht selbst ändern).
**Nicht tun:** `src/Trendo.App/MainWindow*.cs` ändern, `requirements.yaml`/`decisions.yaml` ändern, installieren, anmelden, Volltest.
**Am Ende:** Branch pushen, Bericht `tasks/T-0687-echtlauf.bericht.md`.
