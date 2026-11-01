# T-0308 – Vorhaben als Zeitplan (Gantt)

- Freigabe: Manuel im Chat am 01.11.2026 (Auswahl „Vorhaben als Zeitplan“).
- Vorher: „Gantt light“ – je Schritt nur ein Fälligkeitsdatum, Balken vom vorigen Termin bis zum eigenen, Termine nur per Eingabefeld.
- Jetzt (`GanttCanvas`, `MainWindow.W4Suite.cs`, Kern `CreationSuite` + `CreationPlan.cs`):
  - Schritte haben Beginn und Ende sowie „kommt nach“ (Abhängigkeiten). In `SCHRITTE.md` lesbar und abwärtskompatibel: `@Ende` wie bisher, `@Beginn..Ende`, `^2,3`.
  - Gantt: Monate/Tage, Wochenenden schattiert, goldene Heute-Linie, Balken je Status gefärbt, Meilensteine als Raute, Pfeile für Abhängigkeiten.
  - Maus: Balken ziehen = verschieben (in Tagen), Kante ziehen = Beginn/Ende ändern (mind. 1 Tag), gelben Punkt am Balkenende auf einen anderen Balken ziehen = „kommt danach“.
  - Nachfolger rücken automatisch hinter ihre Vorgänger (Dauer bleibt); Kreise werden abgewiesen; Rückgängig für jede Änderung.
  - Kritischer Pfad (rot) mit Puffer je Schritt; Zusammenfassung „Fertig am … · n Tage“; Zoom Tage/Wochen.
  - Feld für den gewählten Schritt: Beginn, Ende, „kommt nach“ (Häkchen); Ende/Meilenstein setzen wie bisher (auch für Schritte ohne Termin).
- **Fehler behoben (Werkzeug):** Das Render-Werkzeug (Vorschaubilder und Programmkatalog) lief ohne eigenen Projektordner und schrieb daher in `Dokumente\Trendo OS` (z. B. `Modell-Prüfer\beispiel-leseecke.glb` beim Katalog-Lauf am 31.10., ein Beispiel-Vorhaben beim Vorschaubild dieses Auftrags – sofort wieder entfernt). Jetzt liegt der Projektordner im eigenen Temp-Ordner.
- Vorschaubild: Render-Schalter `TRENDO_PLAN=1`.
- Tests: `CreationPlanTests` (Format alt/neu, Balken, Verschieben mit Nachrücken, Dauer, Kreise, kritischer Pfad mit Puffer), `Gantt_Ui` (echte Maus: Balken ziehen, Kante ziehen, Punkt-zu-Balken-Abhängigkeit mit Nachrücken, Rückgängig, Schritt-Feld); `Teil4BueroTests`, `Teil4BueroUiTests` grün.
