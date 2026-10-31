# T-0296 – Notizbuch: Lese-Ansicht, Checklisten, Verknüpfungen, Bilder, PDF

- Freigabe: Manuel im Chat am 31.10.2026 (Auswahl „Notizen als echtes Notizprogramm“).
- Befund: Es gab schon ein Notizbuch (Alltag › Notizbuch) mit Abschnitten, Seiten als Markdown, automatischem Speichern, Schlagworten und Suche. Statt eines zweiten Notizprogramms (Masterprompt: keine Doppelungen) wurde dieses ausgebaut; ein erster Neubau wurde verworfen.
- Neu im Notizbuch:
  - Werkzeugzeile: „👁 Lesen / ✎ Bearbeiten“, „☐ Checkliste“, „[[ ]] Verknüpfen“, Bild einfügen, „Als PDF“.
  - Lese-Ansicht: Überschriften, Checklisten zum Abhaken (sofort gespeichert, Fortschrittsbalken), [[Verknüpfungen]] als Knöpfe – fehlende Seite wird im Abschnitt angelegt –, Bilder.
  - „Verweist hierher“: alle Seiten, die auf die offene Seite verweisen.
  - PDF-Export über das vorhandene Text-PDF (Checklisten als ☐/☑).
- Werkzeuge-Seite: Knopf „Zum Notizbuch →“ (Schnellnotizen bleiben ein eigener kleiner Speicher).
- Kern `NoteText` (Titel, Schlagworte, Verknüpfungen, Bilder, Checklisten zählen/umschalten, Verweise, Umwandlung für PDF).
- Tests: `NotebookPlus_Ui` (Regeln; echte Maus: abhaken speichert, Verknüpfung legt Seite an, Verweist hierher, zurück, PDF); `N5N20_HelferUi` grün.
