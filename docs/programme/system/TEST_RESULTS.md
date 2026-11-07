# System & Sicherheit – Testergebnisse

Studio: Qualität prüfen (SYS-13) · Bereich: System & Sicherheit · Seite: `system` · Stand: 0.9.36 (Stand 2963379)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 26 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen | ✓ bestanden |
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
| X-02 UI: Beim Start automatisch geprüft, Seite zeigt Ergebnis, erneut prüfen | ✓ bestanden |
