# Dokumente scannen – Testergebnisse

Studio: Dokumente scannen (STUDIO-DOCSCAN-01) · Bereich: Arbeiten & Büro · Seite: `scannen-programm` · Stand: 0.9.113 (Stand 1978f1a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 30 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 0

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
| T-0774 (PW-12): zwei KI-Teilläufe „alle n Seiten“ nacheinander, dann Rückgängig des zweiten – die Dateien des ersten Laufs bleiben unverändert liegen | ✓ bestanden |
| T-0774 2. Gegenprüfung H1: gesperrte Stelle „folder“ auch mit Leerzeichen, Tab und anderer Groß-/Kleinschreibung – abgelehnt, nichts geschrieben | ✓ bestanden |
| T-0774 2. Gegenprüfung M1: Fehler beim Übernehmen – eine fremde Datei, die währenddessen im Ergebnis-Ordner entsteht, bleibt liegen; nur eigene Dateien werden beiseitegelegt | ✓ bestanden |
| T-0774 2. Gegenprüfung N1: Rückgängig mit gesperrter KI-Datei wirft nicht – Schritt zurückgenommen, Meldung im Programm | ✓ bestanden |
| T-0774 2. Gegenprüfung N2: „pdf.ausfuehren“ ohne Werkzeug nach „tool=passwort“ fällt nicht still weg – deutlicher Fehler | ✓ bestanden |
| T-0774 3. Gegenprüfung M-a: Regel-Plan ohne KI mit unverstandenem Teil – Hinweis „Nicht verstanden und ausgelassen“, Ausführung läuft weiter bis zur PDF | ✓ bestanden |
| T-0774 3. Gegenprüfung M-b: Auswahlwerte ohne Rücksicht auf Groß-/Kleinschreibung („Komprimieren“, „Zusammenfügen“, „Dokument“) – zugeordnet statt Abbruch | ✓ bestanden |
| T-0774 3. Gegenprüfung N-a/N-b: Teilen meldet jedes Teil vor dem Schreiben; scheitert der Lauf, bleibt kein leerer, selbst angelegter „-teile“-Ordner zurück | ✓ bestanden |
| T-0774 Dokumente scannen: KI-Aktion „Fotos → PDF“ – Klick → Vorschau → Ja → PDF „Datum Titel.pdf“ mit 2 Seiten, EIN Rückgängig-Schritt, Rückgängig legt die PDF beiseite (KI ersetzt) | ✓ bestanden |
| T-0774 Dokumente scannen: der Knopf heißt ehrlich „Hinweis: Papier-Scanner“ (kein Scanner angeschlossen) und gibt weiter den Weg über Fotos an | ✓ bestanden |
| T-0774 ECHT: lokale KI (Ollama) – PDF-Werkstatt „Seiten 1–3 heraus“ und Scannen „Fotos → PDF“ im offenen Programm, echte PDFs, Rückgängig (nur mit TRENDO_T0774_ECHT) | übersprungen |
| T-0774 Gegenprüfung 1: zwei Fachaktionen („1-2“, dann „4-5“) oder „dokument.setzen“ danach – deutlicher Fehler, keine Datei, Dokument unverändert | ✓ bestanden |
| T-0774 Gegenprüfung 2: KI-Plan mit Werkzeug „Passwort“ (entfernen) – abgelehnt, keine entsperrte Kopie | ✓ bestanden |
| T-0774 Gegenprüfung 3: Vorschau-Zurückrollen bewegt keine KI-Datei – nach „Ergebnisliste leeren“ und neuer Vorschau mit „Nein“ liegt die PDF noch da | ✓ bestanden |
| T-0774 Gegenprüfung 4: KI setzt keinen Ablage-Ordner (absolut/UNC) für Scans – abgelehnt, keine PDF außerhalb | ✓ bestanden |
| T-0774 Gegenprüfung 5: Fehler beim Übernehmen (zweite PDF kaputt) – schon geschriebene Dateien werden zurückgenommen, Dokument unverändert | ✓ bestanden |
| T-0774 Gegenprüfung 6: Wiederholen bei belegtem Namen trifft keine fremde Datei – Meldung, beiseitegelegte Fassung bleibt | ✓ bestanden |
| T-0774 Gegenprüfung 8–10: „3 bis Ende“ → 3-z, Unbekanntes gemeldet; nach „Nein“ keine Meldung stehen; Regel-Planer setzt Modus „foto“ nicht ungefragt | ✓ bestanden |
| T-0774 PDF-Werkstatt: falsche Seiten („Seite 9“ bei 5 Seiten) – Fehler schon in der Vorschau, keine Datei, Dokument unverändert (KI ersetzt) | ✓ bestanden |
| T-0774 PDF-Werkstatt: „Seiten 1–3 heraus“ – Klick → KI-Plan → Vorschau ohne Datei → Ja → neue PDF mit 3 Seiten, EIN Rückgängig-Schritt, Rückgängig/Wiederholen bringen die Datei mit (KI ersetzt) | ✓ bestanden |
| T-0780 H1 (2. Gegenprüfung): Claude-Brücke – „befehl“, „klicken“ und „feld_setzen“ kommen an Quellen-Sperre, Zitatprüfung und Ordner-Sperre nicht vorbei; Schritt wird zurückgerollt und Claude bekommt den Fehler | ✓ bestanden |
| T-0780 H3: Claude-Brücke hält KiLocked ein – „sources“ (Referate) und „folder“ (Dokumente scannen) setzt Claude nicht, freie Felder weiter schon | ✓ bestanden |
