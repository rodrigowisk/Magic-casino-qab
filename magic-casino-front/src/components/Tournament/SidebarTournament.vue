<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { 
    ChevronDown, MapPin, Star,
    Trophy, ArrowLeftCircle, 
    Swords 
    // History removido das importações
} from 'lucide-vue-next';
import SportsService from '../../services/SportsService';
import TournamentService from '../../services/Tournament/TournamentService';
import { getFlag } from '../../utils/flags'; 
import { normalizeCountryName } from '../../utils/countryTranslations';
import { useFavoritesStore } from '../../stores/useFavoritesStore';

const router = useRouter();
const route = useRoute();
const favStore = useFavoritesStore(); 

// --- ESTADOS ---
const loading = ref(false);
const countriesList = ref<any[]>([]); 
const expandedCountries = ref<Set<string>>(new Set());
const debugInfo = ref('');

// --- CONTROLE DE EXIBIÇÃO ---
const isSportsMenuOpen = ref(true); 
const canShowSportsButton = ref(false); 

// --- DADOS DO TORNEIO ---
const tournamentName = ref('');

// --- FAVORITOS ---
const isFavoritesExpanded = ref(true); 
const expandedFavoriteCountries = ref<Set<string>>(new Set()); 

// --- DADOS ---
const tournamentId = computed(() => {
    const id = Number(route.params.id);
    return isNaN(id) ? null : id;
});

const isLobby = computed(() => route.name === 'TournamentLobby' || (!tournamentId.value && route.name !== 'TournamentHistory'));

const isHistoryPage = computed(() => {
    return route.name === 'TournamentHistory' || route.path.includes('/history');
});

const currentSportKey = ref('soccer');
const currentSportDisplay = ref('Futebol');

const sportNameTranslated = computed(() => currentSportDisplay.value);

const favoriteCountriesTree = computed(() => {
    if (countriesList.value.length === 0 || favStore.favoriteLeagues.size === 0) return [];
    
    const tree = countriesList.value.map(country => {
        const favLeagues = country.leagues.filter((lg: any) => favStore.isFavorite(lg.queryName));
        if (favLeagues.length > 0) {
            return { ...country, leagues: favLeagues };
        }
        return null;
    }).filter(c => c !== null); 
    return tree;
});

const mapSportToKey = (name: string): string => {
    const lower = (name || '').toLowerCase();
    if (lower.includes('futebol') || lower.includes('soccer')) return 'soccer';
    if (lower.includes('basket') || lower.includes('basketball')) return 'basketball';
    if (lower.includes('tenis') || lower.includes('tennis')) return 'tennis';
    return 'soccer'; 
};

// --- NAVEGAÇÃO ---
const goBackToLobby = () => router.push('/tournaments');

const goToMyTournaments = () => {
    const currentId = route.params.id || '0';
    router.push(`/tournament/${currentId}/history`);
};

// Função goToHistory removida pois era redundante

const goToLeague = (leagueName: string) => { 
    if (!tournamentId.value) return;
    router.push({ 
        name: 'TournamentPlay', 
        params: { id: tournamentId.value }, 
        query: { league: leagueName } 
    }); 
};

const toggleCountry = (countryName: string) => {
    if (expandedCountries.value.has(countryName)) expandedCountries.value.delete(countryName);
    else expandedCountries.value.add(countryName);
};

const toggleFavoriteCountry = (countryName: string) => {
    if (expandedFavoriteCountries.value.has(countryName)) expandedFavoriteCountries.value.delete(countryName);
    else expandedFavoriteCountries.value.add(countryName);
};

const toggleLeagueFavorite = (leagueName: string, event: Event) => {
    event.stopPropagation(); 
    favStore.toggleFavorite(leagueName, currentSportKey.value);
};

const getUserId = () => {
    try {
        const userStr = localStorage.getItem('user');
        if (userStr) {
            const user = JSON.parse(userStr);
            return user.id || user.Id || user.userId || '';
        }
        return localStorage.getItem('userId') || '';
    } catch (e) { return ''; }
};

