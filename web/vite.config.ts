import { defineConfig } from 'vite';
import preact from '@preact/preset-vite';

// In development, /api is proxied to the public glei API (no CORS needed).
// Override with GLEI_API=http://localhost:5000 npm run dev to use a local API.
const api = process.env.GLEI_API ?? 'https://glei-739185299212.europe-west1.run.app';

export default defineConfig({
  plugins: [preact()],
  server: {
    proxy: { '/api': { target: api, changeOrigin: true } },
  },
});
