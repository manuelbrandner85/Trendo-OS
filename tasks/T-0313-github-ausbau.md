# T-0313 – GitHub-Programme ausgebaut

- Freigabe: Manuel im Chat am 01.11.2026 (alle vier Vorschläge: Trefferliste, Updates, Fortsetzen, Anmeldung).
- Jetzt (`MainWindow.GitHubPlus.cs`, `MainWindow.Programme.cs`, `MainWindow.W4Oss.cs`, Kern `GitHubApps.Availability/IsNewer`):
  - **Trefferliste:** gleich nach der Suche zeigt jeder Treffer „✓ Windows-Programm · Version · Größe“, „Nur Quellcode“ oder „Noch keine Veröffentlichung“ (ohne Anmeldung die ersten 6, sonst bis 20; weitere per „prüfen“). „Installieren & starten“ nur, wo es ein Windows-Programm gibt.
  - **Updates:** „Nach Updates suchen“ (einmal je Sitzung auch von selbst) vergleicht Versionsnummern (v1.10 > v1.9), „Auf vX aktualisieren“ richtet die neue Version ein – die alte bleibt, bis die neue fertig ist.
  - **Fortsetzen:** angefangene Downloads bleiben (7 Tage) liegen; der nächste Versuch lädt per Range ab der Stelle weiter (206), sonst von vorn.
  - **Anmeldung (optional):** eigener GitHub-Schlüssel wird gegen `/rate_limit` geprüft und im sicheren Windows-Speicher abgelegt; er geht nur an api.github.com, nie an Download-Server; Abmelden löscht ihn. Limit-Meldung weist darauf hin.
- Tests: `GitHubPlus_Ui` (Trefferliste, Update, Fortsetzen mit Range, Anmeldung inkl. falschem Schlüssel und Kopfzeilen-Prüfung); mit `GitHubNet_Ui` und den bisherigen GitHub-/Open-Source-/KI-Quellen-Tests 22 grün.
