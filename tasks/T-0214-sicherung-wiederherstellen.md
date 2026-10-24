# T-0214: Sicherung wiederherstellen stürzt ab

**Ausführung:** Box-Agent (nicht Claude Code). Windows-Nachweise am PC über den Integrator.

**Ziel:** „Wiederherstellen“ bringt meine Daten aus einer Sicherung zurück, ohne Absturz.
**Branch:** box/T-0214-sicherung-wiederherstellen
**Dateien/Bereich:** `src/Trendo.Core/Backup.cs`, Sicherungs-Ansicht in `src/Trendo.App/`, Tests.
**Befund:** Klick-Audit: „Wiederherstellen“ wirft `IOException` (Sicherungsdatei existiert schon) bzw. `UnauthorizedAccessException` (schreibgeschützte `.git`-Objekte im Projektordner).
**Fertig, wenn:**
- [ ] Vor dem Wiederherstellen zeigt Trendo, was überschrieben wird, und fragt einmal nach.
- [ ] Vorher wird automatisch eine Sicherung des aktuellen Stands angelegt (eindeutiger Dateiname).
- [ ] Schreibgeschützte Dateien (z. B. `.git/objects`) werden korrekt ersetzt; kein Absturz (Test mit Git-Projekt).
- [ ] Danach Meldung „Wiederhergestellt: n Dateien“.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Am Ende:** Branch pushen, Bericht. PR über den üblichen Ablauf (kleiner PR, main grün).