const fetchAndGroupLeagues = async () => {
    if (isHistoryPage.value) {
        canShowSportsButton.value = false;
        return;
    }

    if (!tournamentId.value) {
        countriesList.value = [];
        tournamentName.value = '';
        return;
    }

    loading.value = true;
    canShowSportsButton.value = false;
    isSportsMenuOpen.value = true; 

    countriesList.value = [];
    expandedCountries.value.clear();
    debugInfo.value = '';

    try {
        let allowedLeagueIds = new Set<string>();
        let isRestricted = false;
        let tournamentEndTime = 0; 
        const userId = getUserId();
        
        const tRes = await TournamentService.getTournament(tournamentId.value, userId);
        
        if (tRes.data) {
            tournamentName.value = tRes.data.name; 
            currentSportDisplay.value = tRes.data.sport || 'Futebol';
            currentSportKey.value = mapSportToKey(currentSportDisplay.value);
            
            if (tRes.data.endDate) {
                tournamentEndTime = new Date(tRes.data.endDate).getTime();
            }

            if (tRes.data.filterRules) {
                try {
                    const rules = JSON.parse(tRes.data.filterRules);
                    const sportRules = rules.sports?.find((s: any) => 
                        mapSportToKey(s.key) === currentSportKey.value
                    );
                    
                    if (sportRules?.leagues) {
                        sportRules.leagues.forEach((l: any) => allowedLeagueIds.add(String(l.id)));
                        isRestricted = true;
                    }
                } catch (e) { console.error("Erro regras:", e); }
            }
        }

        const response = await SportsService.getEvents(currentSportKey.value, 1, 3000);
        let events = Array.isArray(response) ? response : (response.data || []);
        
        if (events && events.length > 0) {
            const now = new Date().getTime(); 

            const filteredEvents = events.filter((e: any) => {
                const gameTime = new Date(e.commenceTime).getTime();
                const isWithinTime = gameTime >= now && (tournamentEndTime === 0 || gameTime <= tournamentEndTime);

                if (!isWithinTime) return false;

                if (isRestricted) {
                    const lid = e.leagueId || e.LeagueId || e.league?.id || (e.league && e.league.id);
                    if (!lid || !allowedLeagueIds.has(String(lid))) return false;
                }
                return true;
            });
            events = filteredEvents;
        }

        if (events && events.length > 0) {
            const groups: Record<string, Set<string>> = {};
            const leagueOriginalNames: Record<string, string> = {};

            events.forEach((event: any) => {
                const rawLeague = event.league || event.League || event.leagueName || 'Outros';
                const rawCountry = event.country || event.Country || event.countryName || 'Internacional';
                const { country: displayCountry, league: cleanLeague } = normalizeCountryName(rawCountry, rawLeague);

                if (!groups[displayCountry]) groups[displayCountry] = new Set();
                groups[displayCountry].add(cleanLeague);
                leagueOriginalNames[cleanLeague] = rawLeague; 
            });

            const sortedCountries = Object.keys(groups).sort();
            
            if (sortedCountries.includes('Brasil')) {
                 const idx = sortedCountries.findIndex(c => c === 'Brasil');
                 if (idx !== -1) {
                     const removed = sortedCountries.splice(idx, 1)[0];
                     if (removed) sortedCountries.unshift(removed);
                 }
                 expandedFavoriteCountries.value.add('Brasil');
            }

            countriesList.value = sortedCountries.map(country => ({
                name: country,
                leagues: Array.from(groups[country] || []).sort().map(lgName => ({
                    displayName: lgName,
                    queryName: leagueOriginalNames[lgName] || lgName
                }))
            }));
        } else {
            countriesList.value = [];
            debugInfo.value = 'Nenhum jogo disponível neste período.';
        }

    } catch (e) {
        console.error("Erro menu torneio:", e);
    } finally {
        loading.value = false;
        setTimeout(() => {
            if (countriesList.value.length > 0) {
                canShowSportsButton.value = true;
            }
        }, 800);
    }
};

watch(() => route.params.id, fetchAndGroupLeagues, { immediate: true });
watch(() => route.path, () => {
    if (isHistoryPage.value) canShowSportsButton.value = false;
});

onMounted(() => {
    favStore.fetchFavorites();
});
</script>

