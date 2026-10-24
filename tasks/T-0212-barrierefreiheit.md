# T-0212: Schrift & Farben anpassen – Schalter wirken sofort sichtbar

**Ausführung:** Claude Code am Windows-PC (seit 2026-10-24, ADR-0009).

**Ziel:** Einfacher Modus, Zeichen statt Farbe, Bewegung reduzieren und leichte Sprache ändern sofort sichtbar die Oberfläche.
**Branch:** claude/T-0212-barrierefreiheit
**Dateien/Bereich:** `MainWindow.Sys12.cs`, `MainWindow.Look.cs`, `UserSettings` (nur Lesen/Schreiben der Schalter), Tests.
**Befund:** Die vier Schalter lösen im Klick-Audit keine sichtbare Änderung aus; Zustand ist nicht erkennbar.
**Fertig, wenn:**
- [ ] Schalter zeigen Ein/Aus-Zustand.
- [ ] Einfacher Modus blendet Fachwerkzeuge aus und vergrößert Knöpfe (Test zählt sichtbare Elemente vorher/nachher).
- [ ] Status-Punkte bekommen Zeichen (✓ ⚠ ✗) (Test).
- [ ] Bewegung reduzieren schaltet Animationen ab (Test).
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Am Ende:** Branch pushen, Bericht. PR über den üblichen Ablauf (kleiner PR, main grün).
