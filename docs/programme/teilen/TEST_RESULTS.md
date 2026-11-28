# Ordner im Heimnetz teilen – Testergebnisse

Studio: Dateien teilen (SYS-06) · Bereich: System & Sicherheit · Seite: `teilen` · Stand: 0.9.113 (Stand 1978f1a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 10 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| LocalSend echt über HTTP: Dateien kommen Byte für Byte an (Token, SHA-256); abgewählte Datei entfällt; Ablehnung, PIN und Prüfsummenfehler verständlich und mit cancel | ✓ bestanden |
| SYS-06 Freigabe-Dialog: Fenster schließen (X) lehnt ab | ✓ bestanden |
| SYS-06 Freigabe-Dialog: Klick auf „Ablehnen“ lehnt ab | ✓ bestanden |
| SYS-06 Freigabe-Dialog: Klick auf „Erlauben“ startet die Freigabe; Schließen des Hauptfensters beendet sie | ✓ bestanden |
| SYS-06 Freigabe-Dialog: nennt Ordner und Dauer, Esc lehnt ab | ✓ bestanden |
| SYS-06 Freigabe-Gate: ohne Zustimmung keine Freigabe, Anfrage abgelehnt | ✓ bestanden |
| SYS-06 UI: Ordner teilen, Datei echt per HTTP laden, falsches Token abgelehnt, beenden | ✓ bestanden |
| SYS-06: Freigabe nur mit Token, nur im Ordner, keine versteckten Dateien, läuft ab | ✓ bestanden |
| SYS-06: Nur private Heimnetz-Adressen | ✓ bestanden |
| Y4/Y7/Y11/Y14: Gefahren prüfen, Teilen, Qualität und Geräteverbund als Werkzeug-Karten mit Symbol; Geräteverbund ohne Geräte mit Leerzustand | ✓ bestanden |
