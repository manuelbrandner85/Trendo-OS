# IFC-Ansicht für „Wohnung planen“

web-ifc 0.0.78 (ThatOpen, MPL-2.0, unverändert) liest IFC-Dateien, three.js 0.180 (MIT) zeichnet sie. Gebaut als ein klassisches Skript (IIFE), weil Trendo die Seite per `file://` lädt.
Die WASM-Datei von web-ifc ist im Bündel eingebettet (`?url&inline`). Die IFC-Datei schickt Trendo über die WebView-Brücke (`window.trendoIfc(base64, name)`).

Bauen (npm nur mit `--ignore-scripts`):

```
npm install --ignore-scripts
npx vite build
node lizenzen.mjs
```

Danach `dist/` nach `src/Trendo.App/Assets/ifc/` kopieren.
