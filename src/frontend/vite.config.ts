import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'
import fs from 'fs'

// 處理 OAuth callback 路徑的 plugin
function oauthCallbackPlugin(): Plugin {
  return {
    name: 'oauth-callback-handler',
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        // 攔截 /auth/callback 路徑，提供靜態 HTML
        if (req.url?.startsWith('/auth/callback')) {
          const callbackHtml = fs.readFileSync(
            path.resolve(__dirname, 'public/auth/callback/index.html'),
            'utf-8'
          )
          res.setHeader('Content-Type', 'text/html')
          res.end(callbackHtml)
          return
        }
        next()
      })
    },
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [oauthCallbackPlugin(), react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, '.'),
    },
  },
  server: {
    port: 5174,
    strictPort: true,
  },
})