<template>
  <aside 
    class="bg-[#0f172a] flex-shrink-0 overflow-y-auto border-r border-white/5 custom-scrollbar h-full w-full md:w-64 transition-all"
  >
    
    <div v-if="!isLobby" class="flex flex-col h-full">
        
        <div class="bg-blue-600/10 border-b border-blue-500/20 px-3 py-2 flex flex-col justify-center gap-1.5 shrink-0 min-h-[120px] relative">
            
            <div class="flex items-center gap-2 mb-1 pb-1.5 border-b border-blue-500/10">
                <Trophy class="w-4 h-4 text-blue-400" />
                <span class="text-[11px] font-bold uppercase text-blue-400 tracking-wide">Menu Torneio</span>
            </div>
            
            <button @click="goToMyTournaments" class="flex items-center gap-2 text-[10px] font-bold text-gray-300 hover:text-white transition-colors bg-black/20 hover:bg-blue-600/20 py-2 px-3 rounded w-full text-left border border-white/5 hover:border-blue-500/30">
                <Swords class="w-3.5 h-3.5 text-yellow-500" /> Meus Torneios
            </button>

            <div class="h-px bg-blue-500/10 w-full my-0.5"></div>

            <button @click="goBackToLobby" class="flex items-center gap-2 text-[10px] font-bold text-red-400 hover:text-red-300 transition-colors bg-black/20 hover:bg-red-500/10 py-2 px-3 rounded w-full justify-center border border-transparent hover:border-red-500/20">
                <ArrowLeftCircle class="w-3.5 h-3.5" /> Sair para o Lobby
            </button>
        </div>

        <div v-if="!isHistoryPage" class="p-4 flex-1 overflow-hidden flex flex-col">
            <transition name="fade">
                <div v-if="canShowSportsButton" class="mb-4 shrink-0">
                    <button 
                        @click="isSportsMenuOpen = !isSportsMenuOpen" 
                        class="w-full flex items-center justify-between bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-500 hover:to-blue-400 text-white text-[10px] font-bold uppercase tracking-wider py-2.5 px-3 rounded shadow-lg shadow-blue-900/20 border border-blue-400/20 transition-all group active:scale-[0.98]"
                    >
                        <span class="drop-shadow-md">Mostrar Menu Esportes</span>
                        <ChevronDown class="w-4 h-4 transition-transform duration-300 text-white/80" :class="isSportsMenuOpen ? 'rotate-180' : ''" />
                    </button>
                </div>
            </transition>

            <transition name="expand">
                <div v-show="isSportsMenuOpen" class="flex-1 overflow-y-auto custom-scrollbar pb-10">
                    
                    <div class="px-2 mb-4 mt-1 flex flex-col">
                        <h3 class="text-[10px] font-black uppercase tracking-widest text-white leading-tight">
                            {{ tournamentName }}
                        </h3>
                        <span class="text-[9px] text-gray-500 font-bold uppercase tracking-wider mt-0.5">
                            ID: #{{ tournamentId }}
                        </span>
                    </div>

                    <div v-if="favoriteCountriesTree.length > 0">
                        <div @click="isFavoritesExpanded = !isFavoritesExpanded" class="flex items-center justify-between cursor-pointer mb-2 px-2 hover:opacity-80 select-none">
                            <h3 class="text-[10px] font-black uppercase tracking-widest text-yellow-500 flex items-center gap-2">
                                <Star class="w-3 h-3 fill-yellow-500" /> Favoritos • {{ sportNameTranslated }}
                            </h3>
                            <ChevronDown class="w-3 h-3 text-yellow-500 transition-transform duration-200" :class="isFavoritesExpanded ? 'rotate-180' : ''"/>
                        </div>

                        <div v-if="isFavoritesExpanded" class="space-y-1 mb-6">
                            <div v-for="country in favoriteCountriesTree" :key="'fav-' + country.name" class="rounded overflow-hidden">
                                <div class="flex items-center gap-3 px-3 py-2 rounded hover:bg-yellow-500/10 cursor-pointer text-yellow-100 font-bold text-xs" @click="toggleFavoriteCountry(country.name)">
                                    <img :src="getFlag(country.name === 'Brasil' ? 'Brazil' : country.name)" class="w-4 h-3 rounded-[1px]" />
                                    <span class="flex-1">{{ country.name }}</span>
                                    <ChevronDown class="w-3 h-3 text-yellow-500/50" :class="expandedFavoriteCountries.has(country.name) ? 'rotate-180' : ''"/>
                                </div>
                                <div v-show="expandedFavoriteCountries.has(country.name)" class="bg-[#0f212e]/30 border-l border-yellow-500/20 ml-2">
                                    <a v-for="lg in country.leagues" :key="lg.displayName" @click.prevent="goToLeague(lg.queryName)" class="block pl-3 py-1.5 text-[11px] text-gray-400 hover:text-white hover:bg-white/5 cursor-pointer flex justify-between group">
                                            <span>{{ lg.displayName }}</span>
                                            <button @click.stop="toggleLeagueFavorite(lg.queryName, $event)" class="p-1">
                                                <Star class="w-3 h-3 text-yellow-500 fill-yellow-500 opacity-0 group-hover:opacity-100 transition-opacity" />
                                            </button>
                                    </a>
                                </div>
                            </div>
                        </div>
                        <div class="h-px bg-white/5 w-full mb-4"></div>
                    </div>

                    <div>
                        <h3 class="text-[11px] font-black uppercase tracking-widest mb-4 text-slate-500 pl-2 flex items-center gap-2">
                            <MapPin class="w-3 h-3"/> Países • {{ sportNameTranslated }}
                        </h3>
                        
                        <div class="space-y-1">
                            <div v-for="country in countriesList" :key="country.name" class="rounded overflow-hidden">
                                <div class="flex items-center gap-3 px-3 py-2.5 rounded hover:bg-white/5 cursor-pointer group transition-colors select-none" @click="toggleCountry(country.name)">
                                    <img :src="getFlag(country.name === 'Brasil' ? 'Brazil' : country.name)" class="w-5 h-3.5 object-cover rounded-[2px] opacity-90" />
                                    <span class="text-xs font-bold text-slate-300 group-hover:text-white flex-1 truncate">{{ country.name }}</span>
                                    <ChevronDown class="w-3.5 h-3.5 text-slate-500 group-hover:text-white transition-transform" :class="expandedCountries.has(country.name) ? 'rotate-180' : ''"/>
                                </div>

                                <div v-show="expandedCountries.has(country.name)" class="bg-[#0f212e]/30 border-l-2 border-slate-700 ml-3 my-1">
                                    <div class="flex flex-col">
                                        <a v-for="lg in country.leagues" :key="lg.displayName" 
                                           @click.prevent="goToLeague(lg.queryName)" 
                                           class="pl-4 pr-2 py-2 text-[11px] font-medium text-slate-400 hover:text-white hover:bg-white/5 flex items-center justify-between cursor-pointer transition-colors group/league"
                                           :class="route.query.league === lg.queryName ? 'text-white bg-white/5 border-l-2 border-green-500 -ml-[2px]' : ''">
                                            <span class="truncate flex-1">{{ lg.displayName }}</span>
                                            <button @click.stop="toggleLeagueFavorite(lg.queryName, $event)" class="p-1">
                                                <Star class="w-3.5 h-3.5 transition-colors" :class="favStore.isFavorite(lg.queryName) ? 'text-yellow-400 fill-yellow-400' : 'text-gray-600 opacity-0 group-hover/league:opacity-100 hover:text-yellow-200'" />
                                            </button>
                                        </a>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                </div>
            </transition>

            <div v-if="!loading && countriesList.length === 0" class="pl-2 mt-4">
                 <span v-if="debugInfo" class="text-xs text-red-500/50 italic block">{{ debugInfo }}</span>
                 <span v-else class="text-xs text-slate-500 italic">Nenhum jogo encontrado.</span>
            </div>
        </div>

    </div>

    <div v-else class="p-4 flex flex-col items-center justify-center h-full text-center space-y-4 opacity-50">
        <Trophy class="w-12 h-12 text-slate-600" />
        <p class="text-xs text-slate-500 uppercase font-bold tracking-widest">
            Selecione um torneio<br>para ver as ligas.
        </p>
    </div>

  </aside>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #0f212e; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #2f4553; border-radius: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #557086; }

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.5s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.expand-enter-active,
.expand-leave-active {
  transition: all 0.4s cubic-bezier(0.25, 0.8, 0.5, 1);
  max-height: 2000px; 
  opacity: 1;
}

.expand-enter-from,
.expand-leave-to {
  max-height: 0;
  opacity: 0;
  transform: translateY(-5px);
}
</style>