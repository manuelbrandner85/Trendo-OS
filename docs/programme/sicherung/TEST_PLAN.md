# Sicherung – Testplan

Studio: Daten schützen (CORE-11) · Bereich: System & Sicherheit · Seite: `sicherung` · Stand: 0.9.36 (Stand 70e872a)

8 Tests sprechen dieses Programm an.

- CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen
- CORE-11: Ordnerprüfung findet Passwort, Schlüssel, gültige IBAN, Kartennummer – maskiert
- CORE-11: Schutz-Prüfung erkennt fehlende Sicherung und verändertes Protokoll
- CORE-11: Sicherung enthält keine fremden Ordner – nur der Trendo-Datenordner, eine kaputte ZIP lässt die Daten unverändert
- CORE-11: Sicherung fällig – ohne Sicherung sofort, sonst erst nach dem Abstand; 0 Stunden heißt nie automatisch
- CORE-11: Sicherung – höchstens „keep“ Sicherungen, Vorschau zählt ersetzte und neue Dateien, Wiederherstellen holt den alten Stand zurück
- T-0214: Jetzt sichern → Meldung; Wiederherstellen fragt nach und meldet Ergebnis
- T-0299: Zeitleiste zeigt Sicherungen, Vergleich mit heute, Vorschau, Einzeldatei nach Ja zurück (Nein ändert nichts)
