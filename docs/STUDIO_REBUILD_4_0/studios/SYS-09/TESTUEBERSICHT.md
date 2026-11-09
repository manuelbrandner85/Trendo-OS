# Testübersicht: E-Mails & Termine

Studio SYS-09 · E-Mails & Termine · Bereich Arbeiten & Büro

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

Gezählte Tests des Studios: 82. Gefordert für die Abnahme 4.0: mindestens 20 eigene positive und negative Testfälle.

## Dem Studio zugeordnete Tests

Die Zuordnung kommt aus dem Programmkatalog und ist ungenau: Sie kann Tests anderer Studios enthalten (siehe REA-Audit).

- Adressbuch wie Google Kontakte/Outlook: CSV einlesen und sichern, Nummern vereinheitlichen, Etiketten der gewählten Gruppe als PDF
- Aufgaben-Kern: Spalte setzen, erledigt beendet „in Arbeit“, ICS behält In-Arbeit und Beschreibung
- Aufgaben-Tafel: anlegen, Karte nach „In Arbeit“ und „Erledigt“ ziehen, Klick öffnet Bearbeiten (Beschreibung, Liste), Listenfilter, Häkchen, Rückgängig
- CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen
- Claude zeigt, wo es arbeitet; Felder in Aufklapp-Menüs kann Claude lesen und ausfüllen
- Genauere Leser (Kalender, Programm-Dokument), Design „Wie Windows“, Hilfe „?“ auf Seiten ohne eigenen Hilfetext, Hinweis statt stummer Knöpfe
- Kalender-Kern: Raster 15 Minuten, Verschieben behält die Dauer, Ende nie vor Beginn + 15 min
- Kalender-Oberfläche: fremde .ics mit „6× jeden Montag“ → 6 Termine und Hinweis; kaputte Datei → Meldung statt Absturz
- Kalender: in der Woche ziehen legt Termin an, benennen, verschieben auf anderen Tag, Unterkante ziehen verlängert, Rückgängig; Monat: Termin auf anderen Tag ziehen
- Mailprogramm: Anhang ansehen und speichern, Allen antworten (Kopie ohne eigene Adresse), Entwurf speichern/öffnen, Senden → Gesendet, Unterhaltungen, Tasten N/R
- O1/O2/O6: Konto (Passwort geschützt), Posteingang nur lesend, Nachricht öffnen, Antworten nur nach Bestätigung senden, Regel erst nach Freigabe
- O3/O5: Termin eintragen (wöchentlich), Monats-/Wochen-/Tagesansicht, Erinnerung als Benachrichtigung; Aufgabe anlegen und abhaken
- Posteingang: Suche nach Absender/Betreff, Ordner „Gemerkt“ zeigt nur gemerkte Nachrichten
- SPEC-12 UI: Prüfsumme vergleichen, Fall anlegen, Beweis sichern, Bericht
- SYS-04 UI: Suchen, installieren, Updates anzeigen und aktualisieren, entfernen
- SYS-06 Freigabe-Dialog: Fenster schließen (X) lehnt ab
- SYS-06 Freigabe-Dialog: Klick auf „Ablehnen“ lehnt ab
- SYS-06 Freigabe-Dialog: Klick auf „Erlauben“ startet die Freigabe; Schließen des Hauptfensters beendet sie
- SYS-06 Freigabe-Dialog: nennt Ordner und Dauer, Esc lehnt ab
- SYS-06 Freigabe-Gate: ohne Zustimmung keine Freigabe, Anfrage abgelehnt
- SYS-06 UI: Ordner teilen, Datei echt per HTTP laden, falsches Token abgelehnt, beenden
- SYS-07 UI: Geräte suchen zeigt Gruppen
- SYS-08 UI: Seite im Lesemodus lesen, .onion wird nicht abgerufen, Sitzung beenden
- SYS-09 F2: Datum, Monatsname, Wochentag und morgen erkennen – mit Uhrzeit
- SYS-09 U1/F2: Ordner · Liste · Lesebereich; Termin aus der E-Mail erkennen und eintragen
- SYS-09 UI: Kontakt anlegen, Entwurf prüfen und im Mailprogramm öffnen (nichts wird gesendet)
- SYS-09: Eingegangene E-Mail (multipart, quoted-printable) wird lesbar
- SYS-09: Entwurf als .eml (Umlaute korrekt, X-Unsent) und mailto; Prüfung findet Fehler
- SYS-09: Kontakte speichern, vCard ein- und ausgeben, ungültige Adresse abgelehnt
- SYS-10 F1/U1/U2: Start mit Programmkacheln und Vorlagen; Dokumente als Kacheln mit Filter; Öffnen im passenden Programm
- SYS-10 wie Microsoft 365: Dokumente suchen (alle Wörter im Namen), Anheften mit ★ zeigt sie oben unter „Angeheftet“, Lösen nimmt sie weg; gelöschte Datei verschwindet
- T-0307: Adressbuch – Neuer Kontakt bearbeiten/speichern, Karte, Gruppe, Favorit, Suche, Doppelte Nein/Ja, Geburtstage in den Kalender, E-Mail schreiben, Löschen Nein/Ja
- Vorschläge UI: Kalender plant fällige Aufgabe ein (Ja, Rückgängig), Aufgaben aus E-Mails (Nein bleibt gemerkt), Abläufe, Videoschnitt-Stille
- X-02 UI: Beim Start automatisch geprüft, Seite zeigt Ergebnis, erneut prüfen
- X-11 F1/F2/U1/U2: Vorhaben mit Ring, Kanban verschieben, Zeitplan mit Meilenstein, Dateien des Vorhabens
- Zeitleiste mit Dauer und Tönen: fertig, Rückfrage, abschaltbar

## Pflichtprüfungen der Abnahme 4.0

Alle Punkte offen, bis ein Testartefakt vorliegt: alte Funktionen, neue Funktionen, Bedienbarkeit, Erreichbarkeit, Dateikompatibilität, Fehlerbehandlung, Rechte, Leistung, Speicher, Stabilität, Tastatur und Maus, Ziehen und Ablegen, Rückgängig, Wiederherstellung, Exporte, Zusammenspiel mit anderen Studios.
