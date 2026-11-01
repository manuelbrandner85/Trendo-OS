# T-0312 – GitHub-Programme: Laden hängt bei 0 %

- Meldung: Manuel im Chat am 01.11.2026 – „es steht dauerhaft bei 0 % und ‚neueste Version auf GitHub suchen‘“.
- Ursachen (im Code gefunden):
  1. Projekt ohne fertiges Windows-Programm: Trendo brach ab, **schloss aber die Einrichtungsanzeige nicht** – sie blieb für immer bei „Neueste Version auf GitHub suchen … 0 %“.
  2. Kein Zeitlimit beim Suchen: der HTTP-Client wartete bis zu 30 Minuten, wenn eine Verbindung hing (z. B. kaputtes IPv6, Virenschutz, Proxy).
  3. „Abbrechen“ wirkte nicht: die Abfragen bekamen das Abbruch-Signal nicht.
  4. Unerwartete Antworten (fehlende Felder) lösten Fehler aus, die niemand abfing → Anzeige hing.
  5. Der Prozentwert bewegte sich beim Herunterladen nicht (fest 25 %).
- Behoben (`MainWindow.Programme.cs`, `MainWindow.InstallOverlay.cs`, `GitHubApps.ParseRelease`, KI-Quellen nutzen denselben Weg):
  - Jede Abfrage mit Zeitlimit (20 s, ein zweiter Versuch), Verbindungsaufbau IPv4 zuerst mit 8 s je Adresse, Abbrechen wirkt sofort.
  - Aufgebrauchtes GitHub-Limit (60 Abfragen/Stunde ohne Anmeldung) wird mit Uhrzeit erklärt; „neueste Version“ 10 Minuten zwischengespeichert.
  - Kein Windows-Programm / Fehler jeder Art: die Anzeige endet immer mit einer verständlichen Meldung.
  - Suchen mit Sekundenzähler; Download mit echtem Prozentwert, MB, MB/s und Restzeit; stockt der Download 45 s, klare Meldung; unvollständiger Download wird erkannt.
  - `ParseRelease` überspringt unvollständige Einträge statt abzustürzen.
- Tests: `GitHubNet_Ui` (nachgebautes GitHub: hängende Antwort, Limit, nur Quellcode, kaputte Antwort, Zwischenspeicher, steigender Prozentwert bis zur Einrichtung, stockender Download); 18 bestehende GitHub-/Open-Source-/KI-Quellen-Oberflächentests und 22 Kerntests grün.
- Hinweis: Wirksam beim Nutzer erst mit dem nächsten Release (installiert ist 0.9.27).
