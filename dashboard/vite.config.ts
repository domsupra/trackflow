import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The built bundle is served by the ASP.NET Core API itself (see src/TrackFlow.Api
// Program.cs: UseDefaultFiles + UseStaticFiles), so production output lands in
// the API's wwwroot. In dev, the Vite server proxies /v1 and /health to the API
// (dotnet run) so the page works against a live backend with no CORS at all.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: "../src/TrackFlow.Api/wwwroot",
    emptyOutDir: true,
  },
  server: {
    proxy: {
      "/v1": "http://localhost:5011",
      "/health": "http://localhost:5011",
    },
  },
});
