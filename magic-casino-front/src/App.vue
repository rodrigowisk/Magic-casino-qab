<script setup lang="ts">
import { onMounted, computed, watch } from 'vue';
import { useAuthStore } from './stores/useAuthStore';
import { useRoute } from 'vue-router'; 
import * as signalR from "@microsoft/signalr"; 
import Swal from 'sweetalert2'; 

// ✅ 1. Importe os Layouts
import MainLayout from './layouts/MainLayout.vue';       
import AdminLayout from './layouts/AdminLayout.vue';     
import TournamentLayout from './layouts/TournamentLayout.vue';

const authStore = useAuthStore();
const route = useRoute();

// Variável para controlar a conexão e evitar duplicações
let connection: signalR.HubConnection | null = null;

// ✅ 2. Mapeamento de nomes para componentes
const layouts: Record<string, any> = {
  default: MainLayout,
  admin: AdminLayout,
  tournament: TournamentLayout,
};

// ✅ 3. Lógica para decidir qual layout usar
const currentLayout = computed(() => {
  const layoutName = (route.meta.layout as string) || 'default';
  return layouts[layoutName] || MainLayout;
});

// ✅ NOVA FUNÇÃO: Gerencia a conexão de segurança em tempo real
const startSecurityConnection = async () => {
  // Pega o token bruto para garantir
  const rawToken = localStorage.getItem('token');
  if (!rawToken) return;

  // Se já existir uma conexão ativa, encerra antes de criar nova
  if (connection) {
      await connection.stop();
  }

  // Remove aspas se houver (comum ao salvar strings JSON)
  const token = rawToken.replace(/['"]+/g, '');

  // A URL deve bater com o que definimos no Program.cs do Backend (/hubs/user)
  // O prefixo '/core' depende da sua configuração de Proxy/Nginx
  const hubUrl = '/core/hubs/user'; 

  connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
          accessTokenFactory: () => token // Envia o token limpo
      })
      .withAutomaticReconnect() // Tenta reconectar se a internet piscar
      .configureLogging(signalR.LogLevel.Warning)
      .build();

  // 👂 ESCUTA O EVENTO DE LOGOUT FORÇADO (Enviado pelo UsersController no Login)
  connection.on("ForceLogout", async (message) => {
      console.warn("⛔ SESSÃO DERRUBADA PELO SERVIDOR:", message);
      
      // 1. 🔥 LIMPEZA CRÍTICA IMEDIATA (Antes de qualquer UI)
      localStorage.removeItem('token');
      localStorage.removeItem('user'); 
      localStorage.removeItem('user_data');
      if (authStore.logout) authStore.logout();

      // 2. Para a conexão atual
      if (connection) await connection.stop();

      // 3. Exibe o alerta visual
      const alertMsg = message || 'Sua conta foi conectada em outro dispositivo.';
      
      if (typeof Swal !== 'undefined') {
          await Swal.fire({
              icon: 'error',
              title: 'Conectado em outro local',
              text: alertMsg,
              confirmButtonText: 'OK',
              confirmButtonColor: '#d33',
              background: '#1e293b',
              color: '#fff',
              allowOutsideClick: false,
              allowEscapeKey: false
          });
      } else {
          alert(alertMsg);
      }

      // 4. 🔥 HARD RELOAD (Melhor que router.push)
      window.location.href = '/'; 
  });

  try {
      await connection.start();
      console.log("🔒 Conectado ao UserHub: Segurança Ativa.");
  } catch (err: any) {
      console.error("Erro ao conectar ao UserHub:", err);

      // ✅ PROTEÇÃO EXTRA:
      // Se o erro for 401 (Unauthorized), significa que o token local já é inválido
      if (err.toString().includes("401") || err.toString().includes("Unauthorized")) {
          console.warn("Token inválido ou expirado detectado no boot. Deslogando...");
          localStorage.removeItem('token');
          window.location.href = '/';
      }
  }
};

// ✅ WATCHER PARA LOGIN IMEDIATO (CORREÇÃO DO BUG DO F5)
// Assim que o token muda (usuário loga), iniciamos o SignalR sem precisar recarregar a página
watch(() => authStore.token, async (newToken) => {
    if (newToken) {
        console.log("Login detectado! Iniciando segurança...");
        await authStore.fetchBalance(); // Garante saldo atualizado
        await startSecurityConnection(); // Conecta no Hub
    } else {
        // Se o token sumiu (logout voluntário), encerra a conexão para economizar recursos
        if (connection) await connection.stop();
    }
});

// ✅ MANTIDO: Lógica para quando o usuário dá F5 e já estava logado
onMounted(async () => {
  if (authStore.token) {
    await authStore.fetchBalance();
    await startSecurityConnection();
  }
});
</script>

<template>
  <component :is="currentLayout" />
</template>

<style>
/* Estilos globais básicos */
body {
  background-color: #121212;
  margin: 0;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
}
</style>