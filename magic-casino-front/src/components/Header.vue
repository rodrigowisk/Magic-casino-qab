<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { Search, Loader2, X, MapPin, Calendar, Trophy } from 'lucide-vue-next';
import { useAuthStore } from '../stores/useAuthStore';
import SportsService from '../services/SportsService';
import TournamentService from '../services/Tournament/TournamentService'; 

import AuthModal from './AuthModal.vue';
import WalletDropdown from './WalletDropdown.vue';
import UserDropdown from './UserDropdown.vue';

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();
// emit removido pois o menu lateral não é mais acionado daqui

// --- CONTEXTO DA BUSCA ---
const isTournamentContext = computed(() => {
    return route.path.includes('/tournament/') && route.name !== 'TournamentLobby';
});

const searchPlaceholder = computed(() => {
    return isTournamentContext.value ? 'Buscar jogos...' : 'Buscar torneio (Nome ou ID)...';
});

// --- AUTH ---
const showAuthModal = ref(false);
const authModalTab = ref<'login' | 'register'>('login');

const openAuthModal = (tab: 'login' | 'register') => {
    authModalTab.value = tab;
    showAuthModal.value = true;
};

const handleLoginSuccess = (data: any) => {
    authStore.setLogin(data.user || data, localStorage.getItem('token') || '');
    showAuthModal.value = false;
};

// --- BUSCA ---
const searchQuery = ref('');
const searchResults = ref<any[]>([]);
const isSearching = ref(false);
const showSearchResults = ref(false);
let searchTimeout: any = null;
const searchContainerRef = ref<HTMLElement | null>(null);

const handleClickOutsideSearch = (event: MouseEvent) => {
    if (searchContainerRef.value && !searchContainerRef.value.contains(event.target as Node)) {
        showSearchResults.value = false;
    }
};

const formatDate = (dateString: string) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' }) + ' ' + 
           date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
};

