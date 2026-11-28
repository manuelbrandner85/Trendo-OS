# KI-Modelle vergleichen – Testergebnisse

Studio: KI-Modelle vergleichen (STUDIO-MODELS-01) · Bereich: KI & Automatik · Seite: `modelle-vergleichen-programm` · Stand: 0.9.114 (Stand 99cbb5c)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 5 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| KI-Modelle vergleichen: Quellen finden, fehlende Quelle ohne Absturz, Antworten gemischt und verdeckt, Sterne, Aufdecken, Ergebnistabelle, Exporte | ✓ bestanden |
| T-0739 ECHT: zehn Fragen aus Datei gegen zwei Ollama-Modelle in einem Durchgang, Tabelle, CSV/MD; Abbrechen und Fortsetzen echt (nur mit TRENDO_ECHT_MODELLE) | übersprungen |
| T-0739 Fragenreihe: CSV importieren, alle Fragen an zwei Modelle, Fehler mitten im Lauf steht je Frage, Tabelle, CSV/MD-Export, ein Rückgängig-Schritt | ✓ bestanden |
| T-0739 Fragenreihe: „Abbrechen“ mitten in einer Antwort behält das Erhaltene, „Fortsetzen“ fragt nur das Fehlende; ohne erreichbares Modell BLOCKIERT | ✓ bestanden |
| T-0739 Gegenprüfung: Rückgängig während eines laufenden Durchgangs ist gesperrt, mit Hinweis; danach bleibt der Stand sauber | ✓ bestanden |
| T-0739 Gegenprüfung: nach Absturz (Teilergebnisse ohne Abbruch-Merker) bietet „Fortsetzen“ nur die fehlenden Paare an; „Neu starten“ fragt vorher | ✓ bestanden |
