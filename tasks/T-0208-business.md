# T-0208: Online verkaufen / Business – Eingaben mit Rückmeldung, kein Absturz

**Ausführung:** Box-Agent (nicht Grok Build). Windows-Nachweise am PC über den Integrator.

**Ziel:** Jeder „Hinzufügen/Speichern“-Knopf im Business-Bereich sagt mir, was passiert ist oder was fehlt.
**Branch:** box/T-0208-business
**Dateien/Bereich:** `MainWindow.W5Ops.cs`, `MainWindow.W5Brand.cs`, `MainWindow.W5Market.cs`, `MainWindow.W5Launch.cs`, Tests.
**Befund:** 14 Knöpfe ohne Wirkung (z. B. Tarif anlegen, Mitglied anlegen, Farben berechnen, SEO prüfen, Test anlegen), „Übernehmen“ (Import) stürzt bei leerem Pfad ab.
**Fertig, wenn:**
- [ ] Jeder Knopf: Ergebnis sichtbar oder Hinweis „Bitte … ausfüllen“; Pflichtfelder markiert.
- [ ] Import per Dateidialog, kein Absturz (Test).
- [ ] Angelegte Einträge erscheinen sofort in der Liste und lassen sich bearbeiten und löschen.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Am Ende:** Branch pushen, Bericht. PR über den üblichen Ablauf (kleiner PR, main grün).
