<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch, nextTick } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { Search, Loader2, X, MapPin, Calendar, Trophy, Info } from 'lucide-vue-next';
import Swal from 'sweetalert2';
import { useAuthStore } from '../stores/useAuthStore';
import SportsService from '../services/SportsService';
import TournamentService from '../services/Tournament/TournamentService'; 

import AuthModal from './AuthModal.vue';
import WalletDropdown from './WalletDropdown.vue';
import UserDropdown from './UserDropdown.vue';
import TournamentInfoModal from './Tournament/TournamentInfoModal.vue';
import { useTournamentModal } from '../composables/useTournamentModal';

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();
const { confirmPurchase } = useTournamentModal();

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

// 🔥 NOVO SISTEMA DE IMAGENS À PROVA DE FALHAS 🔥
const coversModules = import.meta.glob('/src/assets/tournament_covers/**/*.{png,jpg,jpeg,svg,webp}', { eager: true });

const getDefaultImage = (): string => {
    const vals = Object.values(coversModules);
    const first = vals[0] as { default: string } | undefined;
    return first ? first.default : '';
};

const getCardImage = (t: any): string => {
    if (!t) return getDefaultImage();
    
    const rawName = t?.coverImage || t?.CoverImage;
    if (!rawName) return getDefaultImage();

    const query = String(rawName).trim().toLowerCase().replace(/\\/g, '/');

    // 1. Tenta achar o caminho exato
    for (const path in coversModules) {
        if (path.toLowerCase().endsWith(query)) {
            const mod = coversModules[path] as { default: string } | undefined;
            return mod ? mod.default : getDefaultImage();
        }
    }

    // 2. Fallback de segurança para torneios antigos
    const justName = query.split('/').pop();
    if (justName) {
        for (const path in coversModules) {
            if (path.toLowerCase().endsWith('/' + justName)) {
                const mod = coversModules[path] as { default: string } | undefined;
                return mod ? mod.default : getDefaultImage();
            }
        }
    }

    return getDefaultImage();
};

// --- MODAL DE INFO DO TORNEIO ---
const showInfoModal = ref(false);
const selectedTournament = ref<any>(null);

const openInfoModal = (tournament: any) => {
    selectedTournament.value = tournament;
    showInfoModal.value = true;
};

const closeInfoModal = () => {
    showInfoModal.value = false;
    setTimeout(() => { selectedTournament.value = null; }, 300); // Aguarda animação para limpar
};

const handleJoinFromModal = async (id: number) => {
    const t = selectedTournament.value;
    if (!t) return;

    // 1. Abre o modal de confirmação de compra
    const isConfirmed = await confirmPurchase(t);

    if (isConfirmed) {
        try {
            // 2. Valida se o usuário está logado e pega os dados
            const user = authStore.user;
            if (!user) {
                Swal.fire({ toast: true, position: 'top-end', icon: 'warning', title: 'Você precisa estar logado!', showConfirmButton: false, timer: 3000, background: '#121212', color: '#fff' });
                return;
            }

            // 3. Limpa o visual
            closeInfoModal();
            showSearchResults.value = false;
            showMobileSearch.value = false;
            clearSearch();
            
            // 4. Dispara a requisição para o backend confirmar a inscrição!
            await TournamentService.joinTournament(
                id, 
                user.id, 
                user.name || user.userName || 'Jogador', 
                user.avatar || ''
            );
            
            // 5. Sucesso! Vai pro jogo.
            router.push(`/tournament/${id}/play`);
            
        } catch (error: any) {
            console.error("Erro ao comprar entrada:", error);
            
            Swal.fire({
                toast: true, 
                position: 'top-end', 
                icon: 'error', 
                title: error?.response?.data?.message || 'Erro ao processar a compra!', 
                showConfirmButton: false, 
                timer: 3000, 
                background: '#121212', 
                color: '#fff' 
            });
        }
    }
};

// --- BUSCA E CONTROLE MOBILE ---
const searchQuery = ref('');
const searchResults = ref<any[]>([]);
const isSearching = ref(false);
const showSearchResults = ref(false);
const showMobileSearch = ref(false); // <--- Controle do painel mobile
const mobileSearchInput = ref<HTMLInputElement | null>(null);