const getItemId = (item: any) => {
    return item.id || item.Id || item.externalId || item.gameId;
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
            const term = searchQuery.value.toLowerCase();

            if (isTournamentContext.value) {
                const events = await SportsService.getEvents('soccer', 1, 300);
                if (events && Array.isArray(events)) {
                    searchResults.value = events.filter((e: any) => {
                        const home = (e.homeTeam || e.HomeTeam || '').toLowerCase();
                        const away = (e.awayTeam || e.AwayTeam || '').toLowerCase();
                        const league = (e.league || e.League || '').toLowerCase();
                        return home.includes(term) || away.includes(term) || league.includes(term);
                    }).slice(0, 8).map(e => ({ ...e, type: 'game' })); 
                } else {
                    searchResults.value = [];
                }
            } 
            else {
                const userId = authStore.user?.id || ''; 
                const res = await TournamentService.listTournaments(userId);
                
                if (res.data && Array.isArray(res.data)) {
                    searchResults.value = res.data.filter((t: any) => {
                        const name = (t.name || '').toLowerCase();
                        const id = String(t.id || '');
                        return name.includes(term) || id.includes(term);
                    }).slice(0, 8).map(t => ({ ...t, type: 'tournament' })); 
                } else {
                    searchResults.value = [];
                }
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

const goToResult = (item: any) => {
    const id = getItemId(item);
    if (!id) return;

    if (item.type === 'tournament') {
        router.push(`/tournament/${id}/play`);
    } else {
        router.push(`/event/${id}`);
    }
    
    showSearchResults.value = false;
    clearSearch(); 
};

watch(() => route.path, () => {
    clearSearch();
});

onMounted(() => { document.addEventListener('click', handleClickOutsideSearch); });
onUnmounted(() => { document.removeEventListener('click', handleClickOutsideSearch); });
</script>

<template>
  <header class="h-16 bg-[#0f172a]/95 flex items-center justify-between px-4 shadow-lg sticky top-0 z-[100] flex-shrink-0 border-b border-white/5 backdrop-blur">
      
      <AuthModal 
        v-if="showAuthModal" 
        :initial-tab="authModalTab"
        @close="showAuthModal = false" 
        @login-success="handleLoginSuccess" 
      />

      <div class="flex items-center">
        <div 
            @click="router.push('/')" 
            class="w-auto md:w-56 flex justify-start cursor-pointer select-none hover:brightness-110 transition-all group"
            style="font-family: 'Montserrat', sans-serif;"
        >
            <img src="/logo.png?v=4" alt="Logo" class="h-8 md:h-14 object-contain" />
        </div>
      </div>
      
      <div class="hidden md:flex relative w-96 z-[101]" ref="searchContainerRef">
        <div class="w-full flex items-center bg-[#1e293b] rounded-full border border-gray-700/50 hover:border-gray-500 transition-colors focus-within:border-blue-500 focus-within:ring-1 focus-within:ring-blue-500/50">
            <div class="pl-4 pr-2 py-2 text-slate-400">
                <Search v-if="!isSearching" class="w-4 h-4" />
                <Loader2 v-else class="w-4 h-4 animate-spin text-blue-500" />
            </div>
            <input 
                type="text" 
                v-model="searchQuery" 
                @input="handleInput" 
                @focus="showSearchResults = true" 
                :placeholder="searchPlaceholder" 
                class="bg-transparent outline-none text-white text-sm w-full py-2 placeholder-gray-500"
            >
            <button v-if="searchQuery" @click="clearSearch" class="mr-3 text-gray-500 hover:text-white transition-colors"><X class="w-4 h-4" /></button>
        </div>

        <transition enter-active-class="transition duration-100 ease-out" enter-from-class="transform scale-95 opacity-0 -translate-y-2" enter-to-class="transform scale-100 opacity-100 translate-y-0" leave-active-class="transition duration-75 ease-in" leave-from-class="transform scale-100 opacity-100 translate-y-0" leave-to-class="transform scale-95 opacity-0 -translate-y-2">
            <div v-if="showSearchResults && (searchResults.length > 0 || isSearching || searchQuery.length >= 2)" class="absolute top-full left-0 w-full mt-2 bg-[#1e293b] border border-gray-700 rounded-xl shadow-2xl overflow-hidden max-h-[400px] overflow-y-auto custom-scrollbar z-[9999]">
                
                <div v-if="isSearching" class="p-4 text-center text-sm text-gray-400 flex items-center justify-center gap-2">
                    <Loader2 class="w-4 h-4 animate-spin" /> Buscando...
                </div>
                
                <div v-else-if="searchResults.length === 0 && searchQuery.length >= 2" class="p-4 text-center text-sm text-gray-400">
                    Nenhum resultado encontrado.
                </div>
                
                <div v-else-if="searchResults.length > 0" class="flex flex-col">
                    <div class="px-3 py-2 text-[10px] uppercase font-bold text-gray-500 bg-[#0f172a]/50 border-b border-white/5">
                        {{ isTournamentContext ? 'Jogos Encontrados' : 'Torneios Encontrados' }}
                    </div>
                    
                    <button v-for="result in searchResults" :key="getItemId(result)" @click="goToResult(result)" class="flex flex-col gap-1 px-4 py-3 hover:bg-[#2f4553] transition-colors border-b border-white/5 last:border-0 text-left group">
                        
                        <template v-if="result.type === 'game'">
                            <div class="flex items-center gap-2 text-[10px] text-gray-400 mb-0.5">
                                <MapPin class="w-3 h-3 text-gray-500" />
                                <span class="truncate max-w-[180px] font-medium">{{ result.league || result.League }}</span>
                                <span class="w-1 h-1 bg-gray-600 rounded-full"></span>
                                <Calendar class="w-3 h-3 text-gray-500" />
                                <span>{{ formatDate(result.commenceTime || result.commence_time) }}</span>
                            </div>
                            <div class="flex items-center justify-between">
                                <div class="text-sm font-bold text-white group-hover:text-blue-400 transition-colors">
                                    {{ result.homeTeam || result.HomeTeam }} <span class="text-gray-500 mx-1 font-normal">vs</span> {{ result.awayTeam || result.AwayTeam }}
                                </div>
                            </div>
                        </template>

                        <template v-else>
                            <div class="flex items-center gap-2 text-[10px] text-gray-400 mb-0.5">
                                <Trophy class="w-3 h-3 text-yellow-500" />
                                <span class="font-medium text-yellow-500/80 uppercase tracking-wider">Torneio</span>
                                <span class="w-1 h-1 bg-gray-600 rounded-full"></span>
                                <span class="font-mono text-gray-500">ID: #{{ result.id }}</span>
                            </div>
                            <div class="flex items-center justify-between">
                                <div class="text-sm font-bold text-white group-hover:text-yellow-400 transition-colors uppercase">
                                    {{ result.name }}
                                </div>
                                <span v-if="result.isJoined" class="text-[9px] bg-green-500/10 text-green-400 px-1.5 py-0.5 rounded border border-green-500/20">INSCRITO</span>
                            </div>
                        </template>

                    </button>
                </div>
            </div>
        </transition>
      </div>

      <div class="flex items-center gap-2 md:gap-3">
        <div v-if="authStore.user" class="flex items-center gap-2 md:gap-4">
            
            <button @click="router.push('/promocoes')" class="relative group transition-all duration-300 transform hover:scale-110 hidden md:block" title="Bônus">
                <div class="absolute inset-0 bg-green-500/40 blur-lg rounded-full opacity-0 group-hover:opacity-100 transition-opacity duration-300"></div>
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" class="relative z-10 drop-shadow-lg filter">
                    <path d="M4 10H20V20C20 21.1046 19.1046 22 18 22H6C4.89543 22 4 21.1046 4 20V10Z" fill="#22c55e" stroke="#166534" stroke-width="1.5"/>
                    <path d="M2 6H22V10H2V6Z" fill="#4ade80" stroke="#166534" stroke-width="1.5"/>
                    <path d="M11 10H13V22H11V10Z" fill="#ef4444"/>
                    <path d="M11 6H13V10H11V6Z" fill="#ef4444"/>
                    <path d="M12 6C12 6 9 2 6 4C3 6 6 6 12 6Z" fill="#ef4444" stroke="#991b1b" stroke-width="1"/>
                    <path d="M12 6C12 6 15 2 18 4C21 6 18 6 12 6Z" fill="#ef4444" stroke="#991b1b" stroke-width="1"/>
                </svg>
                <span class="absolute -top-1 -right-1 w-2.5 h-2.5 bg-red-500 rounded-full border-2 border-[#1e293b] z-20 animate-pulse shadow-md"></span>
            </button>
            
            <WalletDropdown :balance="authStore.user.balance || 0" />
            
            <UserDropdown />
        </div>
        <div v-else class="flex items-center gap-2">
            <button @click="openAuthModal('login')" class="font-bold text-gray-300 text-xs md:text-sm hover:text-white transition-colors px-2">Entrar</button>
            <button @click="openAuthModal('register')" class="bg-blue-600 hover:bg-blue-500 text-white px-4 md:px-6 py-1.5 md:py-2 rounded-md font-bold text-xs md:text-sm shadow-lg shadow-blue-900/50 transition-all">Cadastre-se</button>
        </div>
      </div>
  </header>
</template>