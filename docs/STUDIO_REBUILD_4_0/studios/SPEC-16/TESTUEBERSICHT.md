# Testübersicht: Auto & Fahrten

Studio SPEC-16 · Auto & Fahrten · Bereich Technik & Fachgebiete

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

Gezählte Tests des Studios: 101. Gefordert für die Abnahme 4.0: mindestens 20 eigene positive und negative Testfälle.

## Dem Studio zugeordnete Tests

Die Zuordnung kommt aus dem Programmkatalog und ist ungenau: Sie kann Tests anderer Studios enthalten (siehe REA-Audit).

- Alle Studios: kurze deutsche Namen und ein Satz, nach Bereichen gruppiert, keine Kennungen oder Fachbegriffe sichtbar
- Download-Manager: mehrere Links auf einmal, höchstens N gleichzeitig, Fortschritt, Anhalten/Fortsetzen, Prüfsumme falsch → Fehler; ohne Netz (nachgebildet)
- Fahrtenbuch (T-0311): Fahrt korrigieren mit Grund, Fahrt stornieren mit Grund, Monat festschreiben – danach keine Änderung mehr möglich
- Lernplan: Plan mit Prüfungstermin anlegen → Tagesplan mit Ziel, Kalender mit Prüfungstag; „Jetzt lernen“ lernt in Plan-Reihenfolge und hält das Tempo
- SPEC-16 F05: E-Auto
- SPEC-16 F14: Pendlerpauschale
- SPEC-16 F15: 1-%-Regel
- SPEC-16 F16: Kilometergeld
- SPEC-16 F17: Reifen
- SPEC-16 F18: Fahrtkosten teilen
- SPEC-16 F19: Ladezeit
- SPEC-16 F20: Jahreskosten
- SPEC-16 UI: Fahrten eintragen (dienstlich mit Zweck), Lücke wird gemeldet, Export CSV, Rechner rechnen sofort mit Werten des Fahrzeugs, Löschen nur mit Bestätigung
- SPEC-16 UI: Fahrzeug anlegen, dreimal voll tanken (Enter), Verbrauch und ct/km in der Übersicht, Tankung in der Tabelle korrigieren
- SPEC-16: Tankbuch (Volltank-Methode), Fahrtenbuch mit Lücken, Erinnerungen, CSV
- Symbole: alle Pfade sind gültige Avalonia-Geometrien mit Akzentfarben
- Symbole: jede Karte unter „Alle Studios“ und jeder Fach-Reiter zeigt sein Studio-Symbol
- Symbole: jedes Studio aus requirements.yaml hat ein eigenes, eindeutiges Symbol

## Pflichtprüfungen der Abnahme 4.0

Alle Punkte offen, bis ein Testartefakt vorliegt: alte Funktionen, neue Funktionen, Bedienbarkeit, Erreichbarkeit, Dateikompatibilität, Fehlerbehandlung, Rechte, Leistung, Speicher, Stabilität, Tastatur und Maus, Ziehen und Ablegen, Rückgängig, Wiederherstellung, Exporte, Zusammenspiel mit anderen Studios.
