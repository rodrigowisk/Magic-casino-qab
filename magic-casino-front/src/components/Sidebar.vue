<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { 
  History, CalendarClock, 
  ChevronDown, ChevronRight, MapPin 
} from 'lucide-vue-next';
import SportsService from '../services/SportsService';
import { getFlag } from '../utils/flags'; 
import axios from 'axios'; 

const router = useRouter();
const route = useRoute();

// --- ESTADOS ---
const loading = ref(false);
const countriesList = ref<any[]>([]); 
const expandedCountries = ref<Set<string>>(new Set());

// --- CONTADORES ---
const liveCount = ref(0);
const preMatchCount = ref(0);

// --- LISTA DE PAÍSES ---
const knownCountries = [
    'Brazil', 'Brasil', 'England', 'Spain', 'Italy', 'Germany', 'France', 
    'Portugal', 'Argentina', 'USA', 'Russia', 'Netherlands', 'Turkey', 
    'Mexico', 'Colombia', 'Chile', 'Uruguay', 'Paraguay', 'Peru', 'Bolivia', 
    'Ecuador', 'Venezuela', 'China', 'Japan', 'South Korea', 'Australia'
];

const translateCountry = (name: string) => {
    if (name === 'Brazil') return 'Brasil';
    if (name === 'England') return 'Inglaterra';
    if (name === 'Spain') return 'Espanha';
    if (name === 'Italy') return 'Itália';
    if (name === 'Germany') return 'Alemanha';
    if (name === 'France') return 'França';
    if (name === 'USA') return 'EUA';
    return name;
};

// --- COMPUTED CORRIGIDO ---
const currentSport = computed(() => {
    const param = route.params.id;
    const val = (Array.isArray(param) ? param[0] : param);

    // 1. Se não tiver parametro, assume soccer
    if (!val) return 'soccer';

    // 2. Verifica se é um ID numérico (estamos na tela de detalhes)
    // Se for número, precisamos recuperar o último esporte visto
    if (/^\d+$/.test(val)) {
        if (typeof window !== 'undefined') {
            return localStorage.getItem('lastSelectedSport') || 'soccer';
        }
        return 'soccer';
    }

    // 3. Se for texto (ex: "soccer", "tennis"), salvamos no localStorage e retornamos
    if (typeof window !== 'undefined') {
        localStorage.setItem('lastSelectedSport', val);
    }
    return val;
});

const sportNameTranslated = computed(() => {
    const s = currentSport.value.toLowerCase();
    if (s.includes('soccer')) return 'Futebol';
    if (s.includes('basket')) return 'Basquete';
    if (s.includes('tennis')) return 'Tênis';
    return currentSport.value.toUpperCase();
});

// --- NAVEGAÇÃO ---
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

// --- LÓGICA DE CONTAGEM (LIVE) ---
const fetchLiveCount = async () => {
    try {
        const response = await axios.get('/sportbook/api/LiveEvents');
        if (response.data && Array.isArray(response.data)) {
            liveCount.value = response.data.length;
        } else {
            liveCount.value = 0;
        }
    } catch (error) {
        console.error("Erro ao buscar contagem ao vivo:", error);
        liveCount.value = 0;
    }
};

