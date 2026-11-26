# Ordner im Heimnetz teilen – Testplan

Studio: Dateien teilen (SYS-06) · Bereich: System & Sicherheit · Seite: `teilen` · Stand: 0.9.110 (Stand 0eb9d52)

10 Tests sprechen dieses Programm an.

- LocalSend echt über HTTP: Dateien kommen Byte für Byte an (Token, SHA-256); abgewählte Datei entfällt; Ablehnung, PIN und Prüfsummenfehler verständlich und mit cancel
- SYS-06 Freigabe-Dialog: Fenster schließen (X) lehnt ab
- SYS-06 Freigabe-Dialog: Klick auf „Ablehnen“ lehnt ab
- SYS-06 Freigabe-Dialog: Klick auf „Erlauben“ startet die Freigabe; Schließen des Hauptfensters beendet sie
- SYS-06 Freigabe-Dialog: nennt Ordner und Dauer, Esc lehnt ab
- SYS-06 Freigabe-Gate: ohne Zustimmung keine Freigabe, Anfrage abgelehnt
- SYS-06 UI: Ordner teilen, Datei echt per HTTP laden, falsches Token abgelehnt, beenden
- SYS-06: Freigabe nur mit Token, nur im Ordner, keine versteckten Dateien, läuft ab
- SYS-06: Nur private Heimnetz-Adressen
- Y4/Y7/Y11/Y14: Gefahren prüfen, Teilen, Qualität und Geräteverbund als Werkzeug-Karten mit Symbol; Geräteverbund ohne Geräte mit Leerzustand
