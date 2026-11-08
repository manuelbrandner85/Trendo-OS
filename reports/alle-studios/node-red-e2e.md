# Echter Lauf: Node-RED mit Trendos Einstellungen (Aufgaben automatisieren) – 08.11.2026

Freigabe des Nutzers: „Ja, alle testen“. Die Anbindung gibt es schon (N1–N7, `MainWindow.N8n.cs`, `N8n.NodeRedSettings`/`NodeRedArgs`). Neu ist hier nur der echte Lauf.

## Einrichtung
- `npm install node-red@5.0.9 --prefix <temp> --no-fund --no-audit --omit=dev --ignore-scripts` → 276 Pakete in 34 s, 109 MB. Kein globales Paket, ohne Admin.
- Node-RED 5.0.9 (Apache-2.0) läuft auch **ohne** npm-Installationsskripte.

## Lauf (Trendo-Einstellungsdatei unverändert, `--userDir <daten> --port 1880 --settings <daten>/settings.js`)
- `GET http://127.0.0.1:1880/settings` → Version 5.0.9, bereit nach wenigen Sekunden.
- Ablauf über die Admin-Schnittstelle übernommen (`POST /flows`, „full“) → HTTP 204.
- Ablauf „http in → function → http response“: `GET /trendo-test` → `{"gruss":"Hallo aus Node-RED","summe":5}`.
- Aufruf über die Netzwerkadresse des PCs (statt 127.0.0.1) → nicht erreichbar. `uiHost: 127.0.0.1` wirkt.
- Prozess danach beendet (eigene PID und Kindprozess).

## Befunde (umgesetzt 08.11.2026 – Nutzer: „mach einfach alles automatisch“)
1. **Admin-Schnittstelle ohne Anmeldung:** Jedes Programm auf diesem PC kann Abläufe mit eigenem Code (Funktionsbausteine) übernehmen. Vorschlag: `adminAuth` mit einem von Trendo erzeugten Schlüssel setzen, so wie bei der Trendo-Schnittstelle.
2. **npm ohne `--ignore-scripts`:** `N8n.InstallArgs` erlaubt Installationsskripte. Für Node-RED ist belegt, dass es ohne sie läuft, und das Abschalten würde fremden Code beim Einrichten vermeiden. Für n8n ist das nicht geprüft.

## Nicht geprüft
- Einrichtung und Start über die Trendo-Oberfläche („KI & Automatik → Abläufe“).
- Aufrufe von Trendo-Aktionen aus Node-RED (Trendo-Schnittstelle 47800).

## Umsetzung und echter Nachlauf (Node-RED 5.0.9, neue Trendo-Einstellungsdatei)
- `N8n.InstallArgs` setzt für Node-RED `--ignore-scripts`, n8n bleibt unverändert.
- `N8n.NodeRedSettings`: Herkunftsprüfung als `httpAdminMiddleware` und `httpNodeMiddleware`. Statt einer Passwortanmeldung, die den Editor-Knopf in Trendo sperren würde, nimmt Node-RED nur Anfragen an 127.0.0.1/localhost an, und nur von dort.

| Anfrage | Antwort |
|---|---|
| Einstellungen von 127.0.0.1 / localhost | 200 / 200 |
| Editor-Seite | 200 |
| Ablauf übernehmen mit Herkunft http://127.0.0.1:1880 | 204 |
| Ablauf übernehmen mit Herkunft https://boese.example (fremde Webseite) | **403** |
| Host boese.example (DNS-Rebinding) | **403** |
| Herkunft „null“ (Sandbox-Rahmen) | **403** |
| eigene Ablauf-Adresse lokal / mit fremdem Host | 200 / **403** |

- Tests: `N8nTests` 7 grün (neu: `NodeRed_hardened`).
- Grenze: Programme, die auf diesem PC laufen, können die Schnittstelle weiterhin nutzen. Dagegen hülfe nur eine Anmeldung, die den Editor-Knopf erschweren würde.
