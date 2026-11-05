# Schutz & Datenschutz – Testergebnisse

Studio: Daten schützen (CORE-11) · Bereich: System & Sicherheit · Seite: `schutz` · Stand: 0.9.36 (Stand 208ed9e)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 18 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen | ✓ bestanden |
| CORE-11: Ordnerprüfung findet Passwort, Schlüssel, gültige IBAN, Kartennummer – maskiert | ✓ bestanden |
| CORE-11: Schutz-Prüfung erkennt fehlende Sicherung und verändertes Protokoll | ✓ bestanden |
| CORE-11: Sicherung enthält keine fremden Ordner – nur der Trendo-Datenordner, eine kaputte ZIP lässt die Daten unverändert | ✓ bestanden |
| CORE-11: Sicherung fällig – ohne Sicherung sofort, sonst erst nach dem Abstand; 0 Stunden heißt nie automatisch | ✓ bestanden |
| CORE-11: Sicherung – höchstens „keep“ Sicherungen, Vorschau zählt ersetzte und neue Dateien, Wiederherstellen holt den alten Stand zurück | ✓ bestanden |
| SPEC-12 UI: Prüfsumme vergleichen, Fall anlegen, Beweis sichern, Bericht | ✓ bestanden |
| SYS-04 UI: Suchen, installieren, Updates anzeigen und aktualisieren, entfernen | ✓ bestanden |
| SYS-06 Freigabe-Dialog: Fenster schließen (X) lehnt ab | ✓ bestanden |
| SYS-06 Freigabe-Dialog: Klick auf „Ablehnen“ lehnt ab | ✓ bestanden |
| SYS-06 Freigabe-Dialog: Klick auf „Erlauben“ startet die Freigabe; Schließen des Hauptfensters beendet sie | ✓ bestanden |
| SYS-06 Freigabe-Dialog: nennt Ordner und Dauer, Esc lehnt ab | ✓ bestanden |
| SYS-06 Freigabe-Gate: ohne Zustimmung keine Freigabe, Anfrage abgelehnt | ✓ bestanden |
| SYS-06 UI: Ordner teilen, Datei echt per HTTP laden, falsches Token abgelehnt, beenden | ✓ bestanden |
| SYS-07 UI: Geräte suchen zeigt Gruppen | ✓ bestanden |
| SYS-08 UI: Seite im Lesemodus lesen, .onion wird nicht abgerufen, Sitzung beenden | ✓ bestanden |
| SYS-09 UI: Kontakt anlegen, Entwurf prüfen und im Mailprogramm öffnen (nichts wird gesendet) | ✓ bestanden |
| X-02 UI: Beim Start automatisch geprüft, Seite zeigt Ergebnis, erneut prüfen | ✓ bestanden |
