# Open Source – Testplan

Studio: Projekte von GitHub holen (X-06) · Bereich: Entwickeln · Seite: `oss` · Stand: 0.9.36 (Stand 9fa45e4)

25 Tests sprechen dieses Programm an.

- Beschreibung: README wird beim Aufklappen geladen; geholte Projekte lassen sich mit Rückfrage entfernen
- GitHub (UI): Treffer mit Beschreibung, „Zustimmen & installieren“ richtet ein, Programmseite zeigt Starten/Entfernen
- Handy (UI): Schritt für Schritt – Ort wählen → Code → Handy erreicht → verbunden → Bedienen erlauben; Hilfe nach 30 s mit Windows-Freigabe
- Handy (UI): Überall/Tailscale – installieren → anmelden → App aufs Handy; jeder Schritt erst, wenn der vorige geprüft ist
- Handy bedient Trendo: Bildschirmbild, Knopf antippen, Feld antippen und schreiben, Löschen
- Handy-App: Apps-Liste, App öffnet in Handy-Größe auf dem Handy (nicht am PC), Navigation bleibt auf dem Handy, Zurück zum Start
- Handy: QR öffnen → verbinden → Foto senden → Text senden → Datei vom PC laden; Fremde und Ungekoppelte abgewiesen
- Handy: Trendo bedienen nur mit Erlaubnis am PC – dann Bildschirm, Tippen, Schreiben, Tasten, Scrollen; Scan wird gemeldet; Unterwegs-Link meldet an
- Installer-Programm: Installieren & starten führt den Installer still aus, erkennt das Programm, startet es; Entfernen ruft den Deinstaller
- KI-Programme entfernen: über KI-Quellen eingerichtetes KoboldCpp und eine KI-Laufzeit lassen sich wieder entfernen
- Programme (UI): mitgelieferte Programme haben eine Beschreibung mit Funktionsweise
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
- X-06 (UI): Suche zeigt Treffer mit Lizenz, ohne Lizenz kein Herunterladen
- X-06: GitHub-Antwort wird gelesen, Lizenz verständlich erklärt
- X-06: Kopie holen (echtes git, lokales Quell-Repo) mit Herkunftsdatei; unsichere Adressen abgewiesen
- X-06: Live-Suche bei GitHub (übersprungen ohne Internet)
