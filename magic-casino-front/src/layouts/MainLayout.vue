<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';
import { Menu, Search, Trophy, ChevronLeft, Loader2, X, MapPin, Calendar } from 'lucide-vue-next';
// Removido axios não utilizado
import SportsService from '../services/SportsService'; 

import Sidebar from '../components/Sidebar.vue'; 
import AuthModal from '../components/AuthModal.vue'; 
import BetSlip from '../components/BetSlip.vue'; 
import WalletDropdown from '../components/WalletDropdown.vue'; 
import UserDropdown from '../components/UserDropdown.vue'; 

import { useBetStore } from '../stores/useBetStore'; 
import { useAuthStore } from '../stores/useAuthStore';

const router = useRouter();
const betStore = useBetStore(); 
const authStore = useAuthStore();

const isSidebarOpen = ref(true);
const showAuthModal = ref(false);
const isBetSlipOpen = ref(betStore.count > 0);

// --- LÓGICA DE BUSCA ---
const searchQuery = ref('');
const searchResults = ref<any[]>([]);
const isSearching = ref(false);
const showSearchResults = ref(false);
let searchTimeout: any = null;
const searchContainerRef = ref<HTMLElement | null>(null);

const formatDate = (dateString: string) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' }) + ' ' + 
           date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
};

// Função segura para pegar o ID
const getEventId = (item: any) => {
    if (!item) return null;
    return item.externalId || item.ExternalId || item.id || item.Id || item.eventId || item.gameId;
};

const handleInput = () => {
    if (searchTimeout) clearTimeout(searchTimeout);

    if (searchQuery.value.length < 2) {
        searchResults.value = [];
        showSearchResults.value = false;
        return;
    }

    isSearching.value = true;
    showSearchResults.value = true;

    searchTimeout = setTimeout(async () => {
        try {
            // Busca Local
            const events = await SportsService.getEvents('soccer', 1, 300);
            
            if (events && Array.isArray(events)) {
                const term = searchQuery.value.toLowerCase();
                searchResults.value = events.filter((e: any) => {
                    const home = (e.homeTeam || e.HomeTeam || '').toLowerCase();
                    const away = (e.awayTeam || e.AwayTeam || '').toLowerCase();
                    const league = (e.league || e.League || '').toLowerCase();
                    return home.includes(term) || away.includes(term) || league.includes(term);
                }).slice(0, 8);
            } else {
                searchResults.value = [];
            }
        } catch (error) {
            console.error("Erro na busca:", error);
            searchResults.value = [];
        } finally {
            isSearching.value = false;
        }
    }, 600);
};

const clearSearch = () => {
    searchQuery.value = '';
    searchResults.value = [];
    showSearchResults.value = false;
};

const goToEvent = (item: any) => {
    const id = getEventId(item);
    if (!id) {
        console.error("ID não encontrado no objeto:", item);
        return;
    }
    router.push(`/event/${id}`);
    showSearchResults.value = false;
    clearSearch(); 
};

const handleClickOutsideSearch = (event: MouseEvent) => {
    if (searchContainerRef.value && !searchContainerRef.value.contains(event.target as Node)) {
        showSearchResults.value = false;
    }
};

// ----------------------------------------------------

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
    if (typeof window !== 'undefined') {
        if (window.innerWidth < 768) {
            isSidebarOpen.value = false;
        } else {
            isSidebarOpen.value = true;
        }
    }
};

onMounted(() => {
    checkScreenSize();
    window.addEventListener('resize', checkScreenSize);
    document.addEventListener('click', handleClickOutsideSearch);
});

onUnmounted(() => {
    window.removeEventListener('resize', checkScreenSize);
    document.removeEventListener('click', handleClickOutsideSearch);
});
</script>

