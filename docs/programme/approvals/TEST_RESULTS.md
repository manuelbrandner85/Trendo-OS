# Freigaben – Testergebnisse

Studio: Daten schützen (CORE-11) · Bereich: System & Sicherheit · Seite: `approvals` · Stand: 0.9.36 (Stand 5b38700)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 13 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen | ✓ bestanden |
| CORE-11: Ordnerprüfung findet Passwort, Schlüssel, gültige IBAN, Kartennummer – maskiert | ✓ bestanden |
| CORE-11: Schutz-Prüfung erkennt fehlende Sicherung und verändertes Protokoll | ✓ bestanden |
| CORE-11: Sicherung enthält keine fremden Ordner – nur der Trendo-Datenordner, eine kaputte ZIP lässt die Daten unverändert | ✓ bestanden |
| CORE-11: Sicherung fällig – ohne Sicherung sofort, sonst erst nach dem Abstand; 0 Stunden heißt nie automatisch | ✓ bestanden |
| CORE-11: Sicherung – höchstens „keep“ Sicherungen, Vorschau zählt ersetzte und neue Dateien, Wiederherstellen holt den alten Stand zurück | ✓ bestanden |
| SPEC-15 UI: FFmpeg wird erst nach Zustimmung installiert | ✓ bestanden |
| SPEC-15 UI: Fenster, Kamera und Mikrofon lassen sich wählen | ✓ bestanden |
| SPEC-15 UI: Live startet erst nach Freigabe, Schlüssel bleibt aus Dateien und Protokoll | ✓ bestanden |
| SPEC-15 UI: Mikrofone suchen setzt das Häkchen nicht | ✓ bestanden |
| SPEC-15 UI: Vorschau, Aufnahme und Stopp tun etwas, ohne von allein zu senden | ✓ bestanden |
| SPEC-15 UI: offline kein Stream, Aufnahme bleibt | ✓ bestanden |
| T-0005 (UI): Not-Stopp-Knopf hält an, Freigabe gesperrt mit Hinweis; Fortsetzen; Pause-Taste stoppt ebenfalls | ✓ bestanden |
