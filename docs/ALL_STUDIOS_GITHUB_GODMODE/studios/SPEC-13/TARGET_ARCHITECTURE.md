# Zielarchitektur SPEC-13
Trendo-Studio (Avalonia) → vorhandene Programm-Ansicht → Adapter je Auswahl (Prozess/CLI oder WebView), Ergebnisse als Dateien im Projekt, Freigabe vor Installation (GRUNDSATZ-01), Audit-Log.
- protomaps/PMTiles: Offline-Vektorkarten aus einer einzigen Datei (BSD-3), lesbar ohne Server
- maplibre/maplibre-gl-js: Vektorkarten-Darstellung im WebView (BSD-3), passt zu PMTiles
