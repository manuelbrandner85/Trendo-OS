# Programme verwalten – Testplan

Studio: Programme installieren (SYS-04) · Bereich: System & Sicherheit · Seite: `appcenter` · Stand: 0.9.28 (Stand 83bf3c3)

15 Tests sprechen dieses Programm an.

- CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen
- SPEC-12 UI: Prüfsumme vergleichen, Fall anlegen, Beweis sichern, Bericht
- SYS-04 UI: Suchen, installieren, Updates anzeigen und aktualisieren, entfernen
- SYS-04: Installieren nur nach Freigabe, als Benutzer, gefährliche IDs abgelehnt
- SYS-04: winget- und flatpak-Ausgaben werden korrekt gelesen
- SYS-06 Freigabe-Dialog: Fenster schließen (X) lehnt ab
- SYS-06 Freigabe-Dialog: Klick auf „Ablehnen“ lehnt ab
- SYS-06 Freigabe-Dialog: Klick auf „Erlauben“ startet die Freigabe; Schließen des Hauptfensters beendet sie
- SYS-06 Freigabe-Dialog: nennt Ordner und Dauer, Esc lehnt ab
- SYS-06 Freigabe-Gate: ohne Zustimmung keine Freigabe, Anfrage abgelehnt
- SYS-06 UI: Ordner teilen, Datei echt per HTTP laden, falsches Token abgelehnt, beenden
- SYS-07 UI: Geräte suchen zeigt Gruppen
- SYS-08 UI: Seite im Lesemodus lesen, .onion wird nicht abgerufen, Sitzung beenden
- SYS-09 UI: Kontakt anlegen, Entwurf prüfen und im Mailprogramm öffnen (nichts wird gesendet)
- X-02 UI: Beim Start automatisch geprüft, Seite zeigt Ergebnis, erneut prüfen
