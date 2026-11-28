# PDF-Werkstatt – Testergebnisse

Studio: PDF-Werkstatt (STUDIO-PDFTOOLS-01) · Bereich: Arbeiten & Büro · Seite: `pdf-werkstatt-programm` · Stand: 0.9.114 (Stand 99cbb5c)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 33 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Großprojekt Ende-zu-Ende: Karte → Bücher schreiben → Druck-PDF zurück, nächste Karte → PDF-Werkstatt mit dieser PDF → Seitenzahlen-PDF zurück, mit Vorschau und Prüfsumme | ✓ bestanden |
| PDF-Werkstatt: eigene Seite im Studio, Werkzeugleiste mit allen Werkzeugen, Vorlage wählt das Werkzeug | ✓ bestanden |
| PW-01 UI: zwei PDFs hinzufügen, Reihenfolge tauschen, „Ausführen“ fügt zusammen; Ergebnis öffnet in Trendo PDF; Originale unverändert | ✓ bestanden |
| PW-01/03 UI: Teilen nach Bereichen und Bilder zu PDF über die Werkstatt | ✓ bestanden |
| PW-02 UI: Seiten ordnen – Seite anklicken, drehen, Seite löschen, nach vorn; Ergebnis hat die neue Folge | ✓ bestanden |
| PW-04 UI: Komprimieren mit JPEG (SkiaSharp) – gemessen kleiner, Bild als JPEG gespeichert, Meldung nennt vorher/nachher | ✓ bestanden |
| PW-05/06/07/08 UI: Stapel – Wasserzeichen und Seitenzahlen für jede Datei, Metadaten entfernen, Passwort setzen und mit Passwort entfernen | ✓ bestanden |
| PW-09 UI: Schwärzen per Maus – Balken ziehen, Ausführen; der Text unter dem Balken ist in der Ergebnisdatei nicht mehr lesbar | ✓ bestanden |
| PW-10 UI: Zwei PDFs vergleichen – Unterschiede mit Seitenangabe in der Arbeitsfläche und als Textdatei | ✓ bestanden |
| STUDIO-PDFTOOLS-01 Adapter: Arbeitsauftrag mit aufgelösten Seiten, Pflichtfeld Aktion, falsche Seitenangabe wird gemeldet | ✓ bestanden |
| T-0705 Absturz in allen sieben Büro-Studios: Änderung per Klick, Sicherungstakt (≤ 20 s), Fenster stirbt ohne Speichern – das neue Fenster bietet „Wiederherstellen“ an und bringt jede Änderung zurück | ✓ bestanden |
| T-0705 Echtlauf PDF-Werkstatt: LibreOffice-PDF (3 Seiten) ablegen, Seite 3 nach vorn ziehen, drehen, Seite löschen, Ausführen – qpdf prüft Struktur, Seitenzahl und Drehung, LibreOffice öffnet das Ergebnis | ✓ bestanden |
| T-0705 Klick bis Datei – PDF-Werkstatt: leere Liste, „Dateien hinzufügen …“, Ausführen fügt zusammen; Ergebnis hat alle Seiten in Reihenfolge | ✓ bestanden |
| T-0705 PDF-Werkstatt: PDFs aus dem Explorer auf den ersten Schritt ziehen, Seite 4 mit der Maus an den Anfang ziehen, Rechtsklick „Nach rechts drehen“ und „Seite löschen“; Ausführen schreibt genau diese Reihenfolge | ✓ bestanden |
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
