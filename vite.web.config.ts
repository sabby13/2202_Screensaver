import { resolve } from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

/**
 * Standalone web build of the GlassButterfly renderer for the native WebView2
 * host (Phase 6). It mirrors the renderer section of electron.vite.config.ts
 * but produces a plain static bundle in dist/web/ with relative asset paths, so
 * a WebView2 virtual-host mapping can serve it. This does NOT replace the
 * Electron dev workflow (`npm run dev`) — that continues to work unchanged.
 */
export default defineConfig({
  root: 'src/renderer',
  base: './',
  assetsInclude: ['**/*.glb'],
  resolve: {
    alias: { '@shared': resolve('src/shared') }
  },
  plugins: [react()],
  build: {
    outDir: resolve('dist/web'),
    emptyOutDir: true,
    rollupOptions: {
      input: {
        index: resolve('src/renderer/index.html'),
        settings: resolve('src/renderer/settings.html')
      }
    }
  }
})
