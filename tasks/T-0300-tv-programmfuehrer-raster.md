# T-0300 – TV & Live: Programmführer als Raster

- Freigabe: Manuel im Chat am 31.10.2026 (Auswahl „TV-Programmführer als Raster“).
- Vorher: schlichtes 4-Stunden-Raster ab jetzt, ohne Tage, Filter oder Suche.
- Jetzt (`MainWindow.TvGuide.cs`, Kern `Trendo.Core.Tv.TvGuide`, Erinnerungen im vorhandenen `TvLibrary`):
  - Sender × Zeit, 6 Stunden, mit festem Senderrand (Nummer, Logo, Name; Klick schaltet ein) und Zeitleiste in halben Stunden.
  - Tage (heute bis +6), „◀ 2 Std. früher“, „Jetzt“, „20:15“, „2 Std. später ▶“, Gruppenwahl.
  - Sparten in Farbe (Film, Serie, Sport, Nachrichten, Kinder, Doku, Show) aus der XMLTV-Kategorie bzw. dem Titel; Filter dimmt die übrigen.
  - Goldene Jetzt-Linie über alle Sender, laufende Sendung mit Fortschritt, Vergangenes gedimmt.
  - Suche über alle Tage (Titel, Beschreibung, Kategorie); ein Treffer springt an die Stelle im Raster; „Weitere Termine“ in den Details.
  - Merker ⏺ (Aufnahme geplant) und 🔔 (Erinnerung) direkt in den Zellen; Aufnahme wieder entfernen; Hinweis bei überschneidenden Aufnahmen.
  - Erinnern: kurz vor Beginn ein Hinweis mit „Einschalten“, nur einmal.
- **Fehler behoben:** Suchfelder mit „Datei“ im Platzhalter wurden als Datei-Auswahl umgebaut (`PathFields`): Felder, deren Platzhalter das Wort „suchen“ enthält oder deren Name auf `-search` endet, bleiben Textfelder (z. B. „Ordner durchsuchen“ bleibt ein Ordnerfeld).
- Vorschaubilder: Render-Schalter `TRENDO_TV=1` (Beispielsender) und `TRENDO_BACKUPDEMO=1` (Sicherung mit Zeitleiste).
- Tests: `TvGuideTests` (Sparten, Zeitfenster, Zeile, Suche, Überschneidungen, Erinnerungen), `TvGuide_Ui` (echte Klicks: Zelle, Sparten, Morgen, 20:15, Suche, Aufnahme mit Überschneidung, Erinnerung mit Hinweis); `TvStudioUiTests`, `TvCredentialsUiTests`, `T0201_PathFields`, `PathPickAuditTests`, `FilePickUiTests` grün.
