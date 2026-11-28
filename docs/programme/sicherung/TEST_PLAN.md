# Sicherung – Testplan

Studio: Daten schützen (CORE-11) · Bereich: System & Sicherheit · Seite: `sicherung` · Stand: 0.9.113 (Stand 1978f1a)

12 Tests sprechen dieses Programm an.

- CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen
- CORE-11: Ordnerprüfung findet Passwort, Schlüssel, gültige IBAN, Kartennummer – maskiert
- CORE-11: Schutz-Prüfung erkennt fehlende Sicherung und verändertes Protokoll
- CORE-11: Sicherung enthält keine fremden Ordner – nur der Trendo-Datenordner, eine kaputte ZIP lässt die Daten unverändert
- CORE-11: Sicherung fällig – ohne Sicherung sofort, sonst erst nach dem Abstand; 0 Stunden heißt nie automatisch
- CORE-11: Sicherung – höchstens „keep“ Sicherungen, Vorschau zählt ersetzte und neue Dateien, Wiederherstellen holt den alten Stand zurück
- T-0214: Jetzt sichern → Meldung; Wiederherstellen fragt nach und meldet Ergebnis
- T-0299: Zeitleiste zeigt Sicherungen, Vergleich mit heute, Vorschau, Einzeldatei nach Ja zurück (Nein ändert nichts)
- T-0681 Sicherung UI: gesperrte Datei → „Unvollständig“ in Meldung und Liste; beschädigte Sicherung steht als solche da und wird beim Wiederherstellen übersprungen
- T-0754 Gegenprüfung 4: Studienwissen CORE-11 verspricht keine Passwortverwaltung
- T-0754 UI: Sicherung zeigt deutlich „nicht verschlüsselt“, Ordner und „Ordner öffnen“; Meldung nach dem Sichern sagt es; Ampel gelb; Studio verspricht keine Passwortverwaltung
- T-0764 (14.): Kind – „Sicherung wiederherstellen“ ist gesperrt (Rolle und Dateien bleiben); Erwachsene stellen wieder her
