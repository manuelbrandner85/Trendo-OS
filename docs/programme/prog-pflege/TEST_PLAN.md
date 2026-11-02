# Pflegen – Testplan

Studio: Programme starten (SYS-11) · Bereich: System & Sicherheit · Seite: `prog-pflege` · Stand: 0.9.36 (Stand c18c198)

18 Tests sprechen dieses Programm an.

- Fenster: Einstellung übernehmen (Design-Knopf per Mausklick) schließt keine Fenster – alle kommen mit Lage und Zustand zurück
- Fenster: Größe an Ecke und Rand per Maus ziehen; an linken/rechten Rand ziehen rastet auf halbe Fläche ein
- Fenster: Startmenü öffnet Dokumente mit Taskleisten-Eintrag, Startbildschirm bleibt dahinter
- Fenster: Ziehen an der Titelleiste verschiebt; Klick ins hintere Fenster holt es nach vorn; Reiter teilen ein Fenster
- Fenster: wechseln per Taskleiste, aktives hervorgehoben, maximieren, schließen
- SYS-11 (UI): Tabs; Verknüpfung anlegen; Zurücksetzen erst nach Bestätigung; Voraussetzungen prüfen
- SYS-11-P07: Portable-ZIP nur mit richtiger Prüfsumme entpacken
- SYS-11-P10: Verknüpfungen mit Parametern (Anführungszeichen) speichern und entfernen
- SYS-11-P11: Autostart lesen; ein/aus nur mit Bestätigung, nichts gelöscht
- SYS-11-P14: Offline-Installer – Prüfsumme aus .sha256 und SHA256SUMS, falsche/fehlende erkannt
- SYS-11-P15/P20: Systemvoraussetzungen blockieren mit Grund; Kompatibilitätshinweise
- SYS-11-P18: Zurücksetzen legt Sicherung an und lässt sich wiederherstellen
- SYS-11-P19/P06: Programmnamen zu Zuordnungen; Update-Text
- SYS-11-P21: Integrationstest startet Programm und protokolliert Version/Fehler
- SYS-11-P22/P23: CSV-Export und winget-Wiederherstellungsliste (Rundreise)
- SYS-11-P24/P25: Platz je Werkzeug; Selbsttest-Bericht ohne private Pfade
- SYS-11/X-09: Apps hat genau eine Unterebene; Programme und Speicher-Seite öffnen
- Start: das Hauptfenster ist sichtbar und kommt aus dem minimierten Zustand zurück
