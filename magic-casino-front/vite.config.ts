import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    host: '0.0.0.0', // Permite acesso via rede (celular/outros PCs)
    port: 5173,
    strictPort: true,
    
    // 🛡️ Segurança para Túnneis (Cloudflare/Ngrok)
    allowedHosts: [
      'quebrandoabanca.bet',
      'www.quebrandoabanca.bet',
      'localhost',
      '.ngrok-free.app', // Adicionado por precaução caso use ngrok
      '.cloudflare.com'   // Adicionado por precaução
    ],

    // 🔗 PROXY: A Mágica Híbrida
    proxy: {
      // Regra para o SignalR (WebSocket)
      '/gameHub': {
        target: 'http://127.0.0.1:8888', // Usei IP direto para evitar delay de DNS
        changeOrigin: true,
        ws: true, // Essencial para WebSockets
        secure: false
      },
      // Regra para API /sportbook
      '/sportbook': {
        target: 'http://127.0.0.1:8888',
        changeOrigin: true,
        secure: false
      },
      // Regra para API /core (Login/Auth)
      '/core': {
        target: 'http://127.0.0.1:8888',
        changeOrigin: true,
        secure: false
      },
      // Regra para API /slot
      '/slot': {
        target: 'http://127.0.0.1:8888',
        changeOrigin: true,
        secure: false
      }
    },

    hmr: {
      // 👇 Mantenha comentado para LOCALHOST.
      // 👇 Descomente APENAS se estiver acessando via https://quebrandoabanca.bet
      //clientPort: 443 
    }
  }
})