// --- LÓGICA DE DADOS (PRÉ-JOGO + MENU) ---
const fetchAndGroupLeagues = async () => {
    loading.value = true;
    countriesList.value = [];
    expandedCountries.value.clear();

    try {
        // 🔥 Usa o sport correto (memória ou URL)
        const events = await SportsService.getEvents(currentSport.value, 1, 3000);
        
        // Atualiza o contador com o total real
        preMatchCount.value = events ? events.length : 0;

        if (events && events.length > 0) {
            const groups: Record<string, Set<string>> = {};
            const leagueOriginalNames: Record<string, string> = {};

            events.forEach((event: any) => {
                let rawLeague = event.league || event.League || 'Outros';
                let rawCountry = event.country || event.Country;

                if (!rawCountry || rawCountry === 'Internacional') {
                    const parts = rawLeague.split(' ');
                    const potentialCountry = parts[0]; 
                    if (knownCountries.includes(potentialCountry)) {
                        rawCountry = potentialCountry;
                        rawLeague = parts.slice(1).join(' ').trim();
                        if (!rawLeague) rawLeague = 'Principal'; 
                    } else {
                        rawCountry = 'Internacional';
                    }
                }

                const displayCountry = translateCountry(rawCountry);

                if (!groups[displayCountry]) {
                    groups[displayCountry] = new Set();
                }
                
                groups[displayCountry].add(rawLeague);
                leagueOriginalNames[rawLeague] = event.league || event.League;
            });

            const sortedCountries = Object.keys(groups).sort();
            
            if (sortedCountries.includes('Brasil')) {
                 const idx = sortedCountries.findIndex(c => c === 'Brasil');
                 sortedCountries.unshift(sortedCountries.splice(idx, 1)[0]);
                 expandedCountries.value.add('Brasil');
            }

            countriesList.value = sortedCountries.map(country => ({
                name: country,
                leagues: Array.from(groups[country]).sort().map(lgName => ({
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

// Polling apenas para o Live (mais volátil)
onMounted(() => {
    fetchLiveCount();
    // Atualiza contagem ao vivo a cada 30 segundos
    setInterval(fetchLiveCount, 30000);
});

</script>

<template>
  <aside class="bg-stake-card flex-shrink-0 transition-all duration-300 overflow-y-auto border-r border-stake-dark/50 custom-scrollbar h-full w-full md:w-64">
    <div class="p-4 space-y-6">
        
        <nav class="space-y-1">
            
            <a href="#" 
               @click.prevent="goToLive" 
               class="flex items-center gap-3 px-3 py-2.5 rounded transition-all group cursor-pointer border border-transparent"
               :class="router.currentRoute.value.path === '/live' ? 'bg-stake-hover border-stake-text/10 shadow-sm' : 'hover:bg-stake-hover'"
            >
                <div class="relative flex h-3 w-3 items-center justify-center">
                    <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-500 opacity-75"></span>
                    <span class="relative inline-flex rounded-full h-2 w-2 bg-red-600"></span>
                </div>
                
                <span class="text-sm font-bold flex-1" :class="router.currentRoute.value.path === '/live' ? 'text-white' : 'text-stake-text group-hover:text-white'">Ao Vivo</span>
                
                <span v-if="liveCount > 0" class="text-[10px] font-bold text-white bg-red-500/80 px-1.5 py-0.5 rounded-md min-w-[24px] text-center shadow-sm animate-pulse">
                    {{ liveCount }}
                </span>
            </a>

            <a href="#" 
               @click.prevent="goToPreMatch" 
               class="flex items-center gap-3 px-3 py-2.5 rounded transition-all group cursor-pointer"
               :class="router.currentRoute.value.name === 'sport-events' && !router.currentRoute.value.query.league ? 'bg-stake-hover text-white' : 'hover:bg-stake-hover text-stake-text'"
            >
                <CalendarClock class="w-4 h-4 text-stake-text group-hover:text-white" />
                <span class="text-sm font-semibold group-hover:text-white flex-1">Pré Jogo</span>

                <span v-if="preMatchCount > 0" class="text-[10px] font-bold text-stake-text/70 bg-black/20 px-1.5 py-0.5 rounded-md min-w-[24px] text-center group-hover:text-white group-hover:bg-white/10 transition-colors">
                    {{ preMatchCount }}
                </span>
            </a>

            <a href="#" 
               @click.prevent="goToMyBets" 
               class="flex items-center gap-3 px-3 py-2.5 rounded hover:bg-stake-hover group cursor-pointer text-stake-text transition-all"
               :class="router.currentRoute.value.path === '/minhas-apostas' ? 'bg-stake-hover text-white' : ''"
            >
                <History class="w-4 h-4 text-stake-text group-hover:text-white" />
                <span class="text-sm font-semibold group-hover:text-white">Minhas Apostas</span>
            </a>
        </nav>

        <div class="h-px bg-stake-dark/50 w-full"></div>

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
                               class="pl-4 pr-2 py-2 text-[11px] font-medium text-stake-text hover:text-white hover:bg-white/5 flex items-center gap-2 cursor-pointer transition-colors relative"
                               :class="route.query.league === leagueObj.queryName ? 'text-white bg-white/5 border-l-2 border-green-500 -ml-[2px]' : ''">
                                <span class="truncate">{{ leagueObj.displayName }}</span>
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
</style>