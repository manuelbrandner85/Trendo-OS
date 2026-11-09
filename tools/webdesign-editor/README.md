# Trendo Web Design Studio – Editor-Quellen

Quellcode des visuellen Seiten-Editors im Studio „Webseite bauen“ (CORE-07). Das gebaute Bündel liegt fertig in
`src/Trendo.App/Assets/webdesign/` und wird mit Trendo ausgeliefert – zur Laufzeit lädt Trendo nichts aus dem Netz.

- Editor-Kern: Puck (`@puckeditor/core` 0.24.0, MIT), 3D: React Three Fiber 9.8.1 + three.js (MIT), React 19 (MIT).
- Bewegungen: Web Animations API des Browsers (kein Fremdpaket); Theatre.js folgt erst mit dem Zeitleisten-Modul (nur `@theatre/core`, Apache-2.0 – der Theatre-Editor ist AGPL und wird nicht übernommen).
- Oberfläche komplett deutsch (`src/de.js`, Puck-Wörterbuch).
- Brücke zu Trendo: `invokeCSharpAction(JSON)` (Avalonia NativeWebView) für „bereit“, „speichern“, „entwurf“ (ungespeicherte Änderung), „auswahl“ (Ids aus „Auswählen“) und „aktion“ (Plan aus Editor-Aktionen, z. B. Text direkt geändert). Trendo ruft `window.trendoLoad(json)` und `window.trendoVorschau(json|null)` (KI-Vorschlag, noch nicht übernommen).
- Prüfung des Bündels in echtem Chrome: `node pruefe-auswahl.mjs <chrome> <Ordner des Bündels>`.
- Unter `file://` zeichnet der Editor ohne iframe (Container-Abfragen statt Media-Queries für Handy/Tablet).
- Fertige Seite (Export): `window.TRENDO_SEITE` aus `seite.js` → reiner Anzeigemodus.

## Neu bauen

```bash
npm install --ignore-scripts
npx vite build
node lizenzen.mjs
```

Danach `dist/index.html`, `dist/LIZENZEN.txt`, `dist/LIZENZEN.md` und `dist/assets/*` nach `src/Trendo.App/Assets/webdesign/` kopieren.
`--ignore-scripts`: Installationsskripte fremder Pakete werden nie ausgeführt. `lizenzen.mjs` schreibt alle gebündelten Pakete mit vollem Lizenztext; es bricht nicht ab, meldet aber jede nicht freizügige Lizenz.
