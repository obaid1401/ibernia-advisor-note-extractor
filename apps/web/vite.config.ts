import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// VITE_API_BASE_URL is read at build time from the environment or apps/web/.env*.
// It is required but deliberately not checked here: a missing value is reported clearly at runtime
// (see src/config.ts) instead of failing the build.
export default defineConfig({
  plugins: [react()],
});
