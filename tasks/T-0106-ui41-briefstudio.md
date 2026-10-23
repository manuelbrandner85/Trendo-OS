# T-0106: UI41-4 Briefstudio als Referenzstudio nach Referenz 04

**Ziel:** Dreiteiliges Briefstudio: links Navigation (Neuer Brief, Vorlagen, Meine Briefe, Empfänger, Einstellungen), Mitte Formular + Editor mit Formatleiste, rechts echte DIN-5008-Vorschau (bei schmalem Fenster umschaltbar); Kopfaktionen Speichern / Exportieren / Als PDF / Senden.
**Branch:** claude/T-0106-ui41-briefstudio
**Dateien/Bereich:** `MainWindow.W1Letter.cs` und zugehörige Briefstudio-Views, Tests. Logik (`Letters.cs`, `LetterCase.cs`) nur nutzen, nicht umbauen.
**Fertig, wenn:**
- [ ] Testbrief mit Testdaten schreiben, speichern, erneut öffnen, DOCX- und PDF-Export (Test bzw. PC-Lauf mit Datei-Nachweis)
- [ ] Vorschau aktualisiert sich bei Eingabe (Test)
- [ ] „Senden“ läuft über das bestehende Freigabe-Gate (Test)
- [ ] Briefköpfe, Akten, Fristen funktionieren weiter
- [ ] Screenshot neben Ref 04 im Bericht
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** keine Musteradressen/Namen aus dem Bild, Studioname bleibt „Briefe schreiben“, keine anderen Studios.
**Am Ende:** Branch pushen, Bericht. Keinen PR öffnen.
