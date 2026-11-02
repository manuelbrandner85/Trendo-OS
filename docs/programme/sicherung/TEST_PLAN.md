# Sicherung – Testplan

Studio: Daten schützen (CORE-11) · Bereich: System & Sicherheit · Seite: `sicherung` · Stand: 0.9.36 (Stand fecc44b)

5 Tests sprechen dieses Programm an.

- CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen
- CORE-11: Ordnerprüfung findet Passwort, Schlüssel, gültige IBAN, Kartennummer – maskiert
- CORE-11: Schutz-Prüfung erkennt fehlende Sicherung und verändertes Protokoll
- T-0214: Jetzt sichern → Meldung; Wiederherstellen fragt nach und meldet Ergebnis
- T-0299: Zeitleiste zeigt Sicherungen, Vergleich mit heute, Vorschau, Einzeldatei nach Ja zurück (Nein ändert nichts)
