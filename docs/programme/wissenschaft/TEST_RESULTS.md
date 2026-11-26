# Messdaten & Auswertung – Testergebnisse

Studio: Messwerte auswerten (SPEC-05) · Bereich: Wissen & Lernen · Seite: `wissenschaft` · Stand: 0.9.110 (Stand 0eb9d52)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 28 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Bedienstufen: Einfach blendet Fachwerkzeuge aus, Erweitert zeigt alle, Wechsel lässt das Dokument unverändert; Einstellung bleibt gespeichert | ✓ bestanden |
| SPEC-05 F05: Zusammenhang deuten | ✓ bestanden |
| SPEC-05 F07: Gepaarter t-Test | ✓ bestanden |
| SPEC-05 F08: Quartile & Ausreißer | ✓ bestanden |
| SPEC-05 F09: Histogramm | ✓ bestanden |
| SPEC-05 F10: Konfidenzintervall | ✓ bestanden |
| SPEC-05 F11: Chi²-Test | ✓ bestanden |
| SPEC-05 F12: Fehlerfortpflanzung | ✓ bestanden |
| SPEC-05 F13: Stichprobengröße | ✓ bestanden |
| SPEC-05 F14: Streudiagramm | ✓ bestanden |
| SPEC-05 F15: Glätten | ✓ bestanden |
| SPEC-05 F16: Standardisieren | ✓ bestanden |
| SPEC-05 UI: Werte in die Tabelle tippen (Enter/Tab), Spalte per Kopf wählen, Kennzahlen, Diagrammart, Ausgleich, Export | ✓ bestanden |
| SPEC-05 UI: Zwei Gruppen vergleichen (t-Test), neue Spalte aus Glättung, CSV laden, Rechner rechnen sofort | ✓ bestanden |
| SPEC-05: CSV mit deutschen Zahlen, Kennzahlen, Regression, Welch-t-Test mit p-Wert | ✓ bestanden |
| T-0201: Datei aufs Feld oder aufs Studio ziehen füllt das passende Feld | ✓ bestanden |
| T-0201: Mausklick auf „Datei wählen …“ füllt das Feld, die Datei steht danach unter „Zuletzt“; Eintippen geht weiter | ✓ bestanden |
| T-0315: Rückfrage als Knöpfe mit Empfehlung zuerst, „Immer so“ merkt die Antwort; gemerkte Vorliebe sichtbar und löschbar | ✓ bestanden |
| T-0315: Verwerfen lässt alles, wie es war; Unverstandenes geht an den Studio-Ablauf; Echtzeit-Vorschau beim Tippen zeigt, was passieren würde | ✓ bestanden |
| T-0315: „Füge 3 Zeilen hinzu und zwei Spalten“ → Plan, Live-Haken, Vorschau ohne Änderung am Original; Übernehmen = ein Schritt, Rückgängig nimmt ihn zurück | ✓ bestanden |
| T-0707 ECHT: lokale KI (Ollama, bestes Modell nach T-0703) ändert in drei Programmen das offene Dokument, Datei auf der Platte, Rückgängig (nur mit TRENDO_ASSIST_ECHT) | ✓ bestanden |
| T-0707: Drei Programme aus drei Fächern – Auftrag → KI-Plan → Vorschau → Ja → ein Rückgängig-Schritt im offenen Dokument → echte Datei → Rückgängig (KI ersetzt) | ✓ bestanden |
| T-0707: KI liefert nicht reparierbares JSON – die Kette fragt einmal nach und arbeitet mit der Korrektur weiter | ✓ bestanden |
| T-0707: KI-Befehl im Menüband („Weiterschreiben mit Claude“) ohne Claude – vorher still nichts, jetzt dieselbe Kette ins offene Dokument | ✓ bestanden |
| T-0707: Programme außerhalb der Hülle (Text, Tabelle, Geld …) – einheitlich „Noch nicht im Dokument möglich“, Liste aus dem Katalog | ✓ bestanden |
| T-0707: Rechte in der Kette – beim Not-Stopp ändert die KI nichts; Claude-Brücke prüft mit derselben Regel (Text bleibt Text) | ✓ bestanden |
| T-0725 Echtlauf Neues Projekt: LibreOffice Calc öffnet die Schritte-CSV (Semikolon, UTF-8), Ical.Net liest die Kalenderdatei; Messwerte: LibreOffice öffnet das Diagramm-PNG | ✓ bestanden |
| T-0725 Messwerte: leere Messreihe → „CSV öffnen …“ (Dateiauswahl) und CSV auf die Tabelle ziehen, Rückgängig; Diagramm als PNG-Bild | ✓ bestanden |
