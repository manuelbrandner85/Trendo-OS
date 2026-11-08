# Echter Lauf: Live-Messwerte über MQTT (Geräte überwachen) – 08.11.2026

Umsetzung aus Runde 5: MQTTnet 5.2.0.1603 (MIT, NuGet) im Kern. Trendo **abonniert nur** und sendet keine Befehle an Geräte.

## Umsetzung
- `TwinMqtt` (Kern):
  - Themen-Filter nach MQTT-Regeln (`+` für eine Ebene, `#` für den Rest).
  - Messwert als Zahl (Komma oder Punkt) oder als JSON-Feld mit Pfad (z. B. Tasmota `ENERGY.Power`).
  - Verbindung zum Broker, Abonnieren, Speichern im digitalen Zwilling. Gespeichert wird im UI-Thread, weil die Oberfläche den Zwilling dort liest.
- Studio „Geräte überwachen“, Bereich „Live über MQTT“:
  - Broker, Port, Benutzer und Verknüpfungen je Zeile `Thema;Anlage;Sensor[;JSON-Feld]`.
  - Fehlende Sensoren werden angelegt.
  - Der Broker wird gemerkt, das **Passwort nie**.
  - Start und Stopp stehen im Audit-Log.

## Echter Lauf (Test `TwinMqttTests.Real_broker`)
- Echter MQTT-Broker (MQTTnet.Server, nur im Testprojekt) auf 127.0.0.1 mit freiem Port.
- Ein „Gerät“ verbindet sich und sendet:
  - `haus/keller/temp` = `26,5`
  - `tele/plug1/SENSOR` = `{"ENERGY":{"Power":53.2,"Today":0.4}}`
  - `haus/garten/temp` = `12` (nicht verknüpft)
- Ergebnis im Zwilling: Keller 26,5 °C mit Ampel **Warnung** (Grenze 25), Steckdose 53,2 W. Die Garten-Nachricht wird nicht gespeichert. Genau 2 Werte.
- Oberfläche (`TwinMqttUi`): Ein Broker-Port ohne Zuhörer ergibt eine verständliche Meldung im Hintergrund, Trendo bleibt bedienbar. Das Passwort steht nicht in `mqtt.json`.

## Tests
- Kern: 7 MQTT-Tests (Filter, Werte, Ablehnungen, Verteilen, ungültige Verbindung, echter Broker), zusammen mit den übrigen Zwilling-Tests 10 grün.
- Oberfläche: 4 grün.

## Nicht geprüft
- Echte Geräte (Shelly, Tasmota) und ein externer Broker im Heimnetz.
- TLS-Verbindungen. MQTT läuft unverschlüsselt, wie es im Heimnetz üblich ist; TLS ist noch nicht eingebaut.
