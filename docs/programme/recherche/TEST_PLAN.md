# Recherche im Lesemodus – Testplan

Studio: Sicher recherchieren (SYS-08) · Bereich: Wissen & Lernen · Seite: `recherche` · Stand: 0.9.36 (Stand 2963379)

16 Tests sprechen dieses Programm an.

- CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen
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
- SYS-08: .onion nur Tor Browser, andere Schemata und Heimnetz abgelehnt, Webseiten im Lesemodus
- SYS-08: Lesetext ohne Skripte und Formulare, Links absolut; Sitzung flüchtig
- SYS-09 UI: Kontakt anlegen, Entwurf prüfen und im Mailprogramm öffnen (nichts wird gesendet)
- T-0309: Recherche – Auswahl markieren (farbig im Text), Notiz, Farbe, Zitat mit Quelle kopieren, Literaturliste einfach/APA, Bericht mit Zitaten, Sitzungsende löscht
- X-02 UI: Beim Start automatisch geprüft, Seite zeigt Ergebnis, erneut prüfen
