# Formulare ausfüllen – Testergebnisse

Studio: Formulare ausfüllen (STUDIO-FORMS-01) · Bereich: Arbeiten & Büro · Seite: `formulare-programm` · Stand: 0.9.112 (Stand d6f9fcd)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 9 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| FO-02/03/04 UI: auf der Seite tippen, ankreuzen, auswählen; Pflichtfeld-Hinweis verschwindet; geglättet speichern – Werte stehen fest im Text der Seite | ✓ bestanden |
| FO-05 UI: Formular ohne Felder – Text, Datum und Häkchen per Klick setzen, Unterschrift zeichnen und setzen, speichern | ✓ bestanden |
| FO-06 UI: „Meine Angaben“ speichern (IBAN geprüft), einsetzen – IBAN nur maskiert und nie im Dokument; beim Speichern steht sie in der PDF | ✓ bestanden |
| Formulare: eigene Seite im Studio; Beispiel-Antrag zeigt Felder auf der Seite und in der Liste, leere Pflichtfelder sind markiert | ✓ bestanden |
| STUDIO-FORMS-01 Adapter: Feldnamen werden „Meinen Angaben“ zugeordnet – ohne Werte, Pflichtfeld Felder | ✓ bestanden |
| T-0705 Absturz in allen sieben Büro-Studios: Änderung per Klick, Sicherungstakt (≤ 20 s), Fenster stirbt ohne Speichern – das neue Fenster bietet „Wiederherstellen“ an und bringt jede Änderung zurück | ✓ bestanden |
| T-0705 Echtlauf Formulare: AcroForm-PDF von LibreOffice öffnen, Felder auf der Oberfläche ausfüllen, speichern und geglättet speichern – qpdf liest die Feldwerte bzw. findet keine Felder mehr, LibreOffice öffnet beide | ✓ bestanden |
| T-0705 Formulare, Verträge, Bewerbung, Karten: Datei auf die Arbeitsfläche ziehen öffnet bzw. hängt an; Rechtsklick am Vertrag schreibt die Kündigung als PDF | ✓ bestanden |
| T-0705 Klick bis Datei – Formulare: kein Formular, „Formular öffnen …“, Felder auf der Seite ausgefüllt, gespeichert (Felder bleiben) und fertig geglättet (Werte fest in der Seite) | ✓ bestanden |
