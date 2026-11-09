# Testübersicht: Messwerte auswerten

Studio SPEC-05 · Messwerte auswerten · Bereich Wissen & Lernen

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

Gezählte Tests des Studios: 87. Gefordert für die Abnahme 4.0: mindestens 20 eigene positive und negative Testfälle.

## Dem Studio zugeordnete Tests

Die Zuordnung kommt aus dem Programmkatalog und ist ungenau: Sie kann Tests anderer Studios enthalten (siehe REA-Audit).

- Bedienstufen: Einfach blendet Fachwerkzeuge aus, Erweitert zeigt alle, Wechsel lässt das Dokument unverändert; Einstellung bleibt gespeichert
- Profi-Stufe: Befehlssuche führt Befehle aus, Verlauf springt zurück, Projektdaten – falsches JSON wird abgelehnt, richtiges übernommen und rückgängig machbar
- SPEC-05 F05: Zusammenhang deuten
- SPEC-05 F07: Gepaarter t-Test
- SPEC-05 F08: Quartile & Ausreißer
- SPEC-05 F09: Histogramm
- SPEC-05 F10: Konfidenzintervall
- SPEC-05 F11: Chi²-Test
- SPEC-05 F12: Fehlerfortpflanzung
- SPEC-05 F13: Stichprobengröße
- SPEC-05 F14: Streudiagramm
- SPEC-05 F15: Glätten
- SPEC-05 F16: Standardisieren
- SPEC-05 UI: Werte in die Tabelle tippen (Enter/Tab), Spalte per Kopf wählen, Kennzahlen, Diagrammart, Ausgleich, Export
- SPEC-05 UI: Zwei Gruppen vergleichen (t-Test), neue Spalte aus Glättung, CSV laden, Rechner rechnen sofort
- SPEC-05: CSV mit deutschen Zahlen, Kennzahlen, Regression, Welch-t-Test mit p-Wert
- T-0201: Datei aufs Feld oder aufs Studio ziehen füllt das passende Feld
- T-0201: Mausklick auf „Datei wählen …“ füllt das Feld, die Datei steht danach unter „Zuletzt“; Eintippen geht weiter
- T-0201: Ordnerfeld öffnet den Ordner-Dialog
- T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“
- T-0315: Rückfrage als Knöpfe mit Empfehlung zuerst, „Immer so“ merkt die Antwort; gemerkte Vorliebe sichtbar und löschbar
- T-0315: Verwerfen lässt alles, wie es war; Unverstandenes geht an den Studio-Ablauf; Echtzeit-Vorschau beim Tippen zeigt, was passieren würde
- T-0315: „Füge 3 Zeilen hinzu und zwei Spalten“ → Plan, Live-Haken, Vorschau ohne Änderung am Original; Übernehmen = ein Schritt, Rückgängig nimmt ihn zurück

## Pflichtprüfungen der Abnahme 4.0

Alle Punkte offen, bis ein Testartefakt vorliegt: alte Funktionen, neue Funktionen, Bedienbarkeit, Erreichbarkeit, Dateikompatibilität, Fehlerbehandlung, Rechte, Leistung, Speicher, Stabilität, Tastatur und Maus, Ziehen und Ablegen, Rückgängig, Wiederherstellung, Exporte, Zusammenspiel mit anderen Studios.
