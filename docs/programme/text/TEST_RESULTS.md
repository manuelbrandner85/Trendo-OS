# Trendo Text – Testergebnisse

Studio: Texte & Tabellen (SYS-10) · Bereich: Arbeiten & Büro · Seite: `text` · Stand: 0.9.36 (Stand bee36a7)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 45 · Fehlgeschlagen 0 · Übersprungen 2 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| 108-49: KI-Vorschlag mit Änderungen – erst „Übernehmen“ ersetzt, „Verwerfen“ lässt alles, geänderter Text wird nicht überschrieben | ✓ bestanden |
| Claude live: Webseite erscheint in der Vorschau, beim Ändern vorher/nachher – Behalten oder Zurücknehmen; Lieferformat index.html | ✓ bestanden |
| Claude live: neuer Text öffnet gleich in Trendo Text – aber nie über einen ungespeicherten Text | ✓ bestanden |
| Claude live: offenes Studio-Dokument (.tdoc) – Claude bearbeitet es, das Studio übernimmt die Änderung sofort, Rückgängig im Studio | ✓ bestanden |
| Claude zeigt, wo es arbeitet; Felder in Aufklapp-Menüs kann Claude lesen und ausfüllen | ✓ bestanden |
| Genauere Leser (Kalender, Programm-Dokument), Design „Wie Windows“, Hilfe „?“ auf Seiten ohne eigenen Hilfetext, Hinweis statt stummer Knöpfe | ✓ bestanden |
| Grundriss live: Claude setzt die Wände einer 2-Raum-Wohnung auf einmal – sie erscheinen nacheinander im Programm, Rückgängig ist ein Schritt | ✓ bestanden |
| Live ohne Schreibwerkzeug (z. B. per Befehl): Trendo sieht die Änderung im Projektordner und zeigt sie noch während Claude arbeitet – auch in Trendo Text | ✓ bestanden |
| Live-Wiedergabe: neue Elemente auch in verschachtelten Listen, je Zwischenstand eins mehr; geänderte Werte gelten sofort; nichts Neues → keine Zwischenstände | ✓ bestanden |
| Pilot Text Leistung: 300 formatierte Absätze (ca. 15 Seiten) – Seite aufbauen unter 3 s, Formatwechsel unter 1,5 s | ✓ bestanden |
| Pilot Text UI: Rechtsklick im Text zeigt Ausschneiden/Kopieren/Einfügen, Formate, Farbe, Absatzart, Link, Kommentar und „✳ Claude fragen“ | ✓ bestanden |
| Pilot Text UI: Schriftart, Größe, Farbe und Hervorhebung über das Menüband, Zeilenabstand und Erstzeileneinzug, alles rückgängig machbar | ✓ bestanden |
| SYS-02/SYS-10: Dateiregal filtert nach Art, neueste zuerst, ohne Office-Sperrdateien | ✓ bestanden |
| SYS-09 U1/F2: Ordner · Liste · Lesebereich; Termin aus der E-Mail erkennen und eintragen | ✓ bestanden |
| SYS-10 (T-0244 getrennt): Metadaten aus einem echten LibreOffice-PDF entfernt | übersprungen |
| SYS-10 (UI): Tabs; Aufgabe anlegen/erledigen; Tabelle anzeigen; Text zählen; Drucken nur nach Bestätigung | ✓ bestanden |
| SYS-10 F1/U1/U2: Start mit Programmkacheln und Vorlagen; Dokumente als Kacheln mit Filter; Öffnen im passenden Programm | ✓ bestanden |
| SYS-10-P01 (T-0244 getrennt): LibreOffice öffnet ein von Trendo erzeugtes DOCX | ✓ bestanden |
| SYS-10-P02/P11: Neues DOCX/XLSX; Tabelle ohne Office lesen (XLSX + CSV mit ; und Anführungszeichen) | ✓ bestanden |
| SYS-10-P04: Zuletzt verwendet – neueste zuerst | ✓ bestanden |
| SYS-10-P05/P06: Seitenzahl und A4 eines echten LibreOffice-PDFs | ✓ bestanden |
| SYS-10-P07: Text aus einem echten PDF | ✓ bestanden |
| SYS-10-P08 (T-0244 getrennt): Scan → durchsuchbares PDF mit Tesseract | übersprungen |
| SYS-10-P10: Serienbrief – N Zeilen = N Dateien, Platzhalter auch über Formatgrenzen | ✓ bestanden |
| SYS-10-P12/P21: Protokoll als DOCX + Aufgaben als .ics (VTODO mit Fälligkeit und Erinnerung) | ✓ bestanden |
| SYS-10-P13: Rechtschreibung mit Hunspell-Wörterbuch (Suffixe, Großschreibung, einfache Komposita) | ✓ bestanden |
| SYS-10-P14/P15: Wörter, Sätze, Lesbarkeit; Vergleich markiert Änderungen | ✓ bestanden |
| SYS-10-P16: Metadaten aus DOCX und PDF entfernen (PDF bleibt gültig) | ✓ bestanden |
| SYS-10-P17 (T-0244 getrennt): PDF zusammenfügen = Summe der Seiten, Teilen stimmt | ✓ bestanden |
| SYS-10-P19/P24/P09: Versionen vor dem Überschreiben, Export-Paket mit stimmender SHA-Liste, Vorlagen mit Version | ✓ bestanden |
| SYS-10-P20: Suche in Dokumenten mit Fundstelle (DOCX, TXT, PDF) | ✓ bestanden |
| SYS-10-P22: Unterschrift als Bild in DOCX (Kopie, mit Hinweis „nicht rechtsverbindlich“) | ✓ bestanden |
| SYS-10-P23: PDF-Formular lesen und ausfüllen (Kopie, inkrementell, Original unverändert) | ✓ bestanden |
| SYS-10-P25: Barrierefreiheits-Bericht listet Mängel | ✓ bestanden |
| Schreibhilfe UI: Ja korrigiert (Strg+Z-fähig), Nein bleibt gemerkt, langer Satz wird markiert | ✓ bestanden |
| Schreibhilfe-Regeln: doppeltes Wort, Leerzeichen, Satzzeichen, Anführungszeichen, Schreibweisen, Füllwort, langer Satz; Anwenden prüft die Stelle | ✓ bestanden |
| T-0223 T1–T4 UI: tippen, Enter teilt, Rücktaste verbindet, Fett per Knopf und Strg+B, Absatzart, Liste, Ausrichtung, Rückgängig | ✓ bestanden |
| T-0223 T5/T6/T7/T9/T10 UI: Tabelle und Seitenumbruch einfügen, Suchen & Ersetzen, Speichern und Öffnen, Als PDF, Vorlage | ✓ bestanden |
| T-0223 T8 UI: Rechtschreibung ohne Wörterbuch sagt, woher es kommt; Seite gehört zu „Texte & Tabellen“ | ✓ bestanden |
| T-0230 W1/W2/W5: Formatvorlagen-Galerie, Format übertragen, Link/Kommentar/Fußnote über das Eingabefeld, Verzeichnis, Navigation | ✓ bestanden |
| T-0230 W3/W4: Design, Wasserzeichen, Ränder, Querformat, Spalten, Einzug, Seitenzahlen – im Blatt sichtbar und im Dokument gespeichert | ✓ bestanden |
| T-0230 W6/W7: Serienbrief aus Tabelle mit Feldern und Etiketten; Vergleichen, annehmen; Barrierefreiheit und Kommentare in der Seitenleiste | ✓ bestanden |
| T-0254: Text und Tabelle – ungespeichert gesichert, nach Neustart „Wiederherstellen“; „Verwerfen“ löscht; Speichern räumt die Sicherung weg | ✓ bestanden |
| Text F3: 1.-Liste, Enter und Tab rückt ein (a), Tab noch einmal (i.), Umschalt+Tab zurück; ⇤/⇥-Knöpfe; Rückgängig; Tab ohne Liste ändert nichts | ✓ bestanden |
| Text F3: „Formatierung löschen“ – erst nur das Markierte, ohne Markierung der ganze Absatz (Absatzart, Ausrichtung zurück) | ✓ bestanden |
| X-11 F1/F2/U1/U2: Vorhaben mit Ring, Kanban verschieben, Zeitplan mit Meilenstein, Dateien des Vorhabens | ✓ bestanden |
| Zeitleiste mit Dauer und Tönen: fertig, Rückfrage, abschaltbar | ✓ bestanden |
