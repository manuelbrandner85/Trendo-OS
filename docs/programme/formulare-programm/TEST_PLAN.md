# Formulare ausfüllen – Testplan

Studio: Formulare ausfüllen (STUDIO-FORMS-01) · Bereich: Arbeiten & Büro · Seite: `formulare-programm` · Stand: 0.9.114 (Stand 99cbb5c)

9 Tests sprechen dieses Programm an.

- FO-02/03/04 UI: auf der Seite tippen, ankreuzen, auswählen; Pflichtfeld-Hinweis verschwindet; geglättet speichern – Werte stehen fest im Text der Seite
- FO-05 UI: Formular ohne Felder – Text, Datum und Häkchen per Klick setzen, Unterschrift zeichnen und setzen, speichern
- FO-06 UI: „Meine Angaben“ speichern (IBAN geprüft), einsetzen – IBAN nur maskiert und nie im Dokument; beim Speichern steht sie in der PDF
- Formulare: eigene Seite im Studio; Beispiel-Antrag zeigt Felder auf der Seite und in der Liste, leere Pflichtfelder sind markiert
- STUDIO-FORMS-01 Adapter: Feldnamen werden „Meinen Angaben“ zugeordnet – ohne Werte, Pflichtfeld Felder
- T-0705 Absturz in allen sieben Büro-Studios: Änderung per Klick, Sicherungstakt (≤ 20 s), Fenster stirbt ohne Speichern – das neue Fenster bietet „Wiederherstellen“ an und bringt jede Änderung zurück
- T-0705 Echtlauf Formulare: AcroForm-PDF von LibreOffice öffnen, Felder auf der Oberfläche ausfüllen, speichern und geglättet speichern – qpdf liest die Feldwerte bzw. findet keine Felder mehr, LibreOffice öffnet beide
- T-0705 Formulare, Verträge, Bewerbung, Karten: Datei auf die Arbeitsfläche ziehen öffnet bzw. hängt an; Rechtsklick am Vertrag schreibt die Kündigung als PDF
- T-0705 Klick bis Datei – Formulare: kein Formular, „Formular öffnen …“, Felder auf der Seite ausgefüllt, gespeichert (Felder bleiben) und fertig geglättet (Werte fest in der Seite)
