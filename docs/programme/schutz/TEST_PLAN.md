# Schutz & Datenschutz – Testplan

Studio: Daten schützen (CORE-11) · Bereich: System & Sicherheit · Seite: `schutz` · Stand: 0.9.112 (Stand d6f9fcd)

11 Tests sprechen dieses Programm an.

- CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen
- CORE-11: Ordnerprüfung findet Passwort, Schlüssel, gültige IBAN, Kartennummer – maskiert
- CORE-11: Schutz-Prüfung erkennt fehlende Sicherung und verändertes Protokoll
- CORE-11: Sicherung enthält keine fremden Ordner – nur der Trendo-Datenordner, eine kaputte ZIP lässt die Daten unverändert
- CORE-11: Sicherung fällig – ohne Sicherung sofort, sonst erst nach dem Abstand; 0 Stunden heißt nie automatisch
- CORE-11: Sicherung – höchstens „keep“ Sicherungen, Vorschau zählt ersetzte und neue Dateien, Wiederherstellen holt den alten Stand zurück
- T-0754 Gegenprüfung 4: Studienwissen CORE-11 verspricht keine Passwortverwaltung
- T-0754 Gegenprüfung UI: Laufwerk ohne Papierkorb – Entf fragt „endgültig löschen?“, Abbrechen lässt alles stehen, Ja löscht und sagt „endgültig gelöscht“; Schutzseite nennt sich nicht „gut geschützt“
- T-0754 UI: Sicherung zeigt deutlich „nicht verschlüsselt“, Ordner und „Ordner öffnen“; Meldung nach dem Sichern sagt es; Ampel gelb; Studio verspricht keine Passwortverwaltung
- Y3/Y6: Sicherheitszentrale mit Schutz-Ring und Ampel-Karten je Prüfung; Programme verwalten mit App-Regal – Klick sucht, installiert nichts ohne „Installieren“
- restic-Karte: ohne Einrichtung Einrichten-Knopf; sichern legt Ablage an, Passwort nur in der Umgebung; Stände anzeigen; Wiederherstellen in neuen Ordner
