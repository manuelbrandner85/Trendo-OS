import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
// Trendo lädt den Editor aus dem Programmordner (file://, kein Netz, kein CDN). Moduldateien sind dort gesperrt –
// deshalb ein einziges klassisches Skript (IIFE) plus CSS; index.html wird nach dem Bau auf <script defer> umgestellt.
export default defineConfig({
  plugins: [react(), {
    name: "trendo-klassisches-skript",
    transformIndexHtml: { order: "post", handler: (html) => html.replace(/<script type="module" crossorigin src="([^"]+)"><\/script>/, '<script defer src="$1"></script>').replace(/ crossorigin/g, "") },
  }],
  base: "./",
  build: {
    outDir: "dist", assetsInlineLimit: 0, chunkSizeWarningLimit: 4000, modulePreload: false, cssCodeSplit: false,
    rollupOptions: { output: { format: "iife", inlineDynamicImports: true, entryFileNames: "assets/editor.js", assetFileNames: "assets/[name][extname]" } },
  },
});
