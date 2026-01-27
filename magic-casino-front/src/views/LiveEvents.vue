<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { useRouter } from 'vue-router';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { Radio, AlertCircle, ChevronDown, ChevronRight, ArrowUp, ArrowDown, Lock } from 'lucide-vue-next';
import axios from 'axios'; 
import TeamLogo from '../components/TeamLogo.vue';
import { useBetStore, type BetType } from '../stores/useBetStore';

// Importação do Menu Híbrido
import TopSportsMenu from '../components/TopSportsMenu.vue';

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

// --- NORMALIZAÇÃO ---
const normalizeGame = (e: any): LiveGame => {
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
        gameId: e.externalId || e.gameId,
        sportKey: e.sportKey || 'soccer',
        league: e.league || 'Ao Vivo',
        currentMinute: e.gameTime || e.currentMinute || '0',
        homeScore: hScore,
        awayScore: aScore,
        homeOdd: e.rawOddsHome ?? e.homeOdd ?? 0,
        drawOdd: e.rawOddsDraw ?? e.drawOdd ?? 0,
        awayOdd: e.rawOddsAway ?? e.awayOdd ?? 0,
        period: e.status || e.period || 'Live'
    };
};

// --- DATA FETCHING ---
const fetchInitialData = async () => {
  try {
    const response = await axios.get('/sportbook/api/LiveEvents');
    if (response.data && Array.isArray(response.data)) {
        events.value = response.data.map(normalizeGame);
    } else {
        events.value = [];
    }
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
  
  const signalRUrl = "/gameHub";
  connection = new HubConnectionBuilder()
    .withUrl(signalRUrl) 
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();

  connection.on('LiveOddsUpdate', (updatedGames: any[]) => {
    if (!updatedGames || !Array.isArray(updatedGames)) return;
    updatedGames.forEach(update => {
        const index = events.value.findIndex(g => g.gameId === update.id); 
        if (index !== -1) {
            const game = events.value[index];
            if (!game) return; 
            if (update.time) game.currentMinute = update.time;
            if (update.score && update.score.includes('-')) {
                const parts = update.score.split('-');
                game.homeScore = parseInt(parts[0]) || 0;
                game.awayScore = parseInt(parts[1]) || 0;
            }
            if (update.status) game.period = update.status;
            if (update.homeOdd) updateOddWithAnimation(game, 'homeOdd', update.homeOdd);
            if (update.drawOdd) updateOddWithAnimation(game, 'drawOdd', update.drawOdd);
            if (update.awayOdd) updateOddWithAnimation(game, 'awayOdd', update.awayOdd);
        }
    });
  });

  connection.on('GameWentLive', (newGames: any[]) => {
      if (!newGames || !Array.isArray(newGames)) return;
      newGames.forEach(rawGame => {
          const newGame = normalizeGame(rawGame);
          const exists = events.value.some(g => g.gameId === newGame.gameId);
          if (!exists) {
              events.value.push(newGame);
              if (newGame.league) openLeagues.value.add(newGame.league);
          }
      });
  });

  connection.on('RemoveGames', (endedIds: string[]) => {
      if (events.value.length > 0) {
          events.value = events.value.filter(e => !endedIds.includes(e.gameId));
      }
  });

  try { await connection.start(); } catch (err) { console.error("❌ Erro SignalR:", err); }
});

onUnmounted(() => { if (connection) connection.stop(); });

// --- COMPUTED ---
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

const handleMenuSelect = (key: string) => { selectedSport.value = key; };

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

