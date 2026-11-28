# Kalender – Testergebnisse

Studio: E-Mails & Termine (SYS-09) · Bereich: Arbeiten & Büro · Seite: `kalender` · Stand: 0.9.114 (Stand 99cbb5c)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 12 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Genauere Leser (Kalender, Programm-Dokument), Design „Wie Windows“, Hilfe „?“ auf Seiten ohne eigenen Hilfetext, Hinweis statt stummer Knöpfe | ✓ bestanden |
| Kalender-Oberfläche: fremde .ics mit „6× jeden Montag“ → 6 Termine und Hinweis; kaputte Datei → Meldung statt Absturz | ✓ bestanden |
| Kalender: in der Woche ziehen legt Termin an, benennen, verschieben auf anderen Tag, Unterkante ziehen verlängert, Rückgängig; Monat: Termin auf anderen Tag ziehen | ✓ bestanden |
| O3/O5: Termin eintragen (wöchentlich), Monats-/Wochen-/Tagesansicht, Erinnerung als Benachrichtigung; Aufgabe anlegen und abhaken | ✓ bestanden |
| SYS-09 F2: Datum, Monatsname, Wochentag und morgen erkennen – mit Uhrzeit | ✓ bestanden |
| SYS-09 U1/F2: Ordner · Liste · Lesebereich; Termin aus der E-Mail erkennen und eintragen | ✓ bestanden |
| SYS-09 UI: Kontakt anlegen, Entwurf prüfen und im Mailprogramm öffnen (nichts wird gesendet) | ✓ bestanden |
| SYS-09: Eingegangene E-Mail (multipart, quoted-printable) wird lesbar | ✓ bestanden |
| SYS-09: Entwurf als .eml (Umlaute korrekt, X-Unsent) und mailto; Prüfung findet Fehler | ✓ bestanden |
| SYS-09: Kontakte speichern, vCard ein- und ausgeben, ungültige Adresse abgelehnt | ✓ bestanden |
| T-0717 Kalender: Eintragen ist rückgängig zu machen, Rückgängig/Wiederholen der Hülle über mehrere Schritte, gespeichert nach dem Schließen | ✓ bestanden |
| Vorschläge UI: Kalender plant fällige Aufgabe ein (Ja, Rückgängig), Aufgaben aus E-Mails (Nein bleibt gemerkt), Abläufe, Videoschnitt-Stille | ✓ bestanden |
