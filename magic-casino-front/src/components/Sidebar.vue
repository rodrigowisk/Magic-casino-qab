<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { 
  History, CalendarClock, 
  ChevronDown, MapPin, Star 
} from 'lucide-vue-next';
import SportsService from '../services/SportsService';
import { getFlag } from '../utils/flags'; 
import axios from 'axios'; 

// ✅ IMPORTA A NOVA FUNÇÃO DE TRADUÇÃO
import { normalizeCountryName } from '../utils/countryTranslations';

// ✅ IMPORTA A STORE DE FAVORITOS
import { useFavoritesStore } from '../stores/useFavoritesStore';

const router = useRouter();
const route = useRoute();
const favStore = useFavoritesStore(); // Instância da Store

// --- ESTADOS ---
const loading = ref(false);
const countriesList = ref<any[]>([]); 
const expandedCountries = ref<Set<string>>(new Set());

// --- ESTADOS FAVORITOS ---
// Removido 'favoriteLeagues' local, agora usamos favStore.favoriteLeagues
const isFavoritesExpanded = ref(true); 
const expandedFavoriteCountries = ref<Set<string>>(new Set()); 

// --- CONTADORES ---
const liveCount = ref(0);
const preMatchCount = ref(0);

// --- COMPUTED ---
const currentSport = computed(() => {
    const param = route.params.id;
    const val = (Array.isArray(param) ? param[0] : param);
    if (!val) return 'soccer';
    if (/^\d+$/.test(val)) {
        if (typeof window !== 'undefined') return localStorage.getItem('lastSelectedSport') || 'soccer';
        return 'soccer';
    }
    if (typeof window !== 'undefined') localStorage.setItem('lastSelectedSport', val);
    return val;
});

const sportNameTranslated = computed(() => {
    const s = currentSport.value.toLowerCase();
    if (s.includes('soccer')) return 'Futebol';
    if (s.includes('basket')) return 'Basquete';
    if (s.includes('tennis')) return 'Tênis';
    return currentSport.value.toUpperCase();
});

// --- LÓGICA DE FAVORITOS (COMPUTED) ---
const favoriteCountriesTree = computed(() => {
    // Observa favStore.favoriteLeagues para reagividade global
    if (countriesList.value.length === 0 || favStore.favoriteLeagues.size === 0) return [];
    
    const tree = countriesList.value.map(country => {
        // Usa a Store para filtrar
        const favLeagues = country.leagues.filter((lg: any) => favStore.isFavorite(lg.queryName));
        if (favLeagues.length > 0) {
            return { ...country, leagues: favLeagues };
        }
        return null;
    }).filter(c => c !== null); 
    return tree;
});

// --- AÇÕES ---
const goToLive = () => router.push('/live');
const goToPreMatch = () => router.push({ name: 'sport-events', params: { id: currentSport.value } });
const goToMyBets = () => router.push('/minhas-apostas');

const goToLeague = (leagueName: string) => { 
  router.push({ 
      name: 'sport-events', 
      params: { id: currentSport.value }, 
      query: { league: leagueName } 
  }); 
};

const toggleCountry = (countryName: string) => {
    if (expandedCountries.value.has(countryName)) {
        expandedCountries.value.delete(countryName);
    } else {
        expandedCountries.value.add(countryName);
    }
};

const toggleFavoriteCountry = (countryName: string) => {
    if (expandedFavoriteCountries.value.has(countryName)) {
        expandedFavoriteCountries.value.delete(countryName);
    } else {
        expandedFavoriteCountries.value.add(countryName);
    }
};

// ✅ AÇÃO CENTRALIZADA NA STORE
const toggleLeagueFavorite = (leagueName: string, event: Event) => {
    event.stopPropagation(); 
    favStore.toggleFavorite(leagueName, currentSport.value);
};

