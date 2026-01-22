<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { useRouter } from 'vue-router';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { Radio, AlertCircle, ChevronDown, ChevronRight, Timer, ArrowUp, ArrowDown, Lock } from 'lucide-vue-next';
import apiSports from '../services/apiSports';
import TeamLogo from '../components/TeamLogo.vue';
import { useBetStore, type BetType } from '../stores/useBetStore';

// Importação do Menu Híbrido
import TopSportsMenu from '../components/TopSportsMenu.vue';

// --- ⚙️ CONFIGURAÇÃO DE URL INTELIGENTE (CORREÇÃO DO ERRO 404) ---
const getBaseUrl = () => {
  // Pega a URL do .env (ex: http://localhost:8090/api/sports)
  const envUrl = import.meta.env.VITE_API_URL || 'http://localhost:8888';
  try {
    const url = new URL(envUrl);
    
    // Se estiver apontando direto para o backend (8090), forçamos para o NGINX (8888)
    if (url.port === '8090' && url.hostname === 'localhost') {
        return `${url.protocol}//${url.hostname}:8888`;
    }
    
    // Retorna apenas a origem (http://dominio:porta), removendo "/api/sports"
    return url.origin; 
  } catch {
    return 'http://localhost:8888';
  }
};

const BASE_URL = getBaseUrl();

// --- INTERFACES ---
interface LiveGame {
  gameId: string;
  sportKey: string;
  homeTeam: string;
  awayTeam: string;
  league: string;
  homeTeamLogo?: string | null;
  awayTeamLogo?: string | null;
  commenceTime: string;
  homeScore: number;
  awayScore: number;
  currentMinute: string;
  period: string;
  homeOdd: number;
  drawOdd: number;
  awayOdd: number;
  countryCode?: string;
  // Propriedades de Animação
  homeOddDir?: 'up' | 'down' | null;
  homeOddFlash?: boolean;
  drawOddDir?: 'up' | 'down' | null;
  drawOddFlash?: boolean;
  awayOddDir?: 'up' | 'down' | null;
  awayOddFlash?: boolean;
  [key: string]: any;
}

// --- CONFIGURAÇÃO ---
const router = useRouter();
const betStore = useBetStore();
const loading = ref(true);
const events = ref<LiveGame[]>([]);
const openLeagues = ref<Set<string>>(new Set());
const selectedSport = ref<string>('all');
let connection: HubConnection | null = null;

// --- HELPER DE NORMALIZAÇÃO ---
const getSportCategory = (gameOrKey: LiveGame | string): string => {
    const raw = typeof gameOrKey === 'string' ? gameOrKey : (gameOrKey.sportKey || '');
    const cleanRaw = String(raw).toLowerCase();
    
    if (cleanRaw.includes('soccer')) return 'soccer';
    if (cleanRaw.includes('basket')) return 'basketball';
    if (cleanRaw.includes('tennis')) return 'tennis';
    if (cleanRaw.includes('volley')) return 'volleyball';
    if (cleanRaw.includes('hockey') || cleanRaw.includes('ice')) return 'ice-hockey';
    if (cleanRaw.includes('base')) return 'baseball';
    if (cleanRaw.includes('darts')) return 'darts';
    if (cleanRaw.includes('football') || cleanRaw.includes('americano')) return 'american-football';
    if (cleanRaw.includes('mma') || cleanRaw.includes('ufc')) return 'mma';
    if (cleanRaw.includes('boxing') || cleanRaw.includes('boxe')) return 'boxing';
    if (cleanRaw.includes('esports')) return 'esports';
    
    return cleanRaw || 'other'; 
};

// --- DATA FETCHING ---
const fetchInitialData = async () => {
  try {
    const response = await apiSports.get('/LiveEvents'); 
    if (response.data && Array.isArray(response.data)) {
        events.value = response.data.map((e: any) => {
            // Parse inicial seguro do placar
            let hScore = 0, aScore = 0;
            if (e.score && e.score.includes('-')) {
                const parts = e.score.split('-');
                hScore = parseInt(parts[0]) || 0;
                aScore = parseInt(parts[1]) || 0;
            } else {
                hScore = e.homeScore || 0;
                aScore = e.awayScore || 0;
            }

            return {
                ...e,
                gameId: e.externalId || e.gameId, // Prioriza externalId
                sportKey: e.sportKey || 'soccer',
                league: e.league || 'Ao Vivo',
                currentMinute: e.gameTime || e.currentMinute || '0',
                homeScore: hScore,
                awayScore: aScore,
                homeOdd: e.rawOddsHome ?? e.homeOdd ?? 0,
                drawOdd: e.rawOddsDraw ?? e.drawOdd ?? 0,
                awayOdd: e.rawOddsAway ?? e.awayOdd ?? 0
            };
        });
    } else {
        events.value = [];
    }
    // Abre todas as ligas inicialmente
    events.value.forEach(e => {
        if(e.league) openLeagues.value.add(e.league);
    });
  } catch (e) {
    console.error("Erro na carga inicial:", e);
    events.value = [];
  } finally {
    loading.value = false;
  }
};

