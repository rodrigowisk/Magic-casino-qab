import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    host: '0.0.0.0',
    port: 5173, // 💻 Porta de Desenvolvimento (Hot Reload)
    strictPort: true,
    
    // 🔗 PROXY: A Mágica Híbrida
    // Tudo que não for arquivo do Vue (.js, .css), ele joga para o Docker (8888)
    proxy: {
      '/gameHub': {
        target: 'http://localhost:8888', // Aponta para o NGINX do Docker
        changeOrigin: true,
        ws: true, // Habilita WebSocket para o SignalR funcionar
        secure: false
      },
      '/sportbook': {
        target: 'http://localhost:8888',
        changeOrigin: true,
        secure: false
      },
      '/core': {
        target: 'http://localhost:8888',
        changeOrigin: true,
        secure: false
      },
      '/slot': {
        target: 'http://localhost:8888',
        changeOrigin: true,
        secure: false
      }
    },
    // Configuração para Tunelamento (Cloudflare/Ngrok)
    allowedHosts: [
      'quebrandoabanca.bet',
      'www.quebrandoabanca.bet',
      'localhost'
    ],
    hmr: {
      // ⚠️ IMPORTANTE: Comentei esta linha para funcionar no LOCALHOST.
      // Se for rodar o túnel da Cloudflare novamente, descomente ela.
       clientPort: 443 
    }
  }
})