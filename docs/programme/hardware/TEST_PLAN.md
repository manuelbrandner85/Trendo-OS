# Hardware & Leistung – Testplan

Studio: Qualität prüfen (SYS-13) · Bereich: System & Sicherheit · Seite: `hardware` · Stand: 0.9.113 (Stand 1978f1a)

19 Tests sprechen dieses Programm an.

- Commercial Desktop Edition: Hardware & Leistung prüft den PC, zeigt Profil, Systemcheck und Eignung je Studio; auch vor dem Kauf
- SYS-13 (T-0763): Ampel je Bereich mit nächstem Schritt; Gesamtsatz; rot vor gelb vor grün
- SYS-13 (T-0763): Entwickler-Prüfungen nur in „Professionell“ und ehrlich beschriftet; Telemetrie-Karte mit echtem Ergebnis
- SYS-13 (T-0763): Studio öffnet „Alles prüfen“ – goldener Knopf, Ampel je Bereich mit nächstem Schritt; veränderte Protokollzeile rot mit Stelle
- SYS-13 (T-0763): Telemetrie-Prüfung der installierten Version über die Paketlisten – findet Analyse-Bibliotheken, Trendo selbst ist frei
- SYS-13 (T-0763): veränderte Protokollzeile wird mit Stelle gemeldet – Ampel rot im Bereich Protokoll
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