let searchTimeout: any = null;
const searchContainerRef = ref<HTMLElement | null>(null);
const mobileSearchContainerRef = ref<HTMLElement | null>(null);

const toggleMobileSearch = async () => {
    showMobileSearch.value = !showMobileSearch.value;
    if (showMobileSearch.value) {
        await nextTick();
        mobileSearchInput.value?.focus();
    } else {
        clearSearch();
    }
};

const handleClickOutsideSearch = (event: MouseEvent) => {
    const target = event.target as Node;
    
    // Fecha resultados no desktop
    if (searchContainerRef.value && !searchContainerRef.value.contains(target)) {
        showSearchResults.value = false;
    }

    // Fecha a barra mobile se clicar fora dela E não for no botão da lupa
    const searchToggleButton = document.getElementById('mobile-search-toggle');
    if (showMobileSearch.value && 
        mobileSearchContainerRef.value && !mobileSearchContainerRef.value.contains(target) &&
        searchToggleButton && !searchToggleButton.contains(target)) {
        showMobileSearch.value = false;
        clearSearch();
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
        openInfoModal(item);
        showSearchResults.value = false;
        showMobileSearch.value = false; // Fecha a barra no mobile se estiver aberta
    } else {
        router.push(`/event/${id}`);
        showSearchResults.value = false;
        showMobileSearch.value = false;
        clearSearch(); 
    }
};