const fetchLiveCount = async () => {
    try {
        const response = await axios.get('/sportbook/api/LiveEvents');
        if (response.data && Array.isArray(response.data)) {
            liveCount.value = response.data.length;
        } else {
            liveCount.value = 0;
        }
    } catch (error) { liveCount.value = 0; }
};

// --- NOVA LÓGICA DE DADOS USANDO O ARQUIVO DE TRADUÇÃO ---
const fetchAndGroupLeagues = async () => {
    loading.value = true;
    countriesList.value = [];
    expandedCountries.value.clear();

    try {
        const events = await SportsService.getEvents(currentSport.value, 1, 3000);
        preMatchCount.value = events ? events.length : 0;

        if (events && events.length > 0) {
            const groups: Record<string, Set<string>> = {};
            const leagueOriginalNames: Record<string, string> = {};

            events.forEach((event: any) => {
                const rawLeague = event.league || event.League || 'Outros';
                const rawCountry = event.country || event.Country || 'Internacional';

                // ✅ USANDO NORMALIZAÇÃO
                const { country: displayCountry, league: cleanLeague } = normalizeCountryName(rawCountry, rawLeague);

                if (!groups[displayCountry]) {
                    groups[displayCountry] = new Set();
                }
                
                groups[displayCountry].add(cleanLeague);
                // Mantemos o nome original para query, mas usamos o limpo para display
                leagueOriginalNames[cleanLeague] = rawLeague; 
            });

            const sortedCountries = Object.keys(groups).sort();
            
            // Coloca Brasil no topo
            if (sortedCountries.includes('Brasil')) {
                 const idx = sortedCountries.findIndex(c => c === 'Brasil');
                 if (idx !== -1) {
                     const removed = sortedCountries.splice(idx, 1)[0];
                     if (removed) sortedCountries.unshift(removed);
                 }
                 expandedCountries.value.add('Brasil');
                 expandedFavoriteCountries.value.add('Brasil');
            }

            countriesList.value = sortedCountries.map(country => ({
                name: country,
                leagues: Array.from(groups[country] || []).sort().map(lgName => ({
                    displayName: lgName,
                    queryName: leagueOriginalNames[lgName] || lgName
                }))
            }));
        }

    } catch (e) {
        console.error("Erro ao carregar menu lateral:", e);
    } finally {
        loading.value = false;
    }
};

watch(currentSport, () => {
    fetchAndGroupLeagues();
    fetchLiveCount(); 
}, { immediate: true });

onMounted(() => {
    // ✅ INICIALIZA A STORE
    favStore.fetchFavorites();
    
    fetchLiveCount();
    setInterval(fetchLiveCount, 30000);
});
</script>

