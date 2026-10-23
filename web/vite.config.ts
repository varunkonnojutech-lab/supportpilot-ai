import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The API is served by the SupportPilot backend on :8080 in local dev.
// In production the frontend is served from the same origin as the API,
// so all calls use the relative "/api" base.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "http://localhost:8080",
        changeOrigin: true,
      },
    },
  },
});
