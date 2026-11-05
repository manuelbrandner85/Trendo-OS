# Vorbild-Analyse KI-Verbindungen: Claude Desktop, Raycast, Zapier-Konten, Windows-Kontoverwaltung (Clean-Room)

- Programme: „Mit Claude verbinden“, „Online-Dienste“, „Verbindungen“
- Vorbilder: Claude Desktop/Claude Code (Anmeldung im Browser), Raycast „Extensions & Accounts“, Zapier/Make „Verbindungen“, GitHub Desktop (Anmeldung), Windows „Konten“ – Funktionsumfang aus öffentlicher Doku
- Werkzeug: Funktionsumfang aus öffentlicher Doku und Bedienkonzept; kein fremder Code, keine fremden Grafiken oder Texte. Trendo-Stand aus Code und Tests, Stand 05.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Claude über das eigene Abo: offizielle Anmeldung von Claude im Browser, Trendo sieht weder Passwort noch Schlüssel, speichert keinen Token (alte werden beim Start gelöscht)
- ✓ Anmeldestand anzeigen (ohne das Abo zu verbrauchen), „In Claude Code öffnen“ im Projektordner, je Person ein eigener Claude-Ordner
- ✓ Kostenpflichtige API wird nie vorausgesetzt; API-Variablen werden für Claude-Läufe entfernt
- ✓ Verbindungen: GitHub je Person über die offizielle GitHub CLI (Anmeldung im Browser), Netlify, Erweiterungen für Claude; vor jeder Änderung Rückfrage „✓ Ja/Abbrechen“
- ✓ Online-Dienste: Zugangsschlüssel nur im Windows-Schlüsselspeicher, nie in Dateien; Schlüssel prüfen, Dienst einschalten
- ✓ Testmodus startet nie ein echtes Programm
- ◐ Übersicht „was ist verbunden, seit wann, zuletzt benutzt“ über alle Dienste auf einer Seite – je Dienst vorhanden, nicht gesammelt
- ✗ Verbindung zeitlich begrenzen (z. B. „nach 30 Tagen trennen“) – bewusst offen, Abmeldung übernimmt der jeweilige Dienst

## Bedienkonzept (Profi-Bauplan A1–A3)
Je Dienst eine Karte mit Stand und genau einem Hauptknopf (Verbinden/Trennen) – wie die Kontoseiten der Vorbilder.

## Nächste Schritte
1. Gesammelte Übersicht aller Verbindungen
