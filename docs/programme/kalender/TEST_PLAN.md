# Kalender – Testplan

Studio: E-Mails & Termine (SYS-09) · Bereich: Arbeiten & Büro · Seite: `kalender` · Stand: 0.9.36 (Stand 66211cf)

14 Tests sprechen dieses Programm an.

- Claude zeigt, wo es arbeitet; Felder in Aufklapp-Menüs kann Claude lesen und ausfüllen
- Genauere Leser (Kalender, Programm-Dokument), Design „Wie Windows“, Hilfe „?“ auf Seiten ohne eigenen Hilfetext, Hinweis statt stummer Knöpfe
- Kalender-Kern: Raster 15 Minuten, Verschieben behält die Dauer, Ende nie vor Beginn + 15 min
- Kalender: in der Woche ziehen legt Termin an, benennen, verschieben auf anderen Tag, Unterkante ziehen verlängert, Rückgängig; Monat: Termin auf anderen Tag ziehen
- O1/O2/O6: Konto (Passwort geschützt), Posteingang nur lesend, Nachricht öffnen, Antworten nur nach Bestätigung senden, Regel erst nach Freigabe
- O3/O5: Termin eintragen (wöchentlich), Monats-/Wochen-/Tagesansicht, Erinnerung als Benachrichtigung; Aufgabe anlegen und abhaken
- SYS-09 F2: Datum, Monatsname, Wochentag und morgen erkennen – mit Uhrzeit
- SYS-09 U1/F2: Ordner · Liste · Lesebereich; Termin aus der E-Mail erkennen und eintragen
- SYS-09 UI: Kontakt anlegen, Entwurf prüfen und im Mailprogramm öffnen (nichts wird gesendet)
- SYS-09: Eingegangene E-Mail (multipart, quoted-printable) wird lesbar
- SYS-09: Entwurf als .eml (Umlaute korrekt, X-Unsent) und mailto; Prüfung findet Fehler
- SYS-09: Kontakte speichern, vCard ein- und ausgeben, ungültige Adresse abgelehnt
- Vorschläge UI: Kalender plant fällige Aufgabe ein (Ja, Rückgängig), Aufgaben aus E-Mails (Nein bleibt gemerkt), Abläufe, Videoschnitt-Stille
- Zeitleiste mit Dauer und Tönen: fertig, Rückfrage, abschaltbar
