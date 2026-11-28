# Apps – Testplan

Studio: Programme starten (SYS-11) · Bereich: System & Sicherheit · Seite: `apps` · Stand: 0.9.114 (Stand 99cbb5c)

20 Tests sprechen dieses Programm an.

- SYS-11 (UI): Tabs; Verknüpfung anlegen; Zurücksetzen erst nach Bestätigung; Voraussetzungen prüfen
- SYS-11 T-0764 UI: Kinderprofil – „Ja, entfernen“ auf der Programm-Seite verschiebt nichts (Kern lehnt ab)
- SYS-11 T-0764 UI: Pflegen → Öffnen → Starten/Entfernen auf einer Seite; Entfernen mit Rückfrage in die Sicherung, Wiederherstellen bringt es zurück
- SYS-11 T-0764: Entfernen verschiebt in die Sicherung (nichts gelöscht), Wiederherstellen bringt es zurück, „Endgültig löschen“ gibt den Platz frei
- SYS-11 T-0764: GitHub-Programm entfernen nimmt es aus der Liste, Wiederherstellen trägt es wieder ein
- SYS-11 T-0764: Läuft das Programm noch (Datei offen), wird nichts verschoben – ganz oder gar nicht
- SYS-11 T-0764: Nur Trendos eigener Ordner – Pfadtricks, fremde Sicherungen und Verknüpfungen werden abgelehnt; Kinderprofil darf nichts
- SYS-11 T-0764: Portable, Vollpaket und GitHub erscheinen an einem Ort – mit Programmdatei, Herkunft und Größe
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
- Studio-Seiten: Überschrift ist der Name aus dem Studio-Katalog
