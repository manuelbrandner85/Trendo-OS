# Vektorkarten-Seite für das Karten-Studio

MapLibre GL JS 6.17.0 (BSD-3) mit dem Kartenstil `@protomaps/basemaps` 5.7.2 (BSD-3). Gebaut als ein klassisches Skript (IIFE), weil Trendo die Seite per `file://` lädt.

- Kacheln: `trendo-kachel://z/x/y` geht über die WebView-Brücke an Trendo (`VectorMap`, eigener PMTiles-Leser).
- Schriften (Noto, OFL) und Symbole (MIT) stammen aus `protomaps/basemaps-assets` und liegen in `src/assets`. Sie werden ins Bündel eingebettet, es wird nichts aus dem Netz geladen.

Bauen (npm nur mit `--ignore-scripts`):

```
npm install --ignore-scripts
npx vite build
node lizenzen.mjs
```

Danach `dist/` nach `src/Trendo.App/Assets/karten/` kopieren.