// --- HELPERS ---
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
const getBetTypes = (game: LiveGame) => {
    const sport = getSportCategory(game);
    if (sport === 'soccer') { return ['1', 'X', '2']; }
    return ['1', '2'];
};
const isNumericTime = (time: string) => time && /\d/.test(time);
const getFlagUrl = (game: LiveGame | undefined) => {
  if (!game) return '/images/flags/un.svg'; 
  if (game.countryCode) return `/images/flags/${game.countryCode.toLowerCase()}.svg`;
  return '/images/flags/un.svg';
};
const getOddDirection = (game: LiveGame, type: string) => { if (type === '1') return game.homeOddDir; if (type === 'X') return game.drawOddDir; if (type === '2') return game.awayOddDir; return null; };
const getOddFlash = (game: LiveGame, type: string) => { if (type === '1') return game.homeOddFlash; if (type === 'X') return game.drawOddFlash; if (type === '2') return game.awayOddFlash; return false; };
const getOddRaw = (game: LiveGame, type: string) => { if (type === '1') return game.homeOdd; if (type === 'X') return game.drawOdd; if (type === '2') return game.awayOdd; return 0; };
const getLeagueSelectionCount = (games: LiveGame[]) => {
    return games.reduce((count, game) => {
        return count + (betStore.selections.some(s => s.id === game.gameId) ? 1 : 0);
    }, 0);
};
</script>

