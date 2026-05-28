import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5199,
    strictPort: true,
    proxy: {
      '/api': { target: 'http://localhost:5050', changeOrigin: true, ws: true },
      '/feeds': { target: 'http://localhost:5050', changeOrigin: true },
      '/health': { target: 'http://localhost:5050', changeOrigin: true },
      '/.well-known': { target: 'http://localhost:5050', changeOrigin: true },
    },
  },
});
