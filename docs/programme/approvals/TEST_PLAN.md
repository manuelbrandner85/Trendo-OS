# Freigaben – Testplan

Studio: Daten schützen (CORE-11) · Bereich: System & Sicherheit · Seite: `approvals` · Stand: 0.9.36 (Stand 927ab35)

10 Tests sprechen dieses Programm an.

- CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen
- CORE-11: Ordnerprüfung findet Passwort, Schlüssel, gültige IBAN, Kartennummer – maskiert
- CORE-11: Schutz-Prüfung erkennt fehlende Sicherung und verändertes Protokoll
- SPEC-15 UI: FFmpeg wird erst nach Zustimmung installiert
- SPEC-15 UI: Fenster, Kamera und Mikrofon lassen sich wählen
- SPEC-15 UI: Live startet erst nach Freigabe, Schlüssel bleibt aus Dateien und Protokoll
- SPEC-15 UI: Mikrofone suchen setzt das Häkchen nicht
- SPEC-15 UI: Vorschau, Aufnahme und Stopp tun etwas, ohne von allein zu senden
- SPEC-15 UI: offline kein Stream, Aufnahme bleibt
- T-0005 (UI): Not-Stopp-Knopf hält an, Freigabe gesperrt mit Hinweis; Fortsetzen; Pause-Taste stoppt ebenfalls
