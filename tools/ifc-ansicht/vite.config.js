import { defineConfig } from "vite";
// Trendo lädt die IFC-Ansicht aus dem Programmordner (file://, kein Netz, kein CDN). Moduldateien sind dort gesperrt –
// deshalb ein einziges klassisches Skript (IIFE) plus CSS; index.html wird nach dem Bau auf <script defer> umgestellt (wie beim Web-Design-Editor).
export default defineConfig({
  plugins: [{
    name: "trendo-klassisches-skript",
    transformIndexHtml: { order: "post", handler: (html) => html.replace(/<script type="module" crossorigin src="([^"]+)"><\/script>/, '<script defer src="$1"></script>').replace(/ crossorigin/g, "") },
  }],
  base: "./",
  build: {
    outDir: "dist", assetsInlineLimit: 0, chunkSizeWarningLimit: 8000, modulePreload: false, cssCodeSplit: false,
    rollupOptions: { output: { format: "iife", inlineDynamicImports: true, entryFileNames: "assets/ifc.js", assetFileNames: "assets/[name][extname]" } },
  },
});
