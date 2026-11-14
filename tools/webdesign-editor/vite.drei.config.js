import { defineConfig } from "vite";
// Z3: Bündel für 3D-Modelle. „three“ zeigt hier auf die WebGPU-Fassung von three.js (mit eingebautem WebGL-2-Rückfall);
// Lader und Steuerung aus three/addons nutzen so dieselbe Fassung. Aufruf: npx vite build -c vite.drei.config.js
// Für Splats (Spark braucht den WebGL-Renderer): TRENDO_BUNDLE=splat npx vite build -c vite.drei.config.js
const splat = process.env.TRENDO_BUNDLE === "splat";
export default defineConfig({
  base: "./",
  resolve: { alias: splat ? [] : [{ find: /^three$/, replacement: "three/webgpu" }] },
  build: {
    outDir: "dist", emptyOutDir: false, assetsInlineLimit: 100000000, chunkSizeWarningLimit: 6000, modulePreload: false, target: "es2022",
    lib: { entry: splat ? "src/splat.js" : "src/drei.js", name: splat ? "TrendoSplatBuendel" : "TrendoDreiBuendel", formats: ["iife"], fileName: () => (splat ? "assets/splat.js" : "assets/drei.js") },
  },
});
