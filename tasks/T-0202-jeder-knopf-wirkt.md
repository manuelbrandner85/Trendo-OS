# T-0202: Jeder Knopf wirkt sichtbar (Erweiterung von T-0004)

**Ausführung:** Box-Agent (nicht Grok Build). Windows-Nachweise am PC über den Integrator.

**Ziel:** Kein Knopf bleibt ohne Reaktion; leere Eingaben führen zu einem klaren Hinweis statt zu nichts oder einem Absturz.
**Branch:** box/T-0202-jeder-knopf-wirkt
**Dateien/Bereich:** betroffene `src/Trendo.App/MainWindow.*.cs` (Liste in `docs/audit/LOGIK_AUDIT.md`, Abschnitt „Ohne sichtbare Reaktion“ und „Absturz“), neuer Test `tests/Trendo.App.Tests/EveryButtonTests.cs`.
**Befund:** Headless-Klick-Audit (`reports/audit/`): 6 Abstürze (Geld umrechnen, Pipette, Betrieb-Import, 3× Sicherung „Wiederherstellen“) und 36 Knöpfe ohne jede sichtbare Wirkung bei Standardeingaben; 23 Schalter (CheckBox/RadioButton) und 9 Netz-/Hintergrund-Knöpfe konnten nicht geprüft werden.
**Fertig, wenn:**
- [ ] Test klickt auf jeder Seite (`_showMap`) jeden sichtbaren, aktiven Knopf und verlangt: Seitenwechsel ODER gestartetes Programm ODER geschriebene Datei ODER geänderter sichtbarer Text; Ausnahmen nur über eine begründete Liste im Test.
- [ ] Kein Klick wirft eine Ausnahme (Test).
- [ ] Knöpfe, die eine Eingabe brauchen, sind deaktiviert mit Tooltip-Grund oder zeigen „Bitte zuerst … eingeben“.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Knöpfe nicht einfach entfernen, um den Test zu bestehen; riskante Knöpfe (Löschen, Installieren) im Test nur mit Fake-Launcher.
**Am Ende:** Branch pushen, Bericht. PR über den üblichen Ablauf (kleiner PR, main grün).
