<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { Menu, Search, Wallet, LogOut, Trophy, ChevronLeft, Gem } from 'lucide-vue-next';

import Sidebar from '../components/Sidebar.vue'; 
import TopSportsMenu from '../components/TopSportsMenu.vue'; 
import AuthModal from '../components/AuthModal.vue'; 
import BetSlip from '../components/BetSlip.vue'; 
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

const isHistoryPage = computed(() => route.path === '/minhas-apostas'); 

const handleLogout = () => {
    authStore.logout();
    router.push('/');
};

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
</script>

<template>
  <div class="h-screen bg-stake-dark text-stake-text font-sans flex flex-col overflow-hidden">
    <component is="style">
      @import url('https://fonts.googleapis.com/css2?family=Montserrat:wght@400;700;900&display=swap');
    </component>

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
            <img v-if="configStore.siteLogo" :src="configStore.siteLogo" alt="Logo" class="h-12 md:h-14 object-contain" />
            
            <div v-else class="flex items-center gap-2">
                <div class="bg-gradient-to-br from-yellow-400 to-yellow-600 p-1.5 rounded-lg shadow-lg shadow-yellow-500/20 group-hover:shadow-yellow-500/40 transition-shadow">
                    <Gem class="w-5 h-5 text-gray-900 fill-current" />
                </div>

                <div class="flex flex-col justify-center">
                    <span class="text-gray-300 font-bold text-[10px] uppercase tracking-[0.2em] leading-none mb-0.5">
                        QUEBRANDO
                    </span>
                    <span class="text-xl md:text-2xl font-black italic text-transparent bg-clip-text bg-gradient-to-r from-yellow-200 via-yellow-400 to-yellow-600 drop-shadow-sm leading-none tracking-tight">
                        A BANCA
                    </span>
                </div>
            </div>
        </div>
      </div>
      
      <div class="hidden md:flex bg-stake-dark rounded-full px-4 py-2 w-96 border border-gray-700/50 hover:border-gray-500 transition-colors focus-within:border-stake-blue focus-within:ring-1 focus-within:ring-stake-blue">
        <Search class="w-4 h-4 text-stake-text mr-2 mt-1" />
        <input type="text" placeholder="Buscar jogos, ligas..." class="bg-transparent outline-none text-white text-sm w-full placeholder-gray-500">
      </div>

      <div class="flex items-center gap-3">
        <div v-if="authStore.user" class="flex items-center gap-3">
            <div class="hidden md:flex items-center gap-2 bg-[#0f172a] px-4 py-1.5 rounded-full text-sm text-white border border-gray-700 shadow-inner">
                <Wallet class="w-4 h-4 text-green-400" />
                <span class="font-bold tracking-wide">R$ {{ authStore.user.balance?.toFixed(2) }}</span>
            </div>
            <div class="flex items-center gap-2 cursor-pointer hover:opacity-80 transition-opacity" @click="router.push('/minhas-apostas')">
                <div class="w-9 h-9 bg-gradient-to-br from-blue-600 to-blue-800 rounded-full flex items-center justify-center text-white font-bold shadow-lg border border-blue-400/30">
                    {{ authStore.user.name?.charAt(0)?.toUpperCase() || 'U' }}
                </div>
                <span class="hidden md:block text-white text-sm font-bold">{{ authStore.user.name }}</span>
            </div>
            <button @click="handleLogout" class="text-gray-400 hover:text-red-500 transition-colors ml-2 p-1 hover:bg-white/5 rounded-full">
                <LogOut class="w-5 h-5" />
            </button>
        </div>
        <div v-else class="flex items-center gap-3">
            <button @click="showAuthModal = true" class="font-bold text-gray-300 text-sm hover:text-white transition-colors px-2">Entrar</button>
            <button @click="showAuthModal = true" class="bg-blue-600 hover:bg-blue-500 text-white px-6 py-2 rounded-md font-bold text-sm shadow-lg shadow-blue-900/50 transition-all transform hover:-translate-y-0.5">
                Cadastre-se
            </button>
        </div>
      </div>
    </header>

    <div class="flex flex-1 overflow-hidden relative">
      <Sidebar v-show="isSidebarOpen" class="w-64 flex-shrink-0 transition-all duration-300 border-r border-white/5" />

      <main class="flex-1 overflow-y-auto bg-stake-dark custom-scrollbar p-4 md:p-6 relative transition-all duration-300">
        <div v-if="!isHistoryPage" class="w-full mb-6">
            <TopSportsMenu />
        </div>
        <div class="w-full">
            <router-view />
        </div>
      </main>
      
      <div v-show="isBetSlipOpen" class="w-[320px] bg-[#1e293b] border-l border-gray-700 flex flex-col flex-shrink-0 transition-all duration-300 shadow-2xl z-40">
          <BetSlip @toggle="toggleBetSlip" :is-open="true" />
      </div>

      <button 
        v-if="!isBetSlipOpen"
        @click="toggleBetSlip"
        class="absolute bottom-6 right-6 z-50 bg-[#1e293b]/60  hover:bg-[#1e293b] text-white border border-yellow-500/30 shadow-2xl shadow-black/50 rounded-md px-6 py-3 flex items-center gap-3 transition-all hover:scale-105 group"
      >
        <div class="relative">
            <Trophy class="w-5 h-5 text-yellow-500 group-hover:rotate-12 transition-transform" />
            <span v-if="betStore.count > 0" class="absolute -top-2 -right-2 bg-red-500 text-white text-[10px] w-5 h-5 flex items-center justify-center rounded-full font-bold border-2 border-[#1e293b]">
                {{ betStore.count }}
            </span>
        </div>
        <span class="font-bold text-sm uppercase tracking-wide">Cupom de Apostas</span>
        <ChevronLeft class="w-4 h-4 text-gray-400 group-hover:text-white" />
      </button>

    </div>
  </div>
</template>