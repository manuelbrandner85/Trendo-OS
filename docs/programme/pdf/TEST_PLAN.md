# Trendo PDF – Testplan

Studio: Texte & Tabellen (SYS-10) · Bereich: Arbeiten & Büro · Seite: `pdf` · Stand: 0.9.36 (Stand 52e27a5)

30 Tests sprechen dieses Programm an.

- Klick-Audit 01.11.2026: „PDF öffnen …“ mit einer Datei, die keine PDF ist – Hinweis statt Absturz
- PDF F3: Texterkennung – ohne Tesseract ehrlicher Hinweis; mit Tesseract wird ein Bild-Scan zur durchsuchbaren PDF und die Seite als Text erkannt
- PDF F3: Unterschrift im Zeichenfeld zeichnen, dann auf die Seite klicken – fest als Bild in einer Kopie „(Trendo)“, Original unverändert
- SYS-02/SYS-10: Dateiregal filtert nach Art, neueste zuerst, ohne Office-Sperrdateien
- SYS-09 U1/F2: Ordner · Liste · Lesebereich; Termin aus der E-Mail erkennen und eintragen
- SYS-10 (T-0244 getrennt): Metadaten aus einem echten LibreOffice-PDF entfernt
- SYS-10 (UI): Tabs; Aufgabe anlegen/erledigen; Tabelle anzeigen; Text zählen; Drucken nur nach Bestätigung
- SYS-10 F1/U1/U2: Start mit Programmkacheln und Vorlagen; Dokumente als Kacheln mit Filter; Öffnen im passenden Programm
- SYS-10-P01 (T-0244 getrennt): LibreOffice öffnet ein von Trendo erzeugtes DOCX
- SYS-10-P02/P11: Neues DOCX/XLSX; Tabelle ohne Office lesen (XLSX + CSV mit ; und Anführungszeichen)
- SYS-10-P04: Zuletzt verwendet – neueste zuerst
- SYS-10-P05/P06: Seitenzahl und A4 eines echten LibreOffice-PDFs
- SYS-10-P07: Text aus einem echten PDF
- SYS-10-P08 (T-0244 getrennt): Scan → durchsuchbares PDF mit Tesseract
- SYS-10-P10: Serienbrief – N Zeilen = N Dateien, Platzhalter auch über Formatgrenzen
- SYS-10-P12/P21: Protokoll als DOCX + Aufgaben als .ics (VTODO mit Fälligkeit und Erinnerung)
- SYS-10-P13: Rechtschreibung mit Hunspell-Wörterbuch (Suffixe, Großschreibung, einfache Komposita)
- SYS-10-P14/P15: Wörter, Sätze, Lesbarkeit; Vergleich markiert Änderungen
- SYS-10-P16: Metadaten aus DOCX und PDF entfernen (PDF bleibt gültig)
- SYS-10-P17 (T-0244 getrennt): PDF zusammenfügen = Summe der Seiten, Teilen stimmt
- SYS-10-P19/P24/P09: Versionen vor dem Überschreiben, Export-Paket mit stimmender SHA-Liste, Vorlagen mit Version
- SYS-10-P20: Suche in Dokumenten mit Fundstelle (DOCX, TXT, PDF)
- SYS-10-P22: Unterschrift als Bild in DOCX (Kopie, mit Hinweis „nicht rechtsverbindlich“)
- SYS-10-P23: PDF-Formular lesen und ausfüllen (Kopie, inkrementell, Original unverändert)
- SYS-10-P25: Barrierefreiheits-Bericht listet Mängel
- T-0222: Ordner des Vollpakets wird bei der Installation gemerkt und beim ersten Start gefunden
- T-0223 P1/P2 UI: PDF öffnen, blättern, Gehe zu, suchen, Lesezeichen – Seite wird gezeichnet
- T-0223 P3–P5 UI: Markieren in einer Kopie (Original bleibt), Seiten auswählen, Passwort setzen und wieder öffnen
- T-0303: Miniatur springt zur Seite; Seiten ordnen: Ziehen verschiebt, Drehen, Löschen, leere Seite, PDF einfügen, Rückgängig, Kopie speichern – Original bleibt
- X-11 F1/F2/U1/U2: Vorhaben mit Ring, Kanban verschieben, Zeitplan mit Meilenstein, Dateien des Vorhabens
