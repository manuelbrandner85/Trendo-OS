# Karten & Einladungen – Testplan

Studio: Karten & Einladungen (STUDIO-CARDS-01) · Bereich: Arbeiten & Büro · Seite: `karten-programm` · Stand: 0.9.114 (Stand 99cbb5c)

7 Tests sprechen dieses Programm an.

- Arbeiten & Büro A: alle drei Studios haben eine eigene Seite und öffnen ihre Vorlagen
- Karten: Seite wählen, Text anklicken und ändern, neuer Text, Gäste eintragen und Antwort wählen, Format wechseln, Druck-PDF und Serienkarten, CSV
- STUDIO-CARDS-01 Adapter: Karte aus Anlass anlegen, Platzhalter füllen, Gäste zählen – ohne Nebenwirkung, Pflichtfeld Anlass
- T-0705 Absturz in allen sieben Büro-Studios: Änderung per Klick, Sicherungstakt (≤ 20 s), Fenster stirbt ohne Speichern – das neue Fenster bietet „Wiederherstellen“ an und bringt jede Änderung zurück
- T-0705 Echtlauf Karten und Verträge: Druck-PDF der Karte und Kündigungsschreiben – qpdf prüft, LibreOffice öffnet beide; Kalenderdatei mit Erinnerung protokolliert
- T-0705 Formulare, Verträge, Bewerbung, Karten: Datei auf die Arbeitsfläche ziehen öffnet bzw. hängt an; Rechtsklick am Vertrag schreibt die Kündigung als PDF
- T-0705 Klick bis Datei – Karten: Vorlage unverändert, „Text anpassen“ klappt die Eigenschaften auf, eigener Text getippt, Druck-PDF enthält ihn
