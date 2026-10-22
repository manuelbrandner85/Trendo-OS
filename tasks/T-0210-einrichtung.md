# T-0210: Einrichtung – Auswahl sichtbar, Reparieren mit Rückmeldung

**Ziel:** Im Einrichtungsassistenten sehe ich, was ich gewählt habe, und „Reparieren“ sagt, was es getan hat.
**Branch:** grok/T-0210-einrichtung
**Dateien/Bereich:** `MainWindow.Setup.cs`, `MainWindow.Heal.cs`, Tests.
**Befund:** Design-Wahl (Dunkel/Hell/Kontrast) und Paket-Wahl (KI, Büro, Programmieren, Medien, Briefe scannen) ändern im Assistenten nichts Sichtbares; 6 „Reparieren“-Knöpfe und „Jetzt prüfen und reparieren“ ohne Rückmeldung; „Ordner wählen …“ ohne Dialog.
**Fertig, wenn:**
- [ ] Gewählte Optionen sind markiert (Häkchen/Rahmen) und die Auswahl steht in der Zusammenfassung.
- [ ] Design-Wahl wirkt sofort als Vorschau.
- [ ] Reparieren zeigt Fortschritt und Ergebnis („LibreOffice: repariert“ / „fehlt – Installieren?“).
- [ ] „Ordner wählen …“ öffnet einen Ordnerdialog.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** nichts ohne Rückfrage installieren.
**Am Ende:** Branch pushen, Bericht. Keinen PR öffnen.
