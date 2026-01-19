<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { 
  Gamepad2, Clock, Star, Trophy, ChevronDown, ChevronRight 
} from 'lucide-vue-next';
import SportsService from '../services/SportsService';

// ✅ IMPORTAÇÃO DA FUNÇÃO INTELIGENTE DE BANDEIRAS
import { getFlag } from '../utils/flags'; 

const router = useRouter();
const sportsList = ref<any[]>([]);
const expandedSports = ref<Set<string>>(new Set());
const leaguesCache = ref<Record<string, string[]>>({});
const loadingLeagues = ref<Record<string, boolean>>({});

const goToSport = (key: string) => { router.push({ name: 'sport-events', params: { id: key } }); };
const goToLeague = (sportKey: string, leagueName: string) => { 
  router.push({ name: 'sport-events', params: { id: sportKey }, query: { league: leagueName } }); 
};

// Mapeamento visual dos esportes
const mapVisuals = (key: string) => {
    const k = key.toLowerCase();
    if (k.includes('soccer')) return { name: 'Futebol', icon: '⚽' };
    if (k.includes('basket')) return { name: 'Basquete', icon: '🏀' };
    if (k.includes('tennis')) return { name: 'Tênis', icon: '🎾' };
    if (k.includes('boxing')) return { name: 'Boxe', icon: '🥊' };
    if (k.includes('mma') || k.includes('ufc')) return { name: 'MMA', icon: '🥋' };
    if (k.includes('american')) return { name: 'Futebol Americano', icon: '🏈' };
    if (k.includes('hockey')) return { name: 'Hóquei', icon: '🏒' };
    if (k.includes('esports')) return { name: 'E-Sports', icon: '🎮' };
    if (k.includes('cricket')) return { name: 'Críquete', icon: '🏏' };
    if (k.includes('baseball')) return { name: 'Beisebol', icon: '⚾' };
    if (k.includes('volleyball') || k.includes('volei')) return { name: 'Vôlei', icon: '🏐' };
    if (k.includes('darts')) return { name: 'Dardos', icon: '🎯' };
    
    return { name: key, icon: '🏆' };
};

const toggleSportAccordion = async (sportKey: string) => {
    if (expandedSports.value.has(sportKey)) { expandedSports.value.delete(sportKey); return; }
    expandedSports.value.add(sportKey);
    if (leaguesCache.value[sportKey]) return;
    try {
        loadingLeagues.value[sportKey] = true;
        const events = await SportsService.getEventsBySport(sportKey);
        // Filtra ligas únicas
        const uniqueLeagues = [...new Set(events.map((e: any) => e.league))] as string[];
        leaguesCache.value[sportKey] = uniqueLeagues.sort(); 
    } catch (e) { console.error(e); } finally { loadingLeagues.value[sportKey] = false; }
};

onMounted(async () => {
    try {
        const data = await SportsService.getActiveSports();
        
        // Agrupa os esportes da API
        const grouped = data.reduce((acc: any, item: any) => {
            // Ignora esports se quiser limpar a lista
            if (item.key.includes('esport')) return acc; 

            const visual = mapVisuals(item.key);
            const sportName = visual.name;

            if (!acc[sportName]) {
                acc[sportName] = { 
                    realKey: item.key, 
                    name: sportName, 
                    icon: visual.icon, 
                    count: 0 
                };
            }
            acc[sportName].count += item.count;
            return acc;
        }, {});

        // Ordena por contagem (opcional) ou nome
        sportsList.value = Object.values(grouped).sort((a: any, b: any) => b.count - a.count);
    } catch (e) { console.error(e); }
});
</script>

