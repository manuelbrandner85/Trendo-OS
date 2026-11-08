# Zielarchitektur SPEC-10
Trendo-Studio (Avalonia) → vorhandene Programm-Ansicht → Adapter je Auswahl (Prozess/CLI oder WebView), Ergebnisse als Dateien im Projekt, Freigabe vor Installation (GRUNDSATZ-01), Audit-Log.
- dotnet/MQTTnet: .NET-Bibliothek (MIT): Live-Messwerte über MQTT (Standard bei Sensoren, Shelly, Tasmota, Home Assistant) direkt in den Zwilling – nur lesen, nur nach Freigabe
