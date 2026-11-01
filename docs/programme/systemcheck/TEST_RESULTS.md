# Systemcheck – Testergebnisse

Studio: Qualität prüfen (SYS-13) · Bereich: System & Sicherheit · Seite: `systemcheck` · Stand: 0.9.28 (Stand 83bf3c3)

Lauf: `volltest-0.9.28-api.trx, volltest-0.9.28-app.trx, volltest-0.9.28-core.trx`

Bestanden 17 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| SYS-13 (UI): Rückfall-Sicherung und vorige Version; Protokoll-Prüfung | ✓ bestanden |
| SYS-13-P09: Hash-Kette erkennt Ändern, Löschen und Vertauschen samt Stelle | ✓ bestanden |
| SYS-13-P13: Jede Seite gehört zu einem Modul aus requirements.yaml | ✓ bestanden |
| SYS-13-P14: Fehler in einer Seite zeigt freundliche Fehlerseite statt Absturz | ✓ bestanden |
| SYS-13-P15/P16: Median-Budget und Leck-Grenze 50 MB | ✓ bestanden |
| SYS-13-P15/P16: Start < 3 s, Seitenwechsel im Median < 300 ms, 100 Wechsel ohne > 50 MB Zuwachs | ✓ bestanden |
| SYS-13-P20: Alte Einstellungsdatei (v1) wird gelesen und angehoben | ✓ bestanden |
| SYS-13-P21: Keine Telemetrie-Bibliothek oder -Adresse im Quellcode | ✓ bestanden |
| SYS-13-P22: NuGet-Lizenzliste ist vollständig (alle Pakete aus src/) | ✓ bestanden |
| SYS-13-P23: Ausgabe von dotnet list package --vulnerable wird erkannt | ✓ bestanden |
| SYS-13-P25: Rückfall – vorige Version, Sicherung, Anleitung vorhanden | ✓ bestanden |
| SYS-13: Absturzprotokoll schwärzt Benutzerpfade und behält höchstens 20 Dateien | ✓ bestanden |
| SYS-13: Release-Skript baut nur von sauberem main | ✓ bestanden |
| T-0210: Design-Wahl zeigt sofort Vorschau und „Gewählt“ | ✓ bestanden |
| T-0210: Paket-Wahl erscheint in der Zusammenfassung | ✓ bestanden |
| T-0210: Reparieren meldet „repariert ✓“ bzw. „hat nicht geklappt“ | ✓ bestanden |
| T-0210: „Ordner wählen …“ im Assistenten öffnet den Ordnerdialog und übernimmt die Wahl | ✓ bestanden |
