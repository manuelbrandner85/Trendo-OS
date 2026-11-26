# Verträge-Ordner – Testplan

Studio: Verträge-Ordner (STUDIO-CONTRACTS-01) · Bereich: Arbeiten & Büro · Seite: `vertraege-programm` · Stand: 0.9.110 (Stand 0eb9d52)

7 Tests sprechen dieses Programm an.

- Arbeiten & Büro A: alle drei Studios haben eine eigene Seite und öffnen ihre Vorlagen
- STUDIO-CONTRACTS-01 Adapter: spätestes Kündigungsdatum mit Ampel und Kosten – ohne Nebenwirkung, Pflichtfeld Beginn
- T-0705 Absturz in allen sieben Büro-Studios: Änderung per Klick, Sicherungstakt (≤ 20 s), Fenster stirbt ohne Speichern – das neue Fenster bietet „Wiederherstellen“ an und bringt jede Änderung zurück
- T-0705 Echtlauf Karten und Verträge: Druck-PDF der Karte und Kündigungsschreiben – qpdf prüft, LibreOffice öffnet beide; Kalenderdatei mit Erinnerung protokolliert
- T-0705 Formulare, Verträge, Bewerbung, Karten: Datei auf die Arbeitsfläche ziehen öffnet bzw. hängt an; Rechtsklick am Vertrag schreibt die Kündigung als PDF
- T-0705 Klick bis Datei – Verträge: leerer Ordner, „Ersten Vertrag eintragen“, Felder getippt, Erinnerung als Kalenderdatei mit Alarm und Kündigung als PDF
- Verträge: Liste mit Ampel und Kosten, Vertrag wählen und Frist ändern, Erinnerungen als Kalenderdatei, Kündigung an „Briefe schreiben“, als gekündigt markieren
