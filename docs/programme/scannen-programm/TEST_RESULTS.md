# Dokumente scannen – Testergebnisse

Studio: Dokumente scannen (STUDIO-DOCSCAN-01) · Bereich: Arbeiten & Büro · Seite: `scannen-programm` · Stand: 0.9.110 (Stand 0eb9d52)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 9 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Dokumente scannen: eigene Seite; Fotos über „Fotos hinzufügen“ – Blatt erkannt, Ergebnis entzerrt; als PDF mit Datum + Titel abgelegt, nichts überschrieben | ✓ bestanden |
| SC-06: Seiten drehen, ordnen, entfernen und Modus je Seite/für alle – über Menüband, Werkzeugleiste und Seitenleiste | ✓ bestanden |
| SC-09: Ecke im Original mit der Maus ziehen ändert das Viereck; „Ganzes Bild“ und „Kanten finden“ | ✓ bestanden |
| SC-10/11/12: Ordner einlesen (natürlich sortiert), Texterkennung ohne Tesseract mit Einrichtungs-Hinweis, mit Tesseract durchsuchbare PDF; Scanner-Hinweis | ✓ bestanden |
| STUDIO-DOCSCAN-01 Adapter: Scan-Auftrag mit Dateiname und Ordner – ohne Nebenwirkung, Pflichtfeld Titel | ✓ bestanden |
| T-0705 Absturz in allen sieben Büro-Studios: Änderung per Klick, Sicherungstakt (≤ 20 s), Fenster stirbt ohne Speichern – das neue Fenster bietet „Wiederherstellen“ an und bringt jede Änderung zurück | ✓ bestanden |
| T-0705 Echtlauf Scannen: Seitenbild von LibreOffice ablegen, „Durchsuchbare PDF“ mit dem echten Tesseract – qpdf prüft, die Textebene enthält den Brieftext | ✓ bestanden |
| T-0705 Klick bis Datei – Scannen: keine Seite, „Fotos hinzufügen …“, Blatt gefunden, „Als PDF ablegen“ ergibt eine PDF mit der Seite | ✓ bestanden |
| T-0705 Scannen: Fotos auf den ersten Schritt ziehen, zweite Seite mit der Maus nach vorn ziehen, Rechtsklick „Drehen“ und „Seite entfernen“ | ✓ bestanden |
