import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// Build stamp: the git SHA the deploy passes as VITE_APP_SHA, else a per-build fallback for local
// builds. The running bundle carries it (__APP_VERSION__) and version.json advertises it, so an
// open tab can detect that a newer build has shipped. See src/lib/version.ts.
const APP_VERSION = process.env.VITE_APP_SHA?.trim() || `dev-${Date.now()}`;

// Emit /version.json alongside the bundle so a long-lived tab can poll for a new release.
function emitVersionJson(): Plugin {
  return {
    name: "emit-version-json",
    generateBundle() {
      this.emitFile({
        type: "asset",
        fileName: "version.json",
        source: JSON.stringify({ version: APP_VERSION, builtAt: new Date().toISOString() }),
      });
    },
  };
}

// Served under /admin on a single origin behind Traefik in production.
export default defineConfig({
  base: "/admin/",
  define: {
    __APP_VERSION__: JSON.stringify(APP_VERSION),
  },
  plugins: [react(), tailwindcss(), emitVersionJson()],
  resolve: {
    alias: { "@": "/src" },
  },
  server: {
    port: 34100,
    strictPort: true,
    proxy: {
      // Standalone `npm run dev` proxies the API to the pinned Aspire API port.
      "/api": { target: "http://localhost:34110", changeOrigin: true },
      // Public branding (app name, color, logo bytes) lives at the origin root, not under /api.
      "/branding": { target: "http://localhost:34110", changeOrigin: true },
    },
  },
});