const updateOddWithAnimation = (game: LiveGame, field: 'homeOdd' | 'drawOdd' | 'awayOdd', newValue: number) => {
    if (!newValue || Math.abs(newValue - game[field]) < 0.001) return;
    const dirField = `${field}Dir`; 
    const flashField = `${field}Flash`;
    game[dirField] = newValue > game[field] ? 'up' : 'down';
    game[field] = newValue;
    game[flashField] = true;
    setTimeout(() => { game[flashField] = false; }, 1000);
};

// --- WEBSOCKET ---
onMounted(async () => {
  await fetchInitialData();
  
  // URL Corrigida: http://localhost:8888/gameHub
  const signalRUrl = `${BASE_URL}/gameHub`;
  console.log(`📡 [AO VIVO] Conectando SignalR em: ${signalRUrl}`);

  connection = new HubConnectionBuilder()
    .withUrl(signalRUrl) 
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();

  // 1. ATUALIZAÇÃO (LiveOddsUpdate)
  connection.on('LiveOddsUpdate', (updatedGames: any[]) => {
    if (!updatedGames || !Array.isArray(updatedGames)) return;
    
    updatedGames.forEach(update => {
        // Backend manda 'id' que corresponde ao 'gameId' (ExternalId)
        const index = events.value.findIndex(g => g.gameId === update.id); 
        
        if (index !== -1) {
            const game = events.value[index];
            
            // Atualiza Tempo
            if (update.time) game.currentMinute = update.time;
            
            // Atualiza Placar (Parse de "2-1")
            if (update.score && update.score.includes('-')) {
                const parts = update.score.split('-');
                game.homeScore = parseInt(parts[0]) || 0;
                game.awayScore = parseInt(parts[1]) || 0;
            }

            // Atualiza Status/Periodo se disponível
            if (update.status) game.period = update.status;

            // Atualiza Odds com Animação
            if (update.homeOdd) updateOddWithAnimation(game, 'homeOdd', update.homeOdd);
            if (update.drawOdd) updateOddWithAnimation(game, 'drawOdd', update.drawOdd);
            if (update.awayOdd) updateOddWithAnimation(game, 'awayOdd', update.awayOdd);
        }
    });
  });

  // 2. REMOÇÃO (RemoveGames)
  connection.on('RemoveGames', (endedIds: string[]) => {
      if (events.value.length > 0) {
          const initialCount = events.value.length;
          events.value = events.value.filter(e => !endedIds.includes(e.gameId));
          if (events.value.length < initialCount) {
              console.log(`🏁 Removidos ${initialCount - events.value.length} jogos encerrados.`);
          }
      }
  });

  try { 
      await connection.start(); 
      console.log("🟢 Conectado ao SignalR Ao Vivo!"); 
  } catch (err) { 
      console.error("❌ Erro ao conectar SignalR:", err); 
  }
});

onUnmounted(() => {
  if (connection) connection.stop();
});

// --- COMPUTED: DADOS PARA O MENU ---
const liveSportsData = computed(() => {
    const keys = new Set<string>();
    const counts: Record<string, number> = {};

    events.value.forEach(e => {
        const k = getSportCategory(e);
        if (k) {
            keys.add(k);
            counts[k] = (counts[k] || 0) + 1;
        }
    });
    return { keys, counts };
});

const handleMenuSelect = (key: string) => {
    selectedSport.value = key;
};

const filteredEvents = computed(() => {
    if (selectedSport.value === 'all') return events.value;
    return events.value.filter(e => getSportCategory(e) === selectedSport.value);
});

const groupedEvents = computed(() => {
  const groups: Record<string, LiveGame[]> = {};
  filteredEvents.value.forEach(game => {
    const league = game.league || 'Outros';
    if (!groups[league]) groups[league] = [];
    groups[league].push(game);
  });
  return groups;
});

