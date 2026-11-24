# T-0763: System im Alltag – Alles prüfen, Reparieren, Leistung, Startmodus

**Ziel:** Restliste Rang 41 Teil 1 (Audit `audit-5-entwickeln-system.md`, SYS-13, X-02, X-09, SYS-01): je ein echter Alltagsnutzen statt Entwickler-Anzeige.
**Branch:** claude/T-0763-system-alltag
**Dateien/Bereich:** `src/Trendo.Core/Sys/Sys13.cs`, `src/Trendo.Core/W3/SelfHealing.cs`, `src/Trendo.Core/Sys/TaskManager.cs`, `src/Trendo.App/MainWindow.Sys13.cs`, `MainWindow.Qualitaet.cs`, `MainWindow.Heal.cs`, `MainWindow.Setup.cs`, `MainWindow.TaskManager.cs`, `MainWindow.W7.cs`, `MainWindow.axaml.cs` (Link auf der Systemseite), `tools/Trendo.Render` (Bild mit Prüfergebnis), Tests, `reports/T-0763/`.
**Fertig, wenn:**
- [ ] SYS-13: ein goldener Knopf „Alles prüfen“ mit Ampel je Bereich und nächstem Schritt; Entwickler-Prüfungen getrennt und ehrlich beschriftet; keine leeren Karten; veränderte Protokollzeile mit Stelle gemeldet (Test).
- [ ] X-02: Programmdateien gegen das Installationspaket prüfen und ersetzen (sonst „Setup erneut ausführen“); fehlende Zusatzprogramme an dieser Stelle über den vorhandenen Nachlade-Weg mit eigener Rückfrage nachholen; Quarantäne-Dateien per Knopf zurückholen.
- [ ] X-09: hängendes Trendo-Werkzeug nach Rückfrage beenden (nur eigene Ordner, per Kennung); Grafiklast je Programm nur, wenn ohne Admin sauber messbar; Netzlast je Programm ehrlich als nicht vorhanden; Lastprobe gegen Leistungsindikatoren als Beleg unter `reports/T-0763/`.
- [ ] SYS-01: klare Aussage „Desktop Edition für Windows 11, Linux/Pi nicht im Angebot“; Stick-Schreiber-Schutz unverändert und getestet.
- [ ] Betroffene Testklassen grün (kein Volltest – macht die Hauptsitzung).
**Nicht tun:** kein winget/msiexec/Admin, keine fremden Prozesse beenden, nichts an der echten Installation ändern, kein Volltest, kein Push nach main.
**Am Ende:** Branch pushen, Bericht `tasks/T-0763-system-alltag.bericht.md`.