<template>
  <aside class="bg-stake-card flex-shrink-0 transition-all duration-300 overflow-y-auto border-r border-stake-dark/50 custom-scrollbar h-full">
    <div class="p-4 space-y-6">
        
        <div class="bg-stake-dark p-1 rounded-full flex text-xs font-bold">
            <button class="flex-1 py-2 rounded-full text-center text-stake-text hover:bg-stake-hover">Cassino</button>
            <button class="flex-1 py-2 rounded-full text-center bg-stake-hover text-white shadow">Esportes</button>
        </div>

        <nav class="space-y-1">
            <a href="#" 
               @click.prevent="router.push('/live')" 
               class="flex items-center gap-3 px-3 py-2 rounded transition-colors group cursor-pointer"
               :class="router.currentRoute.value.path === '/live' ? 'bg-stake-hover text-white' : 'hover:bg-stake-hover text-stake-text'"
            >
                <Gamepad2 class="w-4 h-4" :class="router.currentRoute.value.path === '/live' ? 'text-white' : 'text-stake-text group-hover:text-white'" />
                <span class="text-sm font-semibold" :class="router.currentRoute.value.path === '/live' ? 'text-white' : 'text-stake-text group-hover:text-white'">Ao Vivo</span>
                <span class="ml-auto bg-green-500 text-stake-dark text-[10px] font-bold px-1.5 rounded animate-pulse">LIVE</span>
            </a>

            <a href="#" @click.prevent="router.push('/minhas-apostas')" class="flex items-center gap-3 px-3 py-2 rounded hover:bg-stake-hover group text-white">
                <Clock class="w-4 h-4 text-stake-text group-hover:text-white" />
                <span class="text-sm font-semibold">Meu Histórico</span>
            </a>
            <a href="#" class="flex items-center gap-3 px-3 py-2 rounded hover:bg-stake-hover group text-white">
                <Star class="w-4 h-4 text-stake-text group-hover:text-white" />
                <span class="text-sm font-semibold">Favoritos</span>
            </a>
        </nav>

        <div>
            <h3 class="text-xs font-bold uppercase tracking-wider mb-3 text-stake-text/60 pl-2 flex items-center gap-2">
                <Trophy class="w-3 h-3"/> Esportes
            </h3>
            
            <div v-if="sportsList.length === 0" class="pl-4 text-xs text-stake-text animate-pulse">Carregando...</div>
            
            <div v-else class="space-y-1">
                <div v-for="sport in sportsList" :key="sport.realKey" class="rounded overflow-hidden">
                    
                    <div class="flex items-center gap-3 px-3 py-2 rounded hover:bg-stake-hover cursor-pointer group" @click="goToSport(sport.realKey)">
                        <span class="text-lg w-5 text-center">{{ sport.icon }}</span> 
                        <span class="text-sm font-medium text-white group-hover:text-white/90 flex-1">{{ sport.name }}</span>
                        <span class="text-xs text-stake-text/50 bg-stake-dark px-1.5 rounded mr-2">{{ sport.count }}</span>
                        <div @click.stop="toggleSportAccordion(sport.realKey)" class="p-1 rounded hover:bg-white/10">
                            <ChevronDown v-if="expandedSports.has(sport.realKey)" class="w-4 h-4" />
                            <ChevronRight v-else class="w-4 h-4" />
                        </div>
                    </div>

                    <div v-if="expandedSports.has(sport.realKey)" class="bg-black/20 pb-2">
                        <div v-if="loadingLeagues[sport.realKey]" class="px-9 py-2 text-xs text-stake-text animate-pulse">Buscando ligas...</div>
                        <div v-else class="flex flex-col">
                            <a v-for="league in leaguesCache[sport.realKey]" :key="league" @click.prevent="goToLeague(sport.realKey, league)" 
                               class="pl-12 pr-2 py-2 text-xs text-stake-text hover:text-white hover:bg-white/5 flex items-center gap-3 cursor-pointer group">
                                
                                <img :src="getFlag(league)" class="w-4 h-2.5 object-cover rounded-sm opacity-80 group-hover:opacity-100 shadow-sm flex-shrink-0" />
                                
                                <span class="truncate capitalize flex-1 text-left">{{ league }}</span>
                            </a>
                        </div>
                    </div>

                </div>
            </div>
        </div>
    </div>
  </aside>
</template>