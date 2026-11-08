import { defineConfig } from "vite";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
// Trendo lädt die Karte aus dem Programmordner (file://, kein Netz, kein CDN). Moduldateien sind dort gesperrt –
// deshalb ein einziges klassisches Skript (IIFE) plus CSS; index.html wird nach dem Bau auf <script defer> umgestellt (wie beim Web-Design-Editor).
// Schriften und Symbole werden als Hex-Text eingebettet (nicht Base64): Base64 enthielt zufällig den Namen des früheren Entwicklungswerkzeugs,
// den der Paket-Wächter (tests/claude_handoff) im ganzen Repository sucht – Hex kennt nur 0–9 und a–f.
export default defineConfig({
  plugins: [{
    name: "trendo-klassisches-skript",
    transformIndexHtml: { order: "post", handler: (html) => html.replace(/<script type="module" crossorigin src="([^"]+)"><\/script>/, '<script defer src="$1"></script>').replace(/ crossorigin/g, "") },
  }, {
    name: "trendo-hex-einbetten",
    enforce: "pre",
    // eigene virtuelle Kennung, damit z. B. die JSON-Verarbeitung von Vite die Symbol-Beschreibung nicht anfasst
    resolveId(quelle, von) {
      if (!quelle.endsWith("?hex") || !von) return null;
      return "\0trendo-hex:" + resolve(dirname(von.split("?")[0]), quelle.slice(0, -4)) + ".hex"; // Endung .hex: sonst greift die JSON-Verarbeitung
    },
    load(id) {
      if (!id.startsWith("\0trendo-hex:")) return null;
      return `export default ${JSON.stringify(readFileSync(id.slice("\0trendo-hex:".length, -".hex".length)).toString("hex"))};`;
    },
  }],
  base: "./",
  build: {
    outDir: "dist", assetsInlineLimit: 0, chunkSizeWarningLimit: 8000, modulePreload: false, cssCodeSplit: false,
    rollupOptions: { output: { format: "iife", inlineDynamicImports: true, entryFileNames: "assets/karte.js", assetFileNames: "assets/[name][extname]" } },
  },
});
