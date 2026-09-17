import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

const dirname = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  plugins: [react()],
  resolve: {
    // Mirrors tsconfig.json's "@/*" -> "./src/*" alias - tsc/Next.js resolve it from
    // tsconfig automatically, but Vite's own resolver needs it declared separately.
    alias: {
      '@': path.resolve(dirname, 'src'),
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    // E2E is @playwright/test (ADR-031) and has its own runner; keep it out of Vitest.
    include: ['__tests__/**/*.test.{ts,tsx}'],
    exclude: ['node_modules/**', 'e2e/**', '.next/**'],
  },
});
