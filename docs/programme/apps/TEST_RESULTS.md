# Apps – Testergebnisse

Studio: Programme starten (SYS-11) · Bereich: System & Sicherheit · Seite: `apps` · Stand: 0.9.110 (Stand 0eb9d52)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 20 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| SYS-11 (UI): Tabs; Verknüpfung anlegen; Zurücksetzen erst nach Bestätigung; Voraussetzungen prüfen | ✓ bestanden |
| SYS-11 T-0764 UI: Kinderprofil – „Ja, entfernen“ auf der Programm-Seite verschiebt nichts (Kern lehnt ab) | ✓ bestanden |
| SYS-11 T-0764 UI: Pflegen → Öffnen → Starten/Entfernen auf einer Seite; Entfernen mit Rückfrage in die Sicherung, Wiederherstellen bringt es zurück | ✓ bestanden |
| SYS-11 T-0764: Entfernen verschiebt in die Sicherung (nichts gelöscht), Wiederherstellen bringt es zurück, „Endgültig löschen“ gibt den Platz frei | ✓ bestanden |
| SYS-11 T-0764: GitHub-Programm entfernen nimmt es aus der Liste, Wiederherstellen trägt es wieder ein | ✓ bestanden |
| SYS-11 T-0764: Läuft das Programm noch (Datei offen), wird nichts verschoben – ganz oder gar nicht | ✓ bestanden |
| SYS-11 T-0764: Nur Trendos eigener Ordner – Pfadtricks, fremde Sicherungen und Verknüpfungen werden abgelehnt; Kinderprofil darf nichts | ✓ bestanden |
| SYS-11 T-0764: Portable, Vollpaket und GitHub erscheinen an einem Ort – mit Programmdatei, Herkunft und Größe | ✓ bestanden |
| SYS-11-P07: Portable-ZIP nur mit richtiger Prüfsumme entpacken | ✓ bestanden |
| SYS-11-P10: Verknüpfungen mit Parametern (Anführungszeichen) speichern und entfernen | ✓ bestanden |
| SYS-11-P11: Autostart lesen; ein/aus nur mit Bestätigung, nichts gelöscht | ✓ bestanden |
| SYS-11-P14: Offline-Installer – Prüfsumme aus .sha256 und SHA256SUMS, falsche/fehlende erkannt | ✓ bestanden |
| SYS-11-P15/P20: Systemvoraussetzungen blockieren mit Grund; Kompatibilitätshinweise | ✓ bestanden |
| SYS-11-P18: Zurücksetzen legt Sicherung an und lässt sich wiederherstellen | ✓ bestanden |
| SYS-11-P19/P06: Programmnamen zu Zuordnungen; Update-Text | ✓ bestanden |
| SYS-11-P21: Integrationstest startet Programm und protokolliert Version/Fehler | ✓ bestanden |
| SYS-11-P22/P23: CSV-Export und winget-Wiederherstellungsliste (Rundreise) | ✓ bestanden |
| SYS-11-P24/P25: Platz je Werkzeug; Selbsttest-Bericht ohne private Pfade | ✓ bestanden |
| SYS-11/X-09: Apps hat genau eine Unterebene; Programme und Speicher-Seite öffnen | ✓ bestanden |
| Studio-Seiten: Überschrift ist der Name aus dem Studio-Katalog | ✓ bestanden |
