# T-0303 – Trendo PDF als echter Betrachter: Miniaturen und Seiten ordnen

- Freigabe: Manuel im Chat am 01.11.2026 (Auswahl „PDF als echter Betrachter“).
- Vorher: Lesen, Suchen, Lesezeichen, Markieren, Formulare; Seiten auswählen/drehen nur über Eingabefelder („1-3,5“), jeweils als eigene Datei.
- Jetzt (`MainWindow.PdfOrganize.cs`, Kern `PdfTools.Rearrange`):
  - Miniaturleiste neben der Seite; Klick springt hin, aktuelle Seite hervorgehoben (Miniaturen im Hintergrund gezeichnet).
  - „▦ Seiten ordnen“: alle Seiten als Kacheln; Klick wählt, Strg/Umschalt mehrere; per Maus ziehen verschiebt (goldene Einfügemarke), ⟲/⟳ drehen, löschen, „Nach vorn/hinten“, leere Seite einfügen (Größe der Seite davor), andere PDF an der Stelle einfügen, Rückgängig.
  - „Als Kopie speichern“ schreibt „Name (Trendo).pdf“ neben das Original und öffnet sie – das Original bleibt Byte für Byte gleich (wie bei Anmerkungen).
- Vorschaubild: `TRENDO_PDF=<datei>` mit `TRENDO_PDF_ORDNEN=1` (die Datei wird vorher kopiert).
- Tests: `PdfOrganizeTests` (Reihenfolge, Drehung, leere Seite, zweite PDF, Vorlage unverändert, Fehlerfälle), `PdfOrganize_Ui` (Miniatur-Klick, echtes Ziehen, Drehen, Löschen, leere Seite, PDF einfügen über Dateiwahl, Rückgängig, Speichern, Original unverändert); `T0223_TrendoPdfUi` und übrige PDF-Tests grün.
