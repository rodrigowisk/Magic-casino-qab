import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    // Permite acesso externo
    host: '0.0.0.0', 
    port: 5173,
    allowedHosts: [
      'quebrandoabanca.bet',
      'www.quebrandoabanca.bet',
      'localhost'
    ],
    // 🔥 CORREÇÃO IMPORTANTE PARA O TÚNEL 🔥
    hmr: {
      // Quando estiver no domínio, o cliente (navegador) deve tentar conectar na porta 443 (HTTPS)
      // e não na 5173 direto, pois o túnel cuida disso.
      clientPort: 443 
    }
  }
})