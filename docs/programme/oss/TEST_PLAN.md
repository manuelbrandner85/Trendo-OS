# Open Source – Testplan

Studio: Projekte von GitHub holen (X-06) · Bereich: Entwickeln · Seite: `oss` · Stand: 0.9.113 (Stand 1978f1a)

27 Tests sprechen dieses Programm an.

- Beschreibung: README wird beim Aufklappen geladen; geholte Projekte lassen sich mit Rückfrage entfernen
- GitHub (UI): Treffer mit Beschreibung, „Zustimmen & installieren“ richtet ein, Programmseite zeigt Starten/Entfernen
- Handy bedient Trendo: Bildschirmbild, Knopf antippen, Feld antippen und schreiben, Löschen
- Installer-Programm: erst Signaturprüfung mit Ampel (unsigniert = Rot), Ausführen nie im selben Klick, Rot nur nach eigener Risiko-Bestätigung; dann still ausführen, erkennen, starten; Entfernen ruft den Deinstaller
- T-0312: Aufgebrauchtes GitHub-Limit → Meldung mit Uhrzeit; Projekt ohne Windows-Programm und kaputte Antwort beenden die Anzeige statt bei 0 % zu hängen
- T-0312: Download zeigt steigenden Prozentwert bis zur fertigen Einrichtung; stockender Download wird nach dem Zeitlimit gemeldet
- T-0312: Hängende GitHub-Antwort → nach dem Zeitlimit klare Meldung, Anzeige beendet, nächster Versuch möglich; Sekundenzähler beim Suchen
- T-0313: Abgebrochener Download – angefangene Datei bleibt, nächster Versuch lädt mit Range ab der Stelle weiter (206) und richtet ein
- T-0313: GitHub-Anmeldung – Schlüssel wird geprüft und im sicheren Speicher abgelegt, geht nur an api.github.com, Abmelden löscht ihn; falscher Schlüssel abgewiesen
- T-0313: Trefferliste zeigt „✓ Windows-Programm · Version · Größe“ bzw. „Nur Quellcode“; Installieren nur, wo es ein Windows-Programm gibt
- T-0313: Update – neue Version wird erkannt (Versionsvergleich), „Aktualisieren“ richtet sie ein, danach kein Update mehr
- T-0314: Fehlgeschlagener Schritt → klare Meldung mit letzter Ausgabe, nichts eingetragen, alter Stand bleibt; Rust-Projekt zeigt Grund und findet Fork mit Windows-Programm und PyPI-Paket
- T-0314: Python-Projekt ohne Windows-Programm → Abzeichen „läuft in Trendo“, Zustimmung, Einrichtung mit uv (sichtbare Schritte), Start im Fenster, Entfernen
- T-0314: Reine Web-Seite läuft ohne Laufzeit über Trendos lokalen Server (nur 127.0.0.1, nur der Projektordner) und lässt sich beenden
- T-0762 B2 (UI): unsigniertes ZIP-Programm startet nach „Installieren & starten“ NICHT – Ampel Rot, Start erst nach Häkchen und eigenem Klick, danach ohne Rückfrage; geänderte Datei fragt erneut
- T-0762 L1 (UI, Signatur ersetzt und künstlich verlangsamt): „Starten“ kehrt sofort zurück, Prüfung läuft im Hintergrund mit Fortschritt und „Abbrechen“; nach Erlaubnis nur Index + ausführbare Inhalte
- T-0762 X-06 (UI): geholtes Projekt mit einem Klick im Code-Editor, Bauanleitung als Knopf, echter dotnet-Bau (übersprungen ohne SDK); Entfernen ohne Rest
- T-0762 X-06 (UI, Signatur ersetzt, Installer-Lauf ersetzt): gültig signierter Installer = Grün ohne Häkchen, aber eigener Klick; Datei ist bis zum Start gesperrt; „Nicht ausführen“ verwirft; Knopf gehört zu genau seiner Karte
- T-0762 X-06 PC-Lauf: Suche → freies Windows-Programm (ripgrep) mit Prüfsumme holen → starten → per PID beenden → Entfernen ohne Rest; falsche Prüfsumme abgewiesen; Quellcode holen → Code-Editor → bauen → entfernen (nur mit TRENDO_PC_LAUF=1)
- T-0762 X-06: Bauanleitung erkannt (dotnet, npm mit Skripten, cargo, go, Python, CMake) – Werkzeug fehlt wird markiert, vorhandene zuerst
- T-0762 X-06: Entfernen löscht Ordner (auch schreibgeschützte Git-Dateien) und Herkunftsdatei – kein Rest; nur Ordner direkt im Open-Source-Ordner
- T-0762 X-06: Installer-Ampel – gültig = Grün ohne Häkchen, nicht prüfbar = Gelb, unsigniert/ungültig = Rot mit Risiko-Bestätigung, fehlend = gesperrt
- T-0762 X-06: echte Authenticode-Prüfung einer Windows-Programmdatei ergibt Grün (nur Windows)
- X-06 (UI): Suche zeigt Treffer mit Lizenz, ohne Lizenz kein Herunterladen
- X-06: GitHub-Antwort wird gelesen, Lizenz verständlich erklärt
- X-06: Kopie holen (echtes git, lokales Quell-Repo) mit Herkunftsdatei; unsichere Adressen abgewiesen
- X-06: Live-Suche bei GitHub (übersprungen ohne Internet)
