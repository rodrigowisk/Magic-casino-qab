<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue';
import { useRoute } from 'vue-router';
import Header from '../components/Header.vue'; 
import SidebarTournament from '../components/Tournament/SidebarTournament.vue'; 

const route = useRoute(); 

// ✅ LÓGICA DE SCROLL DINÂMICO
// Define quais páginas devem ter o layout fixo (sem scroll na janela principal)
// para que o Carrossel do Wrapper fique fixo.
const isFixedLayout = computed(() => {
    const name = route.name as string;
    return [
        'TournamentPlay', 
        'TournamentLive', 
        'TournamentRanking', 
        'TournamentMyBets'
    ].includes(name);
});

// ✅ LÓGICA PARA IDENTIFICAR O LOBBY
const isLobby = computed(() => route.name === 'TournamentLobby');

// --- ESTADOS DE LAYOUT ---
const isSidebarOpen = ref(true);
const isMobile = ref(false); 

const toggleSidebar = () => {
    isSidebarOpen.value = !isSidebarOpen.value;
};

// --- RESPONSIVIDADE E LÓGICA DE ABERTURA ---

// Função centralizada para decidir o estado da sidebar
const updateSidebarState = () => {
    if (typeof window === 'undefined') return;
    
    const width = window.innerWidth;
    isMobile.value = width < 768;

    if (isMobile.value) {
        // Mobile: Sempre começa fechado para não tapar a tela
        isSidebarOpen.value = false;
    } else {
        // Desktop: 
        // Se estiver no Lobby -> Fecha (para dar espaço aos cards/grid grande)
        // Se estiver DENTRO de um torneio -> ABRE (para navegação lateral)
        isSidebarOpen.value = !isLobby.value;
    }
};

const handleResize = () => {
    updateSidebarState();
};

// ✅ FIX: Observa mudanças na rota (Lobby <-> Torneio)
// Isso garante que ao clicar em um torneio no Lobby, a sidebar abra automaticamente.
watch(isLobby, () => {
    updateSidebarState();
});

// Inicialização
onMounted(() => {
    updateSidebarState();
    window.addEventListener('resize', handleResize);
});

onUnmounted(() => {
    window.removeEventListener('resize', handleResize);
});
</script>

<template>
  <div class="h-screen bg-[#0f172a] text-slate-300 font-sans flex flex-col overflow-hidden">
    
    <Header @toggle-sidebar="toggleSidebar" />

    <div class="flex flex-1 overflow-hidden relative">
      
      <SidebarTournament 
          v-if="!isLobby"
          v-show="isSidebarOpen" 
          class="w-64 flex-shrink-0 transition-all duration-300 border-r border-white/5 bg-[#0f172a] z-40" 
          :class="isMobile ? 'absolute h-full shadow-2xl' : 'relative'"
      />
      
      <div 
        v-if="!isLobby && isMobile && isSidebarOpen" 
        @click="isSidebarOpen = false"
        class="absolute inset-0 bg-black/50 z-30 backdrop-blur-sm"
      ></div>

      <main class="flex-1 bg-[#0f172a] relative transition-all duration-300 !p-0 flex flex-col h-full w-full"
            :class="isFixedLayout ? 'overflow-hidden' : 'overflow-y-auto custom-scrollbar'">
        
        <div class="w-full flex flex-col flex-1 relative"
             :class="isFixedLayout ? 'h-full overflow-hidden' : 'min-h-full'">
            
            <router-view v-slot="{ Component }">
                <transition name="fade" mode="out-in">
                    <component :is="Component" :class="isFixedLayout ? 'h-full w-full flex-1' : 'w-full flex-1'" />
                </transition>
            </router-view>
        </div>
        
      </main>

    </div>
  </div>
</template>

<style>
@import url('https://fonts.googleapis.com/css2?family=Montserrat:wght@400;700;900&display=swap');

.custom-scrollbar::-webkit-scrollbar {
  width: 6px;
  height: 6px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: #0f172a; 
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #334155; 
  border-radius: 3px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: #475569; 
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>