// --- HELPERS GERAIS ---
const isMarketSuspended = (odd: number) => !odd || odd <= 1.01;
const handleSelection = (game: LiveGame, type: BetType) => {
  const price = type === '1' ? game.homeOdd : type === '2' ? game.awayOdd : game.drawOdd;
  if (isMarketSuspended(price)) return; 
  const gameId = game.gameId;
  const currentSelection = betStore.selections.find(s => s.id === gameId);
  if (currentSelection?.type === type) { betStore.removeSelection(gameId); } 
  else {
    betStore.addOrReplaceSelection(gameId, game.homeTeam, game.awayTeam, type === '1' ? game.homeTeam : type === '2' ? game.awayTeam : 'Empate', price, type, game.commenceTime);
  }
};
const goToDetails = (gameId: string) => { router.push({ name: 'event-details', params: { id: gameId } }); };
const handleImageError = (event: Event) => { (event.target as HTMLImageElement).src = '/images/flags/un.svg'; };
const isSelected = (game: LiveGame, type: BetType) => betStore.selections.find(s => s.id === game.gameId)?.type === type;
const getOddValue = (game: LiveGame, type: string) => (type === '1' ? game.homeOdd : type === 'X' ? game.drawOdd : game.awayOdd).toFixed(2);
const getBetTypes = (game: LiveGame) => (game.drawOdd > 0.01) ? ['1', 'X', '2'] : ['1', '2'];
const isNumericTime = (time: string) => time && /\d/.test(time);
const getFlagUrl = (game: LiveGame) => {
  if (game.countryCode) return `/images/flags/${game.countryCode.toLowerCase()}.svg`;
  const leagueName = (game.league || '').toLowerCase();
  const map: Record<string, string> = { brazil: 'br', england: 'gb', spain: 'es', italy: 'it', germany: 'de', france: 'fr', usa: 'us', portugal: 'pt', argentina: 'ar', russia: 'ru', netherlands: 'nl' };
  const found = Object.keys(map).find(c => leagueName.includes(c));
  return found ? `/images/flags/${map[found]}.svg` : '/images/flags/un.svg';
};
const getOddDirection = (game: LiveGame, type: string) => { if (type === '1') return game.homeOddDir; if (type === 'X') return game.drawOddDir; if (type === '2') return game.awayOddDir; return null; };
const getOddFlash = (game: LiveGame, type: string) => { if (type === '1') return game.homeOddFlash; if (type === 'X') return game.drawOddFlash; if (type === '2') return game.awayOddFlash; return false; };
const getOddRaw = (game: LiveGame, type: string) => { if (type === '1') return game.homeOdd; if (type === 'X') return game.drawOdd; if (type === '2') return game.awayOdd; return 0; };
</script>

<template>
  <div class="space-y-0 pb-20 w-full relative">
    
    <TopSportsMenu 
        class="-mx-4 -mt-4 md:-mx-6 md:-mt-6 z-50 sticky top-0"
        :is-live="true" 
        :live-sports="liveSportsData.keys"
        :live-counts="liveSportsData.counts"
        :selected-sport="selectedSport"
        @select="handleMenuSelect" 
    />

    <div class="space-y-4 pt-4">

        <div class="flex items-center gap-2 pb-2 border-b border-stake-dark/50">
            <div class="bg-red-500/10 p-2 rounded-full animate-pulse">
                <Radio class="w-6 h-6 text-red-500" />
            </div>
            <h2 class="text-white text-2xl font-bold uppercase italic tracking-wide">
                AO VIVO
            </h2>
        </div>

        <div v-if="loading" class="text-stake-text animate-pulse pl-2 mt-4">Carregando jogos ao vivo...</div>

        <div v-else class="space-y-4">
            <div v-if="filteredEvents.length === 0" class="py-12 flex flex-col items-center justify-center text-stake-text opacity-50 border border-dashed border-stake-text/20 rounded-lg bg-stake-card/30">
                <AlertCircle class="w-10 h-10 mb-2 opacity-50"/>
                <span class="text-sm font-bold">Nenhum jogo ao vivo nesta categoria.</span>
            </div>
            
            <div v-else v-for="(games, league) in groupedEvents" :key="league" class="rounded overflow-hidden">
                <div @click="openLeagues.has(String(league)) ? openLeagues.delete(String(league)) : openLeagues.add(String(league))" class="bg-stake-card p-3 flex items-center justify-between border-l-4 border-stake-blue cursor-pointer hover:brightness-110 transition-all select-none">
                    <div class="flex items-center gap-3">
                        <img :src="getFlagUrl(games[0])" class="w-5 h-3.5 rounded-sm shadow-sm bg-black/20" @error="handleImageError" />
                        <h3 class="text-white font-bold text-sm uppercase">{{ league }}</h3>
                        <span class="text-xs text-stake-text/60 font-bold bg-black/20 px-2 py-0.5 rounded-full">{{ games.length }}</span>
                    </div>
                    <component :is="openLeagues.has(String(league)) ? ChevronDown : ChevronRight" class="w-5 h-5 text-stake-text" />
                </div>

                <div v-show="openLeagues.has(String(league))" class="bg-stake-dark border-x border-b border-stake-card/30">
                    <div v-for="game in games" :key="game.gameId" class="p-4 border-b border-stake-card/30 flex flex-col md:flex-row items-center gap-4 transition-all relative group hover:bg-[#B6FF00]/[0.05]">
                        
                        <div class="flex flex-col items-center justify-center min-w-[80px]">
                            <div v-if="isNumericTime(game.currentMinute)" class="flex items-center gap-1 text-[#00E701] font-bold mb-1">
                                <Timer class="w-4 h-4 animate-pulse" />
                                <span class="text-sm">{{ game.currentMinute }}'</span>
                            </div>
                            <div v-else class="flex items-center gap-1 text-red-500 font-bold mb-1">
                                <Radio class="w-4 h-4 animate-pulse" />
                                <span class="text-[10px] uppercase">AO VIVO</span>
                            </div>
                            <div class="text-[9px] text-stake-text/70 uppercase font-bold tracking-wider">{{ game.period || 'JOGANDO' }}</div>
                        </div>

                        <div class="flex-1 w-full text-white cursor-pointer" @click="goToDetails(game.gameId)">
                            <div class="flex items-center justify-between mb-2 group-hover:text-stake-blue transition-colors">
                                <div class="flex items-center gap-2">
                                    <TeamLogo :teamName="game.homeTeam" :remoteUrl="game.homeTeamLogo" size="w-5 h-5" />
                                    <span class="font-bold text-sm">{{ game.homeTeam }}</span>
                                </div>
                                <div class="flex items-center gap-2">
                                    <span :class="['font-mono font-bold text-base px-2.5 py-0.5 rounded min-w-[2rem] text-center transition-colors bg-black/20', game.homeScore > 0 ? 'text-yellow-400' : 'text-stake-blue']">{{ game.homeScore }}</span>
                                </div>
                            </div>
                            <div class="flex items-center justify-between group-hover:text-stake-blue transition-colors">
                                <div class="flex items-center gap-2">
                                    <TeamLogo :teamName="game.awayTeam" :remoteUrl="game.awayTeamLogo" size="w-5 h-5" />
                                    <span class="font-bold text-sm">{{ game.awayTeam }}</span>
                                </div>
                                <span :class="['font-mono font-bold text-base px-2.5 py-0.5 rounded min-w-[2rem] text-center transition-colors bg-black/20', game.awayScore > 0 ? 'text-yellow-400' : 'text-stake-blue']">{{ game.awayScore }}</span>
                            </div>
                        </div>

                        <div class="flex gap-2 w-full md:w-auto">
                            <button v-for="type in getBetTypes(game)" :key="type" @click.stop="handleSelection(game, type as BetType)" :disabled="isMarketSuspended(getOddRaw(game, type))"
                                :class="['flex-1 md:w-24 py-2 rounded flex flex-col items-center justify-center border transition-all active:scale-95 group relative overflow-hidden', isMarketSuspended(getOddRaw(game, type)) ? 'bg-stake-card/50 border-transparent opacity-60 cursor-not-allowed' : isSelected(game, type as BetType) ? 'bg-stake-blue border-stake-blue shadow-[0_0_10px_rgba(0,231,1,0.4)]' : 'bg-stake-card border-transparent hover:border-stake-text/30', getOddFlash(game, type) ? 'animate-flash' : '']">
                                <span :class="['text-[10px] font-bold mb-0.5 uppercase tracking-wide', isSelected(game, type as BetType) ? 'text-white' : 'text-stake-text']">{{ type === '1' ? 'Casa' : type === '2' ? 'Fora' : 'Empate' }}</span>
                                <div class="flex items-center gap-1 justify-center min-h-[20px]">
                                    <Lock v-if="isMarketSuspended(getOddRaw(game, type))" class="w-4 h-4 text-stake-text/50" />
                                    <template v-else>
                                        <ArrowUp v-if="getOddDirection(game, type) === 'up'" class="w-3 h-3 text-[#00E701]" />
                                        <ArrowDown v-if="getOddDirection(game, type) === 'down'" class="w-3 h-3 text-red-500" />
                                        <span :class="['text-sm font-black transition-colors', isSelected(game, type as BetType) ? 'text-white' : 'text-white group-hover:scale-110', getOddDirection(game, type) === 'up' ? 'text-[#00E701]' : '', getOddDirection(game, type) === 'down' ? 'text-red-500' : '']">{{ getOddValue(game, type) }}</span>
                                    </template>
                                </div>
                            </button>
                        </div>

                    </div>
                </div>
            </div>
        </div>
    </div>
  </div>
</template>

<style scoped>
@keyframes flash-white {
    0% { background-color: rgba(255, 255, 255, 0.8); border-color: white; }
    100% { background-color: inherit; border-color: inherit; }
}
.animate-flash { animation: flash-white 0.8s ease-out; }
</style>