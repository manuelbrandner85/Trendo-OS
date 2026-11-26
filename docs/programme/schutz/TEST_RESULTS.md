# Schutz & Datenschutz – Testergebnisse

Studio: Daten schützen (CORE-11) · Bereich: System & Sicherheit · Seite: `schutz` · Stand: 0.9.112 (Stand d6f9fcd)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 11 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen | ✓ bestanden |
| CORE-11: Ordnerprüfung findet Passwort, Schlüssel, gültige IBAN, Kartennummer – maskiert | ✓ bestanden |
| CORE-11: Schutz-Prüfung erkennt fehlende Sicherung und verändertes Protokoll | ✓ bestanden |
| CORE-11: Sicherung enthält keine fremden Ordner – nur der Trendo-Datenordner, eine kaputte ZIP lässt die Daten unverändert | ✓ bestanden |
| CORE-11: Sicherung fällig – ohne Sicherung sofort, sonst erst nach dem Abstand; 0 Stunden heißt nie automatisch | ✓ bestanden |
| CORE-11: Sicherung – höchstens „keep“ Sicherungen, Vorschau zählt ersetzte und neue Dateien, Wiederherstellen holt den alten Stand zurück | ✓ bestanden |
| T-0754 Gegenprüfung 4: Studienwissen CORE-11 verspricht keine Passwortverwaltung | ✓ bestanden |
| T-0754 Gegenprüfung UI: Laufwerk ohne Papierkorb – Entf fragt „endgültig löschen?“, Abbrechen lässt alles stehen, Ja löscht und sagt „endgültig gelöscht“; Schutzseite nennt sich nicht „gut geschützt“ | ✓ bestanden |
| T-0754 UI: Sicherung zeigt deutlich „nicht verschlüsselt“, Ordner und „Ordner öffnen“; Meldung nach dem Sichern sagt es; Ampel gelb; Studio verspricht keine Passwortverwaltung | ✓ bestanden |
| Y3/Y6: Sicherheitszentrale mit Schutz-Ring und Ampel-Karten je Prüfung; Programme verwalten mit App-Regal – Klick sucht, installiert nichts ohne „Installieren“ | ✓ bestanden |
| restic-Karte: ohne Einrichtung Einrichten-Knopf; sichern legt Ablage an, Passwort nur in der Umgebung; Stände anzeigen; Wiederherstellen in neuen Ordner | ✓ bestanden |