<template>
  <aside class="bg-stake-card flex-shrink-0 transition-all duration-300 overflow-y-auto border-r border-stake-dark/50 custom-scrollbar h-full w-full md:w-64">
    <div class="p-4 space-y-6">
        
        <nav class="space-y-1">
            <a href="#" @click.prevent="goToLive" class="flex items-center gap-3 px-3 py-2.5 rounded transition-all group cursor-pointer border border-transparent" :class="router.currentRoute.value.path === '/live' ? 'bg-stake-hover border-stake-text/10 shadow-sm' : 'hover:bg-stake-hover'">
                <div class="relative flex h-3 w-3 items-center justify-center">
                    <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-500 opacity-75"></span>
                    <span class="relative inline-flex rounded-full h-2 w-2 bg-red-600"></span>
                </div>
                <span class="text-sm font-bold flex-1" :class="router.currentRoute.value.path === '/live' ? 'text-white' : 'text-stake-text group-hover:text-white'">Ao Vivo</span>
                <span v-if="liveCount > 0" class="text-[10px] font-bold text-white bg-red-500/80 px-1.5 py-0.5 rounded-md min-w-[24px] text-center shadow-sm animate-pulse">{{ liveCount }}</span>
            </a>

            <a href="#" @click.prevent="goToPreMatch" class="flex items-center gap-3 px-3 py-2.5 rounded transition-all group cursor-pointer" :class="router.currentRoute.value.name === 'sport-events' && !router.currentRoute.value.query.league ? 'bg-stake-hover text-white' : 'hover:bg-stake-hover text-stake-text'">
                <CalendarClock class="w-4 h-4 text-stake-text group-hover:text-white" />
                <span class="text-sm font-semibold group-hover:text-white flex-1">Pré Jogo</span>
                <span v-if="preMatchCount > 0" class="text-[10px] font-bold text-stake-text/70 bg-black/20 px-1.5 py-0.5 rounded-md min-w-[24px] text-center group-hover:text-white group-hover:bg-white/10 transition-colors">{{ preMatchCount }}</span>
            </a>

            <a href="#" @click.prevent="goToMyBets" class="flex items-center gap-3 px-3 py-2.5 rounded hover:bg-stake-hover group cursor-pointer text-stake-text transition-all" :class="router.currentRoute.value.path === '/minhas-apostas' ? 'bg-stake-hover text-white' : ''">
                <History class="w-4 h-4 text-stake-text group-hover:text-white" />
                <span class="text-sm font-semibold group-hover:text-white">Minhas Apostas</span>
            </a>
        </nav>

        <div class="h-px bg-stake-dark/50 w-full"></div>

        <div v-if="favoriteCountriesTree.length > 0">
            <div 
                @click="isFavoritesExpanded = !isFavoritesExpanded"
                class="flex items-center justify-between cursor-pointer mb-2 px-2 hover:opacity-80 select-none"
            >
                <h3 class="text-[10px] font-black uppercase tracking-widest text-yellow-500 flex items-center gap-2">
                    <Star class="w-3 h-3 fill-yellow-500" /> Favoritos • {{ sportNameTranslated }}
                </h3>
                <ChevronDown class="w-3 h-3 text-yellow-500 transition-transform duration-200" :class="isFavoritesExpanded ? 'rotate-180' : ''"/>
            </div>

            <div v-if="isFavoritesExpanded" class="space-y-1 animate-fade-in mb-6">
                <div v-for="country in favoriteCountriesTree" :key="'fav-group-' + country.name" class="rounded overflow-hidden">
                    
                    <div class="flex items-center gap-3 px-3 py-2.5 rounded hover:bg-yellow-500/10 cursor-pointer group transition-colors select-none" 
                        :class="expandedFavoriteCountries.has(country.name) ? 'bg-yellow-500/5' : ''"
                        @click="toggleFavoriteCountry(country.name)">
                        
                        <img :src="getFlag(country.name === 'Brasil' ? 'Brazil' : country.name)" 
                            class="w-5 h-3.5 object-cover rounded-[2px] shadow-sm opacity-90 group-hover:opacity-100 transition-opacity" />
                        
                        <span class="text-xs font-bold text-yellow-100/90 group-hover:text-white flex-1 truncate">
                            {{ country.name }}
                        </span>
                        
                        <div class="text-yellow-500/50 group-hover:text-yellow-400 transition-transform duration-200" 
                             :class="expandedFavoriteCountries.has(country.name) ? 'rotate-180' : ''">
                            <ChevronDown class="w-3.5 h-3.5" />
                        </div>
                    </div>

                    <div v-show="expandedFavoriteCountries.has(country.name)" class="bg-[#0f212e]/30 border-l-2 border-yellow-500/30 ml-3 my-1">
                        <div class="flex flex-col">
                            <a v-for="leagueObj in country.leagues" :key="'fav-item-' + leagueObj.displayName" 
                               @click.prevent="goToLeague(leagueObj.queryName)" 
                               class="pl-4 pr-2 py-2 text-[11px] font-medium text-stake-text hover:text-white hover:bg-white/5 flex items-center justify-between cursor-pointer transition-colors relative group/league"
                               :class="route.query.league === leagueObj.queryName ? 'text-white bg-white/5' : ''">
                                
                                <span class="truncate flex-1">{{ leagueObj.displayName }}</span>

                                <button 
                                    @click.stop="toggleLeagueFavorite(leagueObj.queryName, $event)"
                                    class="p-1 rounded transition-all transform active:scale-95"
                                >
                                    <Star 
                                        class="w-3.5 h-3.5 text-yellow-400 fill-yellow-400 hover:scale-110 transition-transform" 
                                    />
                                </button>

                            </a>
                        </div>
                    </div>

                </div>
            </div>
            
            <div class="h-px bg-stake-dark/50 w-full mb-4"></div>
        </div>

        <div>
            <h3 class="text-[11px] font-black uppercase tracking-widest mb-4 text-stake-text/50 pl-2 flex items-center gap-2">
                <MapPin class="w-3 h-3"/> Países • {{ sportNameTranslated }}
            </h3>
            
            <div v-if="loading" class="pl-4 space-y-3">
                <div v-for="i in 5" :key="i" class="h-4 bg-stake-hover/50 rounded animate-pulse w-3/4"></div>
            </div>
            
            <div v-else-if="countriesList.length === 0" class="pl-4 text-xs text-stake-text italic">
                Nenhuma liga encontrada.
            </div>
            
            <div v-else class="space-y-1">
                <div v-for="country in countriesList" :key="country.name" class="rounded overflow-hidden">
                    
                    <div class="flex items-center gap-3 px-3 py-2.5 rounded hover:bg-stake-hover cursor-pointer group transition-colors select-none" 
                        @click="toggleCountry(country.name)">
                        
                        <img :src="getFlag(country.name === 'Brasil' ? 'Brazil' : country.name)" 
                            class="w-5 h-3.5 object-cover rounded-[2px] shadow-sm opacity-90 group-hover:opacity-100 transition-opacity" />
                        
                        <span class="text-xs font-bold text-stake-text group-hover:text-white flex-1 truncate">
                            {{ country.name }}
                        </span>
                        
                        <div class="text-stake-text/50 group-hover:text-white transition-transform duration-200" 
                             :class="expandedCountries.has(country.name) ? 'rotate-180' : ''">
                            <ChevronDown class="w-3.5 h-3.5" />
                        </div>
                    </div>

                    <div v-show="expandedCountries.has(country.name)" class="bg-[#0f212e]/30 border-l-2 border-stake-dark ml-3 my-1">
                        <div class="flex flex-col">
                            <a v-for="leagueObj in country.leagues" :key="leagueObj.displayName" 
                               @click.prevent="goToLeague(leagueObj.queryName)" 
                               class="pl-4 pr-2 py-2 text-[11px] font-medium text-stake-text hover:text-white hover:bg-white/5 flex items-center justify-between cursor-pointer transition-colors relative group/league"
                               :class="route.query.league === leagueObj.queryName ? 'text-white bg-white/5 border-l-2 border-green-500 -ml-[2px]' : ''">
                                
                                <span class="truncate flex-1">{{ leagueObj.displayName }}</span>

                                <button 
                                    @click.stop="toggleLeagueFavorite(leagueObj.queryName, $event)"
                                    class="p-1 rounded transition-all transform active:scale-95"
                                >
                                    <Star 
                                        class="w-3.5 h-3.5 transition-colors duration-200" 
                                        :class="favStore.isFavorite(leagueObj.queryName) 
                                            ? 'text-yellow-400 fill-yellow-400' 
                                            : 'text-gray-600 fill-transparent opacity-0 group-hover/league:opacity-100 hover:text-yellow-200'"
                                    />
                                </button>

                            </a>
                        </div>
                    </div>

                </div>
            </div>
        </div>
    </div>
  </aside>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #0f212e; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #2f4553; border-radius: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #557086; }

.animate-fade-in {
    animation: fadeIn 0.3s ease-in-out;
}
@keyframes fadeIn {
    from { opacity: 0; transform: translateY(-5px); }
    to { opacity: 1; transform: translateY(0); }
}
</style>