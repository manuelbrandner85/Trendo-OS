# T-0309 – Recherche mit Markierungen

- Freigabe: Manuel im Chat am 01.11.2026 (Auswahl „Recherche mit Markierungen“).
- Vorher: Lesemodus mit reinem Text, Notizfeld und Quellenliste der Sitzung; Bericht nur mit Notizen und Quellen.
- Jetzt (`MainWindow.ResearchMarks.cs`, Kern `ResearchDesk` + `W3/ResearchMarks.cs`):
  - Textmarker: Stelle im Lesetext auswählen, Farbe antippen (Gelb, Grün, Blau, Rot), optional mit Notiz; die Stelle erscheint farbig hinterlegt im Text. Gleiche Stelle erneut ersetzt Farbe/Notiz statt doppelt zu markieren; Leerzeichen am Rand fallen weg.
  - Randliste „Markierungen“: Zitat, Notiz (Enter speichert), Farbe ändern, „Zitat kopieren“ (mit Quellenangabe), entfernen.
  - Zitatsammlung über alle Quellen der Sitzung; Literaturliste ohne Doppelte im Stil „einfach (DIN-ähnlich)“ oder „APA“, kopierbar.
  - „Als Bericht speichern“ nimmt Zitate (mit Farbe und Notiz) und die Literaturliste mit; „Sitzung beenden“ löscht auch die Markierungen (Lesemodus bleibt flüchtig).
  - Lesetext: Überschriften werden als Überschrift gezeigt (Kürzel „## “ bleibt unsichtbar, damit die Positionen der Markierungen stimmen); der Seitentitel steht nicht mehr doppelt im Text.
- Vorschaubild: Render-Schalter `TRENDO_RECHERCHE=1` (Beispielartikel, ohne Netz).
- Tests: `ResearchMarksTests` (Markieren, Abweisen, Abschnitte, Zitierstile, Literaturliste, Zitat, Bericht, Sitzungsende), `ResearchMarks_Ui` (Auswahl → Markieren, farbig im Text, Farbe/Notiz, ohne Auswahl abgewiesen, Zitat kopieren, Literaturliste einfach/APA, Bericht, Sitzungsende); `SYS08_Research`, W3-Oberflächentests grün.
