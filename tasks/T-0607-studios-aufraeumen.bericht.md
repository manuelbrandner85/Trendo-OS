# Bericht T-0607: Studio-Liste aufgeräumt

**Zweig:** `claude/T-0607-studios-aufraeumen`. Nach `main` übernommen am 09.11.2026 nach ausdrücklichem OK.

## Inhalt

Die Übersicht, das Startmenü und der Desktop zeigen 37 statt 59 Studios. 18 Studios sind eingegliedert und bleiben über die Suche sowie die Leiste „Gehört hierher“ im aufnehmenden Studio oder in den Einstellungen erreichbar. SPEC-07, SPEC-04, SPEC-16 und CORE-09 werden nirgends mehr angeboten.

Die Kennungen, Seiten und Modelle bleiben erhalten, damit Register, Tests und der Zweig T-0700 gültig bleiben. Der Code der entfernten Studios bleibt also im Produkt, ist aber nicht mehr angeboten.

## Testergebnis

Volltest auf `b1f4c7b54`, Exitcode 0: Api 16 bestanden; Core 2158 bestanden, 5 übersprungen; App 955 bestanden, 4 übersprungen; Paket-Tests 9 OK. Ein vorheriger Lauf hatte 6 Fehler: 4 Testerwartungen an die alte Liste, 1 Bildeditor-Test, weil die Leiste Platz nahm, 1 einzelner Ausfall in `CORE-07 (UI)` unter Last. Die Leiste wurde verkleinert, die Fenster im Test vergrößert.

## Nicht geprüft

- Sichtprüfung am echten Bildschirm.
- Die Leiste nimmt im Bildeditor etwas Höhe weg.
- Die 48 neuen Studios und die Lücken bis 12 je Kategorie sind nicht begonnen.

## Status

Auf `main`. Keine Abnahme durch eine Person.
