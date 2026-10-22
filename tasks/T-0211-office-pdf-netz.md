# T-0211: PDF-Formulare, Druckvorschau und Downloads funktionsfähig machen

**Ausführung:** Box-Agent (nicht Grok Build). Windows-Nachweise am PC über den Integrator.

**Ziel:** PDF-Formular ausfüllen, Druckvorschau und Download-Verwaltung tun, was sie sagen.
**Branch:** box/T-0211-office-pdf-netz
**Dateien/Bereich:** `MainWindow.Sys10.cs`, `MainWindow.Sys03.cs`, `MainWindow.W1Web.cs`, Tests.
**Befund:** ohne Wirkung: „Felder lesen“, „Ausgefüllt speichern“, „Vorschau“ (Druck), „Laden / Weiter laden“, „Anhalten“, „Sortieren“, „Offline speichern“, „Als PDF“, „Tor-Browser offiziell holen“.
**Fertig, wenn:**
- [ ] PDF per Dialog wählen → Felder erscheinen als Formular → ausgefüllte Kopie wird gespeichert und geöffnet.
- [ ] Druckvorschau zeigt Seitenbilder.
- [ ] Downloads: Fortschritt sichtbar, Anhalten/Weiter funktioniert; ohne Adresse klarer Hinweis.
- [ ] Webseite offline/als PDF speichern legt Datei an oder erklärt, warum nicht (offline-Modus).
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Am Ende:** Branch pushen, Bericht. PR über den üblichen Ablauf (kleiner PR, main grün).