<template>
  <div class="space-y-0 pb-20 w-full relative">
    
    <TopSportsMenu 
        class="w-full z-50 sticky top-0 bg-[#0f172a] shadow-lg border-b border-white/5"
        :is-live="true" 
        :live-sports="liveSportsData.keys"
        :selected-sport="selectedSport"
        @select="handleMenuSelect" 
    />

    <div class="space-y-4 p-4 md:p-6">

        <div class="flex items-center gap-2 pb-2 border-b border-stake-dark/50">
            <div class="bg-red-500/10 p-1.5 rounded-full animate-pulse">
                <Radio class="w-5 h-5 text-red-500" />
            </div>
            <h2 class="text-white text-lg font-bold uppercase italic tracking-wide">
                Jogos Ao Vivo
            </h2>
        </div>

        <div v-if="loading" class="text-stake-text animate-pulse pl-2 mt-4 text-sm">Carregando jogos ao vivo...</div>

        <div v-else class="space-y-3">
            <div v-if="filteredEvents.length === 0" class="py-8 flex flex-col items-center justify-center text-stake-text opacity-50 border border-dashed border-stake-text/20 rounded-lg bg-stake-card/30">
                <AlertCircle class="w-8 h-8 mb-2 opacity-50"/>
                <span class="text-xs font-bold">Nenhum jogo ao vivo nesta categoria.</span>
            </div>
            
            <div v-else v-for="(games, league) in groupedEvents" :key="league" class="rounded overflow-hidden">
                <div @click="openLeagues.has(String(league)) ? openLeagues.delete(String(league)) : openLeagues.add(String(league))" class="bg-stake-card/60 backdrop-blur-sm p-2 flex items-center justify-between border-l-2 border-stake-blue cursor-pointer hover:bg-stake-card transition-all select-none">
                    <div class="flex items-center gap-2">
                        <img :src="getFlagUrl(games[0])" class="w-4 h-3 rounded-[1px] shadow-sm" @error="handleImageError" />
                        <h3 class="text-white font-bold text-xs uppercase tracking-wide">{{ league }}</h3>
                        
                        <span v-if="getLeagueSelectionCount(games) > 0" class="text-[10px] text-black font-bold bg-yellow-400 px-1.5 py-0.5 rounded-full shadow-sm animate-pulse">
                            {{ getLeagueSelectionCount(games) }}
                        </span>
                    </div>
                    <component :is="openLeagues.has(String(league)) ? ChevronDown : ChevronRight" class="w-4 h-4 text-stake-text" />
                </div>

                <div v-show="openLeagues.has(String(league))" class="bg-stake-dark border-x border-b border-stake-card/20">
                    <div v-for="game in games" :key="game.gameId" class="py-2 px-2 border-b border-white/5 flex flex-col md:flex-row items-center gap-2 transition-all relative group hover:bg-[#B6FF00]/[0.02]">
                        
                        <div class="flex flex-row md:flex-col items-center justify-start md:justify-center gap-2 md:gap-0.5 min-w-[60px] md:w-[60px] text-left md:text-center mr-2 md:mr-0 border-r md:border-r-0 md:border-b-0 border-white/10 pr-2 md:pr-0 h-full">
                            
                            <div v-if="isNumericTime(game.currentMinute)" class="text-[#00E701] font-bold text-xs">
                                {{ game.currentMinute }}'
                            </div>
                            <div v-else class="text-[#00E701] font-bold text-[10px] uppercase">
                                LIVE
                            </div>

                            <div class="flex items-center gap-1.5 justify-center">
                                <span class="relative flex h-1.5 w-1.5">
                                  <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-500 opacity-75"></span>
                                  <span class="relative inline-flex rounded-full h-1.5 w-1.5 bg-red-500"></span>
                                </span>
                                <div class="text-[9px] text-stake-text/60 uppercase font-bold tracking-wider hidden md:block">
                                    {{ game.period || 'VIVO' }}
                                </div>
                            </div>
                        </div>

                        <div class="flex-1 w-full text-white cursor-pointer md:border-l md:border-white/5 md:pl-3" @click="goToDetails(game.gameId)">
                            <div class="flex flex-col gap-1.5 justify-center h-full">
                                <div class="flex items-center gap-2">
                                    <span :class="['font-mono font-bold text-xs w-5 text-center', game.homeScore > 0 ? 'text-white' : 'text-white/40']">{{ game.homeScore }}</span>
                                    <TeamLogo :teamName="game.homeTeam" :remoteUrl="game.homeTeamLogo" size="w-4 h-4" />
                                    <span class="font-medium text-xs text-white/90 truncate">{{ game.homeTeam }}</span>
                                </div>
                                <div class="flex items-center gap-2">
                                    <span :class="['font-mono font-bold text-xs w-5 text-center', game.awayScore > 0 ? 'text-white' : 'text-white/40']">{{ game.awayScore }}</span>
                                    <TeamLogo :teamName="game.awayTeam" :remoteUrl="game.awayTeamLogo" size="w-4 h-4" />
                                    <span class="font-medium text-xs text-white/90 truncate">{{ game.awayTeam }}</span>
                                </div>
                            </div>
                        </div>

                        <div class="flex gap-1 w-full md:w-auto mt-2 md:mt-0">
                            <button v-for="type in getBetTypes(game)" :key="type" @click.stop="handleSelection(game, type as BetType)" :disabled="isMarketSuspended(getOddRaw(game, type))"
                                :class="['flex-1 md:w-[70px] h-auto py-1.5 rounded-sm flex flex-col items-center justify-center border border-transparent transition-all active:scale-95 group relative overflow-hidden', 
                                isMarketSuspended(getOddRaw(game, type)) ? 'bg-stake-card/30 opacity-50 cursor-not-allowed' : isSelected(game, type as BetType) ? 'bg-stake-blue shadow-[0_0_8px_rgba(0,146,255,0.4)]' : 'bg-stake-card hover:bg-stake-card/80', 
                                getOddFlash(game, type) ? 'animate-flash' : '']">
                                
                                <span :class="['text-[9px] font-bold uppercase mb-0.5 tracking-wide', isSelected(game, type as BetType) ? 'text-white' : 'text-stake-text/70']">
                                    {{ type === '1' ? 'Casa' : type === '2' ? 'Fora' : 'Empate' }}
                                </span>

                                <div class="flex items-center gap-1 justify-center w-full">
                                    <Lock v-if="isMarketSuspended(getOddRaw(game, type))" class="w-3 h-3 text-stake-text/50" />
                                    <template v-else>
                                         <ArrowUp v-if="getOddDirection(game, type) === 'up'" class="w-2.5 h-2.5 text-[#00E701]" />
                                         <ArrowDown v-if="getOddDirection(game, type) === 'down'" class="w-2.5 h-2.5 text-red-500" />
                                         <span :class="['text-xs font-bold transition-colors leading-none', 
                                            isSelected(game, type as BetType) ? 'text-white' : 'text-white group-hover:text-stake-blue', 
                                            getOddDirection(game, type) === 'up' ? 'text-[#00E701]' : '', 
                                            getOddDirection(game, type) === 'down' ? 'text-red-500' : '']">
                                            {{ getOddValue(game, type) }}
                                         </span>
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
    0% { background-color: rgba(255, 255, 255, 0.3); }
    100% { background-color: inherit; }
}
.animate-flash { animation: flash-white 0.5s ease-out; }
</style>