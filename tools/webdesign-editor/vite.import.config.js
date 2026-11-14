import { defineConfig } from "vite";
// Z8/K6: zweites Bündel nur für das Einlesen von HTML (GrapesJS-Kern, BSD-3-Clause). Klassisches Skript (IIFE) wie das Editor-Bündel,
// Ausgabe nach dist/assets/import.js; die Seite dazu (import.html) legt lizenzen.mjs nicht an, sie liegt fest im Programm.
export default defineConfig({
  base: "./",
  build: {
    outDir: "dist", emptyOutDir: false, assetsInlineLimit: 0, chunkSizeWarningLimit: 4000, modulePreload: false,
    lib: { entry: "src/import.js", name: "TrendoImport", formats: ["iife"], fileName: () => "assets/import.js" },
  },
});
