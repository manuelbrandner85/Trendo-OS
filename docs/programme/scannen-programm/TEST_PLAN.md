# Dokumente scannen – Testplan

Studio: Dokumente scannen (STUDIO-DOCSCAN-01) · Bereich: Arbeiten & Büro · Seite: `scannen-programm` · Stand: 0.9.112 (Stand d6f9fcd)

29 Tests sprechen dieses Programm an.

- Dokumente scannen: eigene Seite; Fotos über „Fotos hinzufügen“ – Blatt erkannt, Ergebnis entzerrt; als PDF mit Datum + Titel abgelegt, nichts überschrieben
- SC-06: Seiten drehen, ordnen, entfernen und Modus je Seite/für alle – über Menüband, Werkzeugleiste und Seitenleiste
- SC-09: Ecke im Original mit der Maus ziehen ändert das Viereck; „Ganzes Bild“ und „Kanten finden“
- SC-10/11/12: Ordner einlesen (natürlich sortiert), Texterkennung ohne Tesseract mit Einrichtungs-Hinweis, mit Tesseract durchsuchbare PDF; Scanner-Hinweis
- STUDIO-DOCSCAN-01 Adapter: Scan-Auftrag mit Dateiname und Ordner – ohne Nebenwirkung, Pflichtfeld Titel
- T-0705 Absturz in allen sieben Büro-Studios: Änderung per Klick, Sicherungstakt (≤ 20 s), Fenster stirbt ohne Speichern – das neue Fenster bietet „Wiederherstellen“ an und bringt jede Änderung zurück
- T-0705 Echtlauf Scannen: Seitenbild von LibreOffice ablegen, „Durchsuchbare PDF“ mit dem echten Tesseract – qpdf prüft, die Textebene enthält den Brieftext
- T-0705 Klick bis Datei – Scannen: keine Seite, „Fotos hinzufügen …“, Blatt gefunden, „Als PDF ablegen“ ergibt eine PDF mit der Seite
- T-0705 Scannen: Fotos auf den ersten Schritt ziehen, zweite Seite mit der Maus nach vorn ziehen, Rechtsklick „Drehen“ und „Seite entfernen“
- T-0774 (PW-12): zwei KI-Teilläufe „alle n Seiten“ nacheinander, dann Rückgängig des zweiten – die Dateien des ersten Laufs bleiben unverändert liegen
- T-0774 2. Gegenprüfung H1: gesperrte Stelle „folder“ auch mit Leerzeichen, Tab und anderer Groß-/Kleinschreibung – abgelehnt, nichts geschrieben
- T-0774 2. Gegenprüfung M1: Fehler beim Übernehmen – eine fremde Datei, die währenddessen im Ergebnis-Ordner entsteht, bleibt liegen; nur eigene Dateien werden beiseitegelegt
- T-0774 2. Gegenprüfung N1: Rückgängig mit gesperrter KI-Datei wirft nicht – Schritt zurückgenommen, Meldung im Programm
- T-0774 2. Gegenprüfung N2: „pdf.ausfuehren“ ohne Werkzeug nach „tool=passwort“ fällt nicht still weg – deutlicher Fehler
- T-0774 3. Gegenprüfung M-a: Regel-Plan ohne KI mit unverstandenem Teil – Hinweis „Nicht verstanden und ausgelassen“, Ausführung läuft weiter bis zur PDF
- T-0774 3. Gegenprüfung M-b: Auswahlwerte ohne Rücksicht auf Groß-/Kleinschreibung („Komprimieren“, „Zusammenfügen“, „Dokument“) – zugeordnet statt Abbruch
- T-0774 3. Gegenprüfung N-a/N-b: Teilen meldet jedes Teil vor dem Schreiben; scheitert der Lauf, bleibt kein leerer, selbst angelegter „-teile“-Ordner zurück
- T-0774 Dokumente scannen: KI-Aktion „Fotos → PDF“ – Klick → Vorschau → Ja → PDF „Datum Titel.pdf“ mit 2 Seiten, EIN Rückgängig-Schritt, Rückgängig legt die PDF beiseite (KI ersetzt)
- T-0774 Dokumente scannen: der Knopf heißt ehrlich „Hinweis: Papier-Scanner“ (kein Scanner angeschlossen) und gibt weiter den Weg über Fotos an
- T-0774 ECHT: lokale KI (Ollama) – PDF-Werkstatt „Seiten 1–3 heraus“ und Scannen „Fotos → PDF“ im offenen Programm, echte PDFs, Rückgängig (nur mit TRENDO_T0774_ECHT)
- T-0774 Gegenprüfung 1: zwei Fachaktionen („1-2“, dann „4-5“) oder „dokument.setzen“ danach – deutlicher Fehler, keine Datei, Dokument unverändert
- T-0774 Gegenprüfung 2: KI-Plan mit Werkzeug „Passwort“ (entfernen) – abgelehnt, keine entsperrte Kopie
- T-0774 Gegenprüfung 3: Vorschau-Zurückrollen bewegt keine KI-Datei – nach „Ergebnisliste leeren“ und neuer Vorschau mit „Nein“ liegt die PDF noch da
- T-0774 Gegenprüfung 4: KI setzt keinen Ablage-Ordner (absolut/UNC) für Scans – abgelehnt, keine PDF außerhalb
- T-0774 Gegenprüfung 5: Fehler beim Übernehmen (zweite PDF kaputt) – schon geschriebene Dateien werden zurückgenommen, Dokument unverändert
- T-0774 Gegenprüfung 6: Wiederholen bei belegtem Namen trifft keine fremde Datei – Meldung, beiseitegelegte Fassung bleibt
- T-0774 Gegenprüfung 8–10: „3 bis Ende“ → 3-z, Unbekanntes gemeldet; nach „Nein“ keine Meldung stehen; Regel-Planer setzt Modus „foto“ nicht ungefragt
- T-0774 PDF-Werkstatt: falsche Seiten („Seite 9“ bei 5 Seiten) – Fehler schon in der Vorschau, keine Datei, Dokument unverändert (KI ersetzt)
- T-0774 PDF-Werkstatt: „Seiten 1–3 heraus“ – Klick → KI-Plan → Vorschau ohne Datei → Ja → neue PDF mit 3 Seiten, EIN Rückgängig-Schritt, Rückgängig/Wiederholen bringen die Datei mit (KI ersetzt)
