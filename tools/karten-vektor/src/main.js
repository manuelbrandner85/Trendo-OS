// Trendo – Vektorkarten aus PMTiles (Protomaps-Basiskarte) im Karten-Studio.
// Läuft unter file:// ohne Netz: Kacheln liefert Trendo (eigener PMTiles-Leser in C#) über die Brücke,
// Schriften (Noto, OFL) und Symbole (MIT) stecken im Bündel. Nichts wird aus dem Internet geladen.
import * as maplibregl from "maplibre-gl";
import "maplibre-gl/dist/maplibre-gl.css";
import { layers, namedFlavor } from "@protomaps/basemaps";
import "./trendo.css";
import workerCode from "maplibre-gl/dist/maplibre-gl-worker.mjs?raw";

// MapLibre 6 lädt seinen Worker als eigene Datei – unter file:// nicht erreichbar. Darum steckt der Worker als Text im Bündel und startet über eine Blob-Adresse.
maplibregl.setWorkerUrl(URL.createObjectURL(new Blob([workerCode], { type: "text/javascript" })));

const glyphs = import.meta.glob("./assets/glyphs/*/*.pbf", { query: "?url&inline", import: "default", eager: true });
const sprites = import.meta.glob("./assets/sprites/*", { query: "?url&inline", import: "default", eager: true });
const hinweis = document.getElementById("hinweis");
const offen = new Map(); let naechste = 1;

function zuBytes(dataUrl) { const b = atob(dataUrl.slice(dataUrl.indexOf(",") + 1)); const u = new Uint8Array(b.length); for (let i = 0; i < b.length; i++) u[i] = b.charCodeAt(i); return u; }
function senden(nachricht) { if (window.invokeCSharpAction) window.invokeCSharpAction(JSON.stringify(nachricht)); else if (window.chrome?.webview) window.chrome.webview.postMessage(JSON.stringify(nachricht)); }

// Trendo antwortet mit window.trendoKachel(id, base64 | "")
window.trendoKachel = (id, b64) => { const p = offen.get(id); if (!p) return; offen.delete(id); p(b64 ? zuBytes("x," + b64) : null); };

maplibregl.addProtocol("trendo-kachel", (params) => new Promise((ok) => {
  const m = /^trendo-kachel:\/\/(\d+)\/(\d+)\/(\d+)$/.exec(params.url); if (!m) { ok({ data: new Uint8Array(0) }); return; }
  const id = naechste++; offen.set(id, (bytes) => ok({ data: bytes ?? new Uint8Array(0) }));
  senden({ art: "kachel", id, z: +m[1], x: +m[2], y: +m[3] });
}));
maplibregl.addProtocol("trendo-schrift", async (params) => {
  const m = /^trendo-schrift:\/\/([^/]+)\/(\d+-\d+)\.pbf$/.exec(params.url);
  const stapel = m ? decodeURIComponent(m[1]).split(",")[0] : ""; const url = glyphs[`./assets/glyphs/${stapel}/${m?.[2]}.pbf`];
  return { data: url ? zuBytes(url) : new Uint8Array(0) };
});
maplibregl.addProtocol("trendo-symbol", async (params) => {
  const datei = params.url.replace(/^trendo-symbol:\/\//, "").replace(/[?#].*$/, "").replace("/@", "@"); // MapLibre fragt „light/@2x.json“ an
  const url = sprites[`./assets/sprites/${datei}`];
  if (!url) return { data: datei.endsWith(".json") ? {} : new Uint8Array(0) };
  return { data: datei.endsWith(".json") ? JSON.parse(new TextDecoder().decode(zuBytes(url))) : zuBytes(url) };
});

// Trendo ruft window.trendoKarte({ name, minzoom, maxzoom, bounds: [w, s, o, n] }) auf, sobald das Paket offen ist
window.trendoKarte = (info) => {
  const karte = new maplibregl.Map({
    container: "karte", attributionControl: { compact: true },
    style: {
      version: 8, glyphs: "trendo-schrift://{fontstack}/{range}.pbf", sprite: "trendo-symbol://light",
      sources: { protomaps: { type: "vector", tiles: ["trendo-kachel://{z}/{x}/{y}"], minzoom: info.minzoom, maxzoom: info.maxzoom,
        attribution: "© OpenStreetMap-Mitwirkende · Protomaps" } },
      layers: layers("protomaps", namedFlavor("light"), { lang: "de" }),
    },
    bounds: info.bounds, maxBounds: undefined,
  });
  karte.addControl(new maplibregl.NavigationControl({ visualizePitch: true }), "top-right");
  karte.addControl(new maplibregl.ScaleControl({ unit: "metric" }), "bottom-right");
  karte.on("load", () => { hinweis.textContent = ""; senden({ art: "geladen" }); });
  karte.on("error", (e) => { hinweis.textContent = "Fehler: " + (e.error?.message || e); senden({ art: "fehler", text: String(e.error?.message || e) }); });
  window.trendoKarteObjekt = karte;
};
senden({ art: "bereit" });
