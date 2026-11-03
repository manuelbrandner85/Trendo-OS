# Vorbild-Analyse Anlagen-Monitor: Grafana, Home Assistant, ThingsBoard (Clean-Room)

- Programme: „Geräte überwachen“
- Recherche: Grafana (Zeitreihen-Dashboards, Schwellwerte), Home Assistant (Geräte/Entitäten, Verlauf), ThingsBoard (IoT-Anlagen, Alarme)
- Werkzeug: Funktionsumfang aus öffentlicher Doku; kein Download, kein Code übernommen. REA nicht eingesetzt (kein lokal installiertes Vorbild).

## Übernommen in Trendo „Anlagen-Monitor“ (0.9.28)
Anlagen-/Sensorbaum mit Zustand, Kacheln mit aktuellem Wert und Sparkline, Zeitreihen-Diagramm mit Schwellwert-Bändern, Zeitraumwahl, Ereignisse (Zustandswechsel mit Hysterese), Trend-Prognose, Live-Aktualisierung, CSV-Import/-Export, abgeleitete Sensoren.

## Noch nicht
Live-Anbindung per MQTT/Modbus/Home-Assistant-API, Benachrichtigungen per Mail/Push, mehrere Sensoren in einem Diagramm, frei anordenbare Dashboards.