watch(() => route.path, () => {
    clearSearch();
    showMobileSearch.value = false; // Garante que a barra feche ao mudar de rota
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

      <TournamentInfoModal 
        :show="showInfoModal"
        :tournament="selectedTournament"
        @close="closeInfoModal"
        @join="handleJoinFromModal"
      />

      <div class="flex items-center gap-2">
        <div 
            @click="router.push('/')" 
            class="w-auto md:w-56 flex justify-start cursor-pointer select-none hover:brightness-110 transition-all group"
            style="font-family: 'Montserrat', sans-serif;"
        >
            <img src="/logo.png?v=4" alt="Logo" class="h-8 md:h-14 object-contain" />
        </div>

        <button 
            id="mobile-search-toggle"
            @click="toggleMobileSearch" 
            class="md:hidden p-1 text-slate-400 hover:text-white transition-all"
        >
            <Search class="w-5 h-5" />
        </button>
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
            <div v-if="showSearchResults && (searchResults.length > 0 || isSearching || searchQuery.length >= 2)" class="absolute top-full left-0 w-full mt-2 bg-[#0f172a] border border-gray-700 rounded-xl shadow-2xl overflow-hidden max-h-[400px] overflow-y-auto custom-scrollbar z-[9999]">
                
                <div v-if="isSearching" class="p-4 text-center text-sm text-gray-400 flex items-center justify-center gap-2">
                    <Loader2 class="w-4 h-4 animate-spin" /> Buscando...
                </div>
                <div v-else-if="searchResults.length === 0 && searchQuery.length >= 2" class="p-4 text-center text-sm text-gray-400">
                    Nenhum resultado encontrado.
                </div>
                <div v-else-if="searchResults.length > 0" class="flex flex-col">
                    <div class="px-3 py-2 text-[10px] uppercase font-bold text-gray-500 bg-[#0f172a] border-b border-white/5 relative z-20">
                        {{ isTournamentContext ? 'Jogos Encontrados' : 'Torneios Encontrados' }}
                    </div>
                    <button v-for="result in searchResults" :key="getItemId(result)" @click="goToResult(result)" class="relative flex flex-col gap-1 px-4 py-3 border-b border-white/5 last:border-0 text-left group overflow-hidden bg-[#0f172a]">
                        
                        <div v-if="result.type === 'game'" class="absolute inset-0 bg-transparent group-hover:bg-[#1e293b] transition-colors duration-200 z-0 pointer-events-none"></div>

                        <div v-if="result.type === 'tournament'" class="absolute inset-0 z-0 pointer-events-none bg-[#0f172a]">
                            <img :src="getCardImage(result)" class="absolute right-0 top-0 w-3/4 h-full object-cover opacity-100 transition-opacity duration-300" />
                            <div class="absolute inset-0 bg-gradient-to-r from-[#0f172a] via-[#0f172a]/90 to-transparent group-hover:from-[#1e293b] group-hover:via-[#1e293b]/90 transition-colors duration-200"></div>
                        </div>

                        <template v-if="result.type === 'game'">
                            <div class="relative z-10 flex items-center gap-2 text-[10px] text-gray-400 mb-0.5">
                                <MapPin class="w-3 h-3 text-gray-500" />
                                <span class="truncate max-w-[180px] font-medium">{{ result.league || result.League }}</span>
                                <span class="w-1 h-1 bg-gray-600 rounded-full"></span>
                                <Calendar class="w-3 h-3 text-gray-500" />
                                <span>{{ formatDate(result.commenceTime || result.commence_time) }}</span>
                            </div>
                            <div class="relative z-10 flex items-center justify-between w-full">
                                <div class="text-sm font-bold text-white group-hover:text-blue-400 transition-colors">
                                    {{ result.homeTeam || result.HomeTeam }} <span class="text-gray-500 mx-1 font-normal">vs</span> {{ result.awayTeam || result.AwayTeam }}
                                </div>
                            </div>
                        </template>

                        <template v-else>
                            <div class="relative z-10 flex items-center gap-2 text-[10px] text-gray-400 mb-0.5">
                                <Trophy class="w-3 h-3 text-yellow-500" />
                                <span class="font-medium text-yellow-500/80 uppercase tracking-wider">Torneio</span>
                                <span class="w-1 h-1 bg-gray-600 rounded-full"></span>
                                <span class="font-mono text-gray-500">ID: #{{ result.id }}</span>
                            </div>
                            <div class="relative z-10 flex items-center justify-between w-full">
                                <div class="text-sm font-black text-white group-hover:text-yellow-400 transition-colors uppercase truncate pr-2 tracking-wide italic drop-shadow-md">
                                    {{ result.name }}
                                </div>
                                <div class="flex items-center gap-2 shrink-0">
                                    <span v-if="result.isJoined" class="text-[9px] bg-green-500/10 text-green-400 px-1.5 py-0.5 rounded border border-green-500/20 font-bold tracking-wider">INSCRITO</span>
                                    
                                    <button type="button" @click.stop.prevent="openInfoModal(result)" 
                                            class="h-7 w-7 flex items-center justify-center rounded bg-white text-black hover:bg-gray-200 transition-all shadow-lg shadow-white/20 hover:scale-105 active:scale-95 border border-transparent cursor-pointer" 
                                            title="Informações do Torneio">
                                        <Info class="w-4 h-4" />
                                    </button>
                                </div>
                            </div>
                        </template>
                    </button>
                </div>
            </div>
        </transition>
      </div>

      <div class="flex items-center gap-2 md:gap-3 relative z-[105]">
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

      <transition 
          enter-active-class="transition duration-200 ease-out" 
          enter-from-class="transform -translate-y-4 opacity-0" 
          enter-to-class="transform translate-y-0 opacity-100" 
          leave-active-class="transition duration-150 ease-in" 
          leave-from-class="transform translate-y-0 opacity-100" 
          leave-to-class="transform -translate-y-4 opacity-0"
      >
          <div v-if="showMobileSearch" ref="mobileSearchContainerRef" class="md:hidden absolute top-16 left-0 w-full bg-[#0f172a] shadow-2xl border-b border-white/10 z-[99]">
              
              <div class="p-3">
                  <div class="w-full flex items-center bg-[#1e293b] rounded-xl border border-blue-500/50 shadow-[0_0_15px_rgba(59,130,246,0.2)]">
                      <div class="pl-4 pr-2 py-3 text-slate-400">
                          <Search v-if="!isSearching" class="w-5 h-5 text-blue-400" />
                          <Loader2 v-else class="w-5 h-5 animate-spin text-blue-500" />
                      </div>
                      <input 
                          type="text" 
                          ref="mobileSearchInput"
                          v-model="searchQuery" 
                          @input="handleInput" 
                          :placeholder="searchPlaceholder" 
                          class="bg-transparent outline-none text-white text-base w-full py-3 placeholder-gray-500"
                      >
                      <button v-if="searchQuery" @click="clearSearch" class="px-3 text-gray-500 hover:text-white"><X class="w-5 h-5" /></button>
                  </div>
              </div>

              <div v-if="searchResults.length > 0 || isSearching || searchQuery.length >= 2" class="w-full max-h-[60vh] overflow-y-auto custom-scrollbar bg-[#0f172a] pb-2">
                  <div v-if="isSearching" class="p-6 text-center text-sm text-gray-400 flex items-center justify-center gap-2">
                      <Loader2 class="w-5 h-5 animate-spin" /> Buscando...
                  </div>
                  <div v-else-if="searchResults.length === 0 && searchQuery.length >= 2" class="p-6 text-center text-sm text-gray-400">
                      Nenhum resultado encontrado.
                  </div>
                  <div v-else-if="searchResults.length > 0" class="flex flex-col">
                      <div class="px-4 py-2.5 text-[11px] uppercase font-bold text-gray-400 bg-[#0f172a] border-y border-white/5 sticky top-0 z-20">
                          {{ isTournamentContext ? 'Jogos Encontrados' : 'Torneios Encontrados' }}
                      </div>
                      <button v-for="result in searchResults" :key="getItemId(result)" @click="goToResult(result)" class="relative flex flex-col gap-1.5 px-4 py-4 border-b border-white/5 last:border-0 text-left overflow-hidden bg-[#0f172a]">
                          
                          <div v-if="result.type === 'tournament'" class="absolute inset-0 z-0 pointer-events-none bg-[#0f172a]">
                              <img :src="getCardImage(result)" class="absolute right-0 top-0 w-3/4 h-full object-cover opacity-100" />
                              <div class="absolute inset-0 bg-gradient-to-r from-[#0f172a] via-[#0f172a]/90 to-transparent"></div>
                          </div>

                          <template v-if="result.type === 'game'">
                              <div class="relative z-10 flex items-center gap-2 text-xs text-gray-400 mb-0.5">
                                  <MapPin class="w-3.5 h-3.5 text-gray-500" />
                                  <span class="truncate font-medium">{{ result.league || result.League }}</span>
                                  <span class="w-1 h-1 bg-gray-600 rounded-full"></span>
                                  <Calendar class="w-3.5 h-3.5 text-gray-500" />
                                  <span>{{ formatDate(result.commenceTime || result.commence_time) }}</span>
                              </div>
                              <div class="relative z-10 text-[15px] font-bold text-white leading-tight">
                                  {{ result.homeTeam || result.HomeTeam }} <span class="text-gray-500 mx-1 font-normal">vs</span> {{ result.awayTeam || result.AwayTeam }}
                              </div>
                          </template>

                          <template v-else>
                              <div class="relative z-10 flex items-center gap-2 text-[11px] text-gray-400 mb-0.5">
                                  <Trophy class="w-3.5 h-3.5 text-yellow-500" />
                                  <span class="font-medium text-yellow-500/80 uppercase tracking-wider">Torneio</span>
                                  <span class="w-1 h-1 bg-gray-600 rounded-full"></span>
                                  <span class="font-mono text-gray-500">ID: #{{ result.id }}</span>
                              </div>
                              <div class="relative z-10 flex items-center justify-between w-full">
                                  <div class="text-[15px] font-black text-white uppercase truncate pr-3 tracking-wide italic drop-shadow-md">
                                      {{ result.name }}
                                  </div>
                                  <div class="flex items-center gap-2 shrink-0">
                                      <span v-if="result.isJoined" class="text-[10px] bg-green-500/10 text-green-400 px-2 py-1 rounded border border-green-500/20 font-bold tracking-wider">INSCRITO</span>
                                      
                                      <button type="button" @click.stop.prevent="openInfoModal(result)" 
                                              class="h-8 w-8 flex items-center justify-center rounded bg-white text-black hover:bg-gray-200 transition-all shadow-lg shadow-white/20 hover:scale-105 active:scale-95 border border-transparent cursor-pointer" 
                                              title="Informações do Torneio">
                                          <Info class="w-4.5 h-4.5" />
                                      </button>
                                  </div>
                              </div>
                          </template>
                      </button>
                  </div>
              </div>
          </div>
      </transition>
  </header>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar {
  width: 6px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: transparent;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background-color: #334155;
  border-radius: 10px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background-color: #475569;
}
</style>