<template>
  <div class="h-screen bg-stake-dark text-stake-text font-sans flex flex-col overflow-hidden">
    
    <AuthModal v-if="showAuthModal" @close="showAuthModal = false" @login-success="handleLoginSuccess" />

    <header class="h-16 bg-stake-card flex items-center justify-between px-4 shadow-lg sticky top-0 z-[100] flex-shrink-0 border-b border-white/5">
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
      
      <div class="hidden md:flex relative w-96 z-[101]" ref="searchContainerRef">
        
        <div class="w-full flex items-center bg-stake-dark rounded-full border border-gray-700/50 hover:border-gray-500 transition-colors focus-within:border-stake-blue focus-within:ring-1 focus-within:ring-stake-blue">
            <div class="pl-4 pr-2 py-2 text-stake-text">
                <Search v-if="!isSearching" class="w-4 h-4" />
                <Loader2 v-else class="w-4 h-4 animate-spin text-stake-blue" />
            </div>
            
            <input 
                type="text" 
                v-model="searchQuery"
                @input="handleInput"
                @focus="showSearchResults = true"
                placeholder="Buscar jogos..." 
                class="bg-transparent outline-none text-white text-sm w-full py-2 placeholder-gray-500"
            >

            <button v-if="searchQuery" @click="clearSearch" class="mr-3 text-gray-500 hover:text-white transition-colors">
                <X class="w-4 h-4" />
            </button>
        </div>

        <transition
            enter-active-class="transition duration-100 ease-out"
            enter-from-class="transform scale-95 opacity-0 -translate-y-2"
            enter-to-class="transform scale-100 opacity-100 translate-y-0"
            leave-active-class="transition duration-75 ease-in"
            leave-from-class="transform scale-100 opacity-100 translate-y-0"
            leave-to-class="transform scale-95 opacity-0 -translate-y-2"
        >
            <div v-if="showSearchResults && (searchResults.length > 0 || isSearching || searchQuery.length >= 2)" 
                 class="absolute top-full left-0 w-full mt-2 bg-[#1e293b] border border-gray-700 rounded-xl shadow-2xl overflow-hidden max-h-[400px] overflow-y-auto custom-scrollbar z-[9999]">
                
                <div v-if="isSearching" class="p-4 text-center text-sm text-gray-400 flex items-center justify-center gap-2">
                    <Loader2 class="w-4 h-4 animate-spin" /> Buscando...
                </div>

                <div v-else-if="searchResults.length === 0 && searchQuery.length >= 2" class="p-4 text-center text-sm text-gray-400">
                    Nenhum jogo encontrado.
                </div>

                <div v-else-if="searchResults.length > 0" class="flex flex-col">
                    <div class="px-3 py-2 text-[10px] uppercase font-bold text-gray-500 bg-[#0f172a]/50 border-b border-white/5">
                        Resultados Encontrados
                    </div>
                    
                    <button 
                        v-for="result in searchResults" 
                        :key="getEventId(result)"
                        @click="goToEvent(result)"
                        class="flex flex-col gap-1 px-4 py-3 hover:bg-[#2f4553] transition-colors border-b border-white/5 last:border-0 text-left group"
                    >
                        <div class="flex items-center gap-2 text-[10px] text-gray-400 mb-0.5">
                            <MapPin class="w-3 h-3 text-gray-500" />
                            <span class="truncate max-w-[180px] font-medium">{{ result.league || result.League }}</span>
                            <span class="w-1 h-1 bg-gray-600 rounded-full"></span>
                            <Calendar class="w-3 h-3 text-gray-500" />
                            <span>{{ formatDate(result.commenceTime || result.commence_time) }}</span>
                        </div>

                        <div class="flex items-center justify-between">
                            <div class="text-sm font-bold text-white group-hover:text-stake-blue transition-colors">
                                {{ result.homeTeam || result.HomeTeam }} 
                                <span class="text-gray-500 mx-1 font-normal">vs</span> 
                                {{ result.awayTeam || result.AwayTeam }}
                            </div>
                        </div>
                    </button>
                </div>
            </div>
        </transition>
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