<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { Trophy, Dices, Settings, LayoutDashboard, LogOut } from 'lucide-vue-next';

const route = useRoute();

// 1. Descobre qual aba está ativa olhando a URL
const activeTab = computed(() => {
  if (route.path.includes('/admin/tournaments')) return 'tournament';
  if (route.path.includes('/admin/sportbook')) return 'sportbook';
  if (route.path.includes('/admin/casino')) return 'casino';
  if (route.path.includes('/admin/config')) return 'config';
  return 'tournament'; // Padrão
});

// 2. Define os menus laterais para cada aba
const sidebarMenus = computed(() => {
  switch (activeTab.value) {
    case 'tournament':
      return [
        { label: 'Listar Torneios', to: '/admin/tournaments' },
        { label: 'Criar Novo', to: '/admin/tournaments/create' },
        { label: 'Participantes', to: '#', disabled: true }
      ];
    case 'sportbook':
      return [
        // 🔥 CORREÇÃO: O link agora aponta para a rota correta
        { label: 'Configurar Esportes', to: '/admin/sportbook' },
        { label: 'Gerenciar Odds', to: '#', disabled: true },
        { label: 'Resultados', to: '#', disabled: true }
      ];
    case 'casino':
      return [
        { label: 'Meus Jogos', to: '/admin/casino' },
        { label: 'Provedores', to: '#', disabled: true }
      ];
    case 'config':
      return [
        { label: 'Geral', to: '/admin/config' },
        { label: 'Financeiro', to: '#', disabled: true },
        { label: 'Logs do Sistema', to: '#', disabled: true }
      ];
    default:
      return [];
  }
});
</script>

<template>
  <div class="admin-layout">
    
    <div class="flex flex-col flex-1 h-full overflow-hidden">
      
      <header class="admin-topbar">
        <div class="logo-area">🛡️ PAINEL MASTER</div>
        
        <nav class="top-nav">
          <router-link to="/admin/tournaments" class="nav-item" :class="{ active: activeTab === 'tournament' }">
            <Trophy class="w-4 h-4" /> Torneios
          </router-link>
          
          <router-link to="/admin/sportbook" class="nav-item" :class="{ active: activeTab === 'sportbook' }">
            <LayoutDashboard class="w-4 h-4" /> Sportbook
          </router-link>
          
          <router-link to="/admin/casino" class="nav-item" :class="{ active: activeTab === 'casino' }">
            <Dices class="w-4 h-4" /> Cassino
          </router-link>
          
          <router-link to="/admin/config" class="nav-item" :class="{ active: activeTab === 'config' }">
            <Settings class="w-4 h-4" /> Config
          </router-link>
        </nav>

        <div class="user-area">Admin</div>
      </header>

      <div class="flex flex-1 overflow-hidden">
        
        <aside class="sidebar">
          <div class="menu-title">MÓDULO {{ activeTab.toUpperCase() }}</div>
          
          <nav class="side-menu">
            <router-link 
              v-for="item in sidebarMenus" 
              :key="item.label" 
              :to="item.to"
              class="side-item"
              :class="{ 'disabled': item.disabled }"
            >
              {{ item.label }}
            </router-link>
          </nav>

          <div class="mt-auto p-4 border-t border-gray-700">
            <router-link to="/" class="side-item logout">
              <LogOut class="w-4 h-4" /> Sair do Painel
            </router-link>
          </div>
        </aside>

        <main class="content-area bg-gray-100 text-gray-900">
          <router-view v-slot="{ Component }">
            <transition name="fade" mode="out-in">
              <component :is="Component" />
            </transition>
          </router-view>
        </main>

      </div>
    </div>
  </div>
</template>

<style scoped>
.admin-layout { display: flex; height: 100vh; background-color: #121214; color: #e1e1e6; }

/* --- TOPBAR --- */
.admin-topbar { 
  height: 60px; background-color: #202024; border-bottom: 1px solid #323238; 
  display: flex; align-items: center; justify-content: space-between; padding: 0 20px;
  flex-shrink: 0;
}
.logo-area { font-weight: 900; color: #ffd700; letter-spacing: 1px; }

.top-nav { 
  display: flex; gap: 5px; background: #121214; padding: 5px; border-radius: 8px; 
}

.nav-item { 
  display: flex; align-items: center; gap: 8px; padding: 8px 16px; 
  color: #a8a8b3; text-decoration: none; font-size: 0.9rem; font-weight: 600; 
  border-radius: 6px; transition: 0.2s;
}
.nav-item:hover { color: #fff; background: rgba(255,255,255,0.05); }
.nav-item.active { background-color: #ffd700; color: #121214; }

/* --- SIDEBAR --- */
.sidebar { 
  width: 240px; background-color: #1c1c1e; border-right: 1px solid #323238; 
  display: flex; flex-direction: column; flex-shrink: 0;
}
.menu-title { 
  padding: 20px; font-size: 0.75rem; font-weight: bold; color: #7c7c8a; 
  text-transform: uppercase; letter-spacing: 1px;
}
.side-menu { display: flex; flex-direction: column; padding: 0 10px; gap: 5px; }
.side-item { 
  padding: 12px 15px; color: #e1e1e6; text-decoration: none; font-size: 0.95rem; 
  border-radius: 4px; transition: 0.2s; display: flex; align-items: center; gap: 10px;
}
.side-item:hover:not(.disabled) { background-color: #29292e; }
.side-item.router-link-active { background-color: rgba(255, 215, 0, 0.1); color: #ffd700; border-left: 3px solid #ffd700; }
.side-item.logout { color: #f75a68; }
.side-item.logout:hover { background: rgba(247, 90, 104, 0.1); }
.side-item.disabled { opacity: 0.4; cursor: not-allowed; pointer-events: none; }

/* --- CONTENT --- */
.content-area { flex: 1; overflow-y: auto; padding: 0; position: relative; }

/* Transição Suave */
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>