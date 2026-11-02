# System & Sicherheit – Testplan

Studio: Qualität prüfen (SYS-13) · Bereich: System & Sicherheit · Seite: `system` · Stand: 0.9.36 (Stand 6cf88c3)

26 Tests sprechen dieses Programm an.

- CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen
- SPEC-12 UI: Prüfsumme vergleichen, Fall anlegen, Beweis sichern, Bericht
- SYS-04 UI: Suchen, installieren, Updates anzeigen und aktualisieren, entfernen
- SYS-06 Freigabe-Dialog: Fenster schließen (X) lehnt ab
- SYS-06 Freigabe-Dialog: Klick auf „Ablehnen“ lehnt ab
- SYS-06 Freigabe-Dialog: Klick auf „Erlauben“ startet die Freigabe; Schließen des Hauptfensters beendet sie
- SYS-06 Freigabe-Dialog: nennt Ordner und Dauer, Esc lehnt ab
- SYS-06 Freigabe-Gate: ohne Zustimmung keine Freigabe, Anfrage abgelehnt
- SYS-06 UI: Ordner teilen, Datei echt per HTTP laden, falsches Token abgelehnt, beenden
- SYS-07 UI: Geräte suchen zeigt Gruppen
- SYS-08 UI: Seite im Lesemodus lesen, .onion wird nicht abgerufen, Sitzung beenden
- SYS-09 UI: Kontakt anlegen, Entwurf prüfen und im Mailprogramm öffnen (nichts wird gesendet)
- SYS-13 (UI): Rückfall-Sicherung und vorige Version; Protokoll-Prüfung
- SYS-13-P09: Hash-Kette erkennt Ändern, Löschen und Vertauschen samt Stelle
- SYS-13-P13: Jede Seite gehört zu einem Modul aus requirements.yaml
- SYS-13-P14: Fehler in einer Seite zeigt freundliche Fehlerseite statt Absturz
- SYS-13-P15/P16: Median-Budget und Leck-Grenze 50 MB
- SYS-13-P15/P16: Start < 3 s, Seitenwechsel im Median < 300 ms, 100 Wechsel ohne > 50 MB Zuwachs
- SYS-13-P20: Alte Einstellungsdatei (v1) wird gelesen und angehoben
- SYS-13-P21: Keine Telemetrie-Bibliothek oder -Adresse im Quellcode
- SYS-13-P22: NuGet-Lizenzliste ist vollständig (alle Pakete aus src/)
- SYS-13-P23: Ausgabe von dotnet list package --vulnerable wird erkannt
- SYS-13-P25: Rückfall – vorige Version, Sicherung, Anleitung vorhanden
- SYS-13: Absturzprotokoll schwärzt Benutzerpfade und behält höchstens 20 Dateien
- SYS-13: Release-Skript baut nur von sauberem main
- X-02 UI: Beim Start automatisch geprüft, Seite zeigt Ergebnis, erneut prüfen
