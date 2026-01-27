<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { Menu, Search, Trophy, ChevronLeft } from 'lucide-vue-next';

import Sidebar from '../components/Sidebar.vue'; 
import AuthModal from '../components/AuthModal.vue'; 
import BetSlip from '../components/BetSlip.vue'; 
import WalletDropdown from '../components/WalletDropdown.vue'; // ✅ Menu de Saldo
import UserDropdown from '../components/UserDropdown.vue';     // ✅ Menu de Usuário

import { useBetStore } from '../stores/useBetStore'; 
import { useAuthStore } from '../stores/useAuthStore';
import { useConfigStore } from '../stores/useConfigStore'; 

const router = useRouter();
const route = useRoute();
const betStore = useBetStore(); 
const authStore = useAuthStore();
const configStore = useConfigStore(); 

const isSidebarOpen = ref(true);
const showAuthModal = ref(false);
const isBetSlipOpen = ref(betStore.count > 0);

// Detecta se é a página de histórico
const isHistoryPage = computed(() => route.path === '/minhas-apostas'); 
// Detecta se é a página Live
const isLivePage = computed(() => route.path === '/live');

const handleLoginSuccess = (data: any) => {
    authStore.setLogin(data.user || data, localStorage.getItem('token') || '');
    showAuthModal.value = false;
};

const toggleBetSlip = () => {
    isBetSlipOpen.value = !isBetSlipOpen.value;
};

watch(
  () => betStore.count,
  (newCount, oldCount) => {
    if (newCount > (oldCount || 0)) {
        isBetSlipOpen.value = true;
    }
  }
);

const checkScreenSize = () => {
    if (window.innerWidth < 768) {
        isSidebarOpen.value = false;
    } else {
        isSidebarOpen.value = true;
    }
};

onMounted(() => {
    checkScreenSize();
    window.addEventListener('resize', checkScreenSize);
});

onUnmounted(() => {
    window.removeEventListener('resize', checkScreenSize);
});
</script>

<template>
  <div class="h-screen bg-stake-dark text-stake-text font-sans flex flex-col overflow-hidden">
    
    <AuthModal v-if="showAuthModal" @close="showAuthModal = false" @login-success="handleLoginSuccess" />

    <header class="h-16 bg-stake-card flex items-center justify-between px-4 shadow-lg sticky top-0 z-50 flex-shrink-0 border-b border-white/5">
      <div class="flex items-center">
        <button @click="isSidebarOpen = !isSidebarOpen" class="hover:text-white transition-colors mr-2">
            <Menu class="w-6 h-6" />
        </button>
        
        <div 
            @click="router.push('/')" 
            class="w-56 flex justify-center cursor-pointer select-none hover:brightness-110 transition-all group"
            style="font-family: 'Montserrat', sans-serif;"
        >
            <img src="/logo.png" alt="Logo" class="h-12 md:h-14 object-contain" />
        </div>
      </div>
      
      <div class="hidden md:flex bg-stake-dark rounded-full px-4 py-2 w-96 border border-gray-700/50 hover:border-gray-500 transition-colors focus-within:border-stake-blue focus-within:ring-1 focus-within:ring-stake-blue">
        <Search class="w-4 h-4 text-stake-text mr-2 mt-1" />
        <input type="text" placeholder="Buscar jogos, ligas..." class="bg-transparent outline-none text-white text-sm w-full placeholder-gray-500">
      </div>

      <div class="flex items-center gap-3">
        
        <div v-if="authStore.user" class="flex items-center gap-3">
            
            <WalletDropdown :balance="authStore.user.balance || 0" />

            <UserDropdown />

        </div>
        
        <div v-else class="flex items-center gap-3">
            <button @click="showAuthModal = true" class="font-bold text-gray-300 text-sm hover:text-white transition-colors px-2">Entrar</button>
            <button @click="showAuthModal = true" class="bg-blue-600 hover:bg-blue-500 text-white px-6 py-2 rounded-md font-bold text-sm shadow-lg shadow-blue-900/50 transition-all transform hover:-translate-y-0.5">Cadastre-se</button>
        </div>

      </div>
    </header>

    <div class="flex flex-1 overflow-hidden relative">
      
      <Sidebar v-show="isSidebarOpen" class="w-64 flex-shrink-0 transition-all duration-300 border-r border-white/5" />

      <main class="flex-1 overflow-y-auto bg-stake-dark custom-scrollbar relative transition-all duration-300 !p-0">
        <div class="w-full h-full">
            <router-view />
        </div>
      </main>
      
      <div v-show="isBetSlipOpen" class="w-[320px] bg-[#1e293b] border-l border-gray-700 flex flex-col flex-shrink-0 transition-all duration-300 shadow-2xl z-40">
          <BetSlip @toggle="toggleBetSlip" :is-open="true" />
      </div>

      <button 
        v-if="!isBetSlipOpen"
        @click="toggleBetSlip"
        class="absolute bottom-2 right-2 z-50 bg-[#1e293b]/90 hover:bg-[#1e293b] text-white border border-yellow-500/30 shadow-2xl shadow-black/80 rounded-md px-4 py-2 flex items-center gap-2 transition-all hover:scale-105 group"
      >
        <div class="relative">
            <Trophy class="w-4 h-4 text-yellow-500 group-hover:rotate-12 transition-transform" />
            <span v-if="betStore.count > 0" class="absolute -top-2 -right-2 bg-red-500 text-white text-[9px] w-4 h-4 flex items-center justify-center rounded-full font-bold border-2 border-[#1e293b]">
                {{ betStore.count }}
            </span>
        </div>
        <span class="font-bold text-xs uppercase tracking-wide">Cupom de Apostas</span>
        <ChevronLeft class="w-4 h-4 text-gray-400 group-hover:text-white" />
      </button>

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
</style>