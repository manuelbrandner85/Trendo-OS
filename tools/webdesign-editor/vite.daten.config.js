import { defineConfig } from "vite";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);

// K7: drittes Bündel nur für Seiten mit Daten-Bausteinen (SQLite über sql.js, MIT). Das WebAssembly wird als Text eingebettet,
// damit die Seite auch direkt aus dem Ordner (file://) und ohne Netz läuft.
const wasmAlsText = {
  name: "trendo-wasm-als-text",
  resolveId(id) { return id === "virtual:sql-wasm" ? "\0virtual:sql-wasm" : null; },
  load(id) {
    if (id !== "\0virtual:sql-wasm") return null;
    const bytes = readFileSync(require.resolve("sql.js/dist/sql-wasm.wasm"));
    return `export default ${JSON.stringify("data:application/wasm;base64," + bytes.toString("base64"))};`;
  },
};

export default defineConfig({
  base: "./",
  plugins: [wasmAlsText],
  build: {
    outDir: "dist", emptyOutDir: false, assetsInlineLimit: 0, chunkSizeWarningLimit: 4000, modulePreload: false,
    lib: { entry: "src/daten.js", name: "TrendoDatenBuendel", formats: ["iife"], fileName: () => "assets/daten.js" },
  },
});
