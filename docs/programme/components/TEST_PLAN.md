# Zusatzprogramme – Testplan

Studio: Programme starten (SYS-11) · Bereich: System & Sicherheit · Seite: `components` · Stand: 0.9.112 (Stand d6f9fcd)

32 Tests sprechen dieses Programm an.

- GitHub (UI): Treffer mit Beschreibung, „Zustimmen & installieren“ richtet ein, Programmseite zeigt Starten/Entfernen
- Komponenten: Lizenzliste, Unreal nur geführt (offizieller Link), Zustimmung wird gespeichert
- Programme (UI): mitgelieferte Programme haben eine Beschreibung mit Funktionsweise
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
- Scrollen bleibt stehen: dieselbe Seite neu aufgebaut (wie nach einem Knopfdruck) behält die Position – auch zweimal hintereinander
- T-0313: Update – neue Version wird erkannt (Versionsvergleich), „Aktualisieren“ richtet sie ein, danach kein Update mehr
- T-0314: Python-Projekt ohne Windows-Programm → Abzeichen „läuft in Trendo“, Zustimmung, Einrichtung mit uv (sichtbare Schritte), Start im Fenster, Entfernen
- T-0314: Reine Web-Seite läuft ohne Laufzeit über Trendos lokalen Server (nur 127.0.0.1, nur der Projektordner) und lässt sich beenden
- T-0713 UI: Kinderprofil – „Fehlende Programme installieren“ zeigt keine Rückfrage, legt keine Freigabe an, installiert nichts; Not-Stopp kann es nicht aufheben
- T-0713 UI: „Fehlende Programme installieren“ – echter Sammel-Dialog mit Lizenz/Größe/Herkunft/Wirkung; abgewählt = keine Freigabe, gewählt = genau eine Freigabe und genau eine Ausführung im Tor (Testmodus)
- T-0762 B2 (UI): unsigniertes ZIP-Programm startet nach „Installieren & starten“ NICHT – Ampel Rot, Start erst nach Häkchen und eigenem Klick, danach ohne Rückfrage; geänderte Datei fragt erneut
- T-0762 L1 (UI, Signatur ersetzt und künstlich verlangsamt): „Starten“ kehrt sofort zurück, Prüfung läuft im Hintergrund mit Fortschritt und „Abbrechen“; nach Erlaubnis nur Index + ausführbare Inhalte
- T-0762 X-06 PC-Lauf: Suche → freies Windows-Programm (ripgrep) mit Prüfsumme holen → starten → per PID beenden → Entfernen ohne Rest; falsche Prüfsumme abgewiesen; Quellcode holen → Code-Editor → bauen → entfernen (nur mit TRENDO_PC_LAUF=1)
- Zusatzprogramme: keine Ordnerwahl mehr – „von GitHub laden“; ohne Internet bleibt ein Merker zum Nachladen beim nächsten Start
