import {defineConfig, type Plugin} from 'vite'
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
            '@': path.resolve(__dirname, './src'),
            '@shared': path.resolve(__dirname, './src/shared'),
            '@features': path.resolve(__dirname, './src/features'),
            '@tools': path.resolve(__dirname, './src/tools'),
            '@stores': path.resolve(__dirname, './src/stores'),
        },
    },
    server: {
        port: 5174,
        strictPort: true,
        proxy: {
            '/api': {
                target: 'http://localhost:5063',
                changeOrigin: true,
            },
        },
    },
})
