<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { HubConnectionBuilder, HubConnection } from '@microsoft/signalr';
import { Radio, AlertCircle, ChevronDown, ChevronRight, Timer, ArrowUp, ArrowDown, Lock } from 'lucide-vue-next';
import apiSports from '../services/apiSports';
import TeamLogo from '../components/TeamLogo.vue';
import { useBetStore, type BetType } from '../stores/useBetStore';

// --- HELPER DE VISUALIZAÇÃO ---
const mapSportVisuals = (key: any) => {
    const k = String(key || '').toLowerCase();
    
    if (k === 'all') return { name: 'Todos', color: 'from-indigo-600 to-blue-800', icon: '🔥' };
    if (k.includes('soccer') || k.includes('futebol')) return { name: 'Futebol', color: 'from-green-500 to-green-700', icon: '⚽' };
    if (k.includes('basket')) return { name: 'Basquete', color: 'from-orange-500 to-red-600', icon: '🏀' };
    if (k.includes('tennis')) return { name: 'Tênis', color: 'from-yellow-500 to-orange-500', icon: '🎾' };
    if (k.includes('esports')) return { name: 'E-Sports', color: 'from-purple-600 to-blue-900', icon: '🎮' };
    if (k.includes('volleyball')) return { name: 'Vôlei', color: 'from-yellow-400 to-orange-400', icon: '🏐' };
    
    return { name: 'Outros', color: 'from-gray-500 to-gray-700', icon: '🏆' };
};

interface LiveGame {
  gameId: string;
  sportKey: string;
  homeTeam: string;
  awayTeam: string;
  league: string;
  homeTeamLogo?: string | null; // ✅ Adicionado
  awayTeamLogo?: string | null; // ✅ Adicionado
  commenceTime: string;
  homeScore: number;
  awayScore: number;
  currentMinute: string;
  period: string;
  homeOdd: number;
  drawOdd: number;
  awayOdd: number;
  homeOddDir?: 'up' | 'down' | null;
  homeOddFlash?: boolean;
  drawOddDir?: 'up' | 'down' | null;
  drawOddFlash?: boolean;
  awayOddDir?: 'up' | 'down' | null;
  awayOddFlash?: boolean;
  [key: string]: any; 
}

const betStore = useBetStore();
const loading = ref(true);
const events = ref<LiveGame[]>([]);
const openLeagues = ref<Set<string>>(new Set());
const selectedSport = ref<string>('all');
let connection: HubConnection | null = null;

const getSportCategory = (game: any): any => {
    const raw = game.sportKey || game.SportKey || '';
    const cleanRaw = String(raw).toLowerCase();
    if (cleanRaw.includes('soccer')) return 'soccer';
    if (cleanRaw.includes('basket')) return 'basketball';
    if (cleanRaw.includes('tennis')) return 'tennis';
    if (cleanRaw.includes('esports')) return 'esports';
    return cleanRaw || 'other'; 
};

const fetchInitialData = async () => {
  try {
    const response = await apiSports.get('/LiveEvents'); 
    events.value = response.data || [];
    // Abre todas as ligas por padrão na carga inicial
    events.value.forEach(e => {
        if(e.league) openLeagues.value.add(e.league);
    });
  } catch (e) {
    console.error("Erro na carga inicial:", e);
  } finally {
    loading.value = false;
  }
};

const updateOddWithAnimation = (game: LiveGame, field: 'homeOdd' | 'drawOdd' | 'awayOdd', newValue: number) => {
    if (!newValue || newValue === game[field]) return; 
    const dirField = (field + 'Dir') as keyof LiveGame;
    const flashField = (field + 'Flash') as keyof LiveGame;
    // @ts-ignore
    game[dirField] = newValue > game[field] ? 'up' : 'down';
    // @ts-ignore
    game[flashField] = true;
    game[field] = newValue;
    setTimeout(() => {
        // @ts-ignore
        game[flashField] = false;
    }, 1000);
};

onMounted(async () => {
  await fetchInitialData();
  
  // Conecta no SignalR (Porta 8090 - Padrão do Docker Compose)
  connection = new HubConnectionBuilder()
    .withUrl('http://localhost:8090/gameHub') 
    .withAutomaticReconnect()
    .build();

  connection.on('ReceiveLiveUpdate', (updatedGames: any[]) => {
    updatedGames.forEach(update => {
        const game = events.value.find(g => g.gameId === update.gameId);
        if (game) {
            game.homeScore = update.homeScore;
            game.awayScore = update.awayScore;
            game.currentMinute = update.currentMinute;
            game.period = update.period;
            if (update.homeOdd) updateOddWithAnimation(game, 'homeOdd', update.homeOdd);
            if (update.drawOdd) updateOddWithAnimation(game, 'drawOdd', update.drawOdd);
            if (update.awayOdd) updateOddWithAnimation(game, 'awayOdd', update.awayOdd);
        } else {
            // Opcional: Se o jogo não existe na lista (começou agora), poderíamos dar um fetchInitialData() novamente
        }
    });
  });

  try {
    await connection.start();
    console.log("🟢 Conectado ao SignalR!");
  } catch (err) {
    console.error("❌ Erro ao conectar SignalR:", err);
  }
});

onUnmounted(() => {
  if (connection) connection.stop();
});

// --- COMPUTEDS ---
const menuOptions = computed(() => {
    const groups: Record<string, number> = {};
    events.value.forEach(e => {
        const key = String(getSportCategory(e));
        if (key) groups[key] = (groups[key] || 0) + 1;
    });

    const options = Object.entries(groups).map(([key, count]) => {
        const visual = mapSportVisuals(String(key));
        return {
            key: key,
            name: visual.name,
            count: count,
            color: visual.color,
            icon: visual.icon
        };
    });
    options.sort((a, b) => b.count - a.count);
    const allVisual = mapSportVisuals('all');
    options.unshift({ key: 'all', name: 'Todos', count: events.value.length, color: allVisual.color, icon: allVisual.icon });
    return options;
});

const filteredEvents = computed(() => {
    if (selectedSport.value === 'all') return events.value;
    return events.value.filter(e => String(getSportCategory(e)) === selectedSport.value);
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

const isMarketSuspended = (odd: number) => !odd || odd <= 1.01;

const handleSelection = (game: LiveGame, type: BetType) => {
  const price = type === '1' ? game.homeOdd : type === '2' ? game.awayOdd : game.drawOdd;
  if (isMarketSuspended(price)) return; 

  const gameId = game.gameId;
  const currentSelection = betStore.selections.find(s => s.id === gameId);
  
  if (currentSelection?.type === type) {
    betStore.removeSelection(gameId);
  } else {
    betStore.addOrReplaceSelection(
        gameId, 
        game.homeTeam, 
        game.awayTeam, 
        type === '1' ? game.homeTeam : type === '2' ? game.awayTeam : 'Empate', 
        price, 
        type, 
        game.commenceTime
    );
  }
};

const isSelected = (game: LiveGame, type: BetType) => betStore.selections.find(s => s.id === game.gameId)?.type === type;
const getOddValue = (game: LiveGame, type: string) => (type === '1' ? game.homeOdd : type === 'X' ? game.drawOdd : game.awayOdd).toFixed(2);
const getBetTypes = (game: LiveGame) => (game.drawOdd > 0.01) ? ['1', 'X', '2'] : ['1', '2'];
const isNumericTime = (time: string) => time && /\d/.test(time);
const getFlagUrl = (leagueName: string) => {
  const name = (leagueName || '').toLowerCase();
  const map: Record<string, string> = { brazil: 'br', england: 'gb', spain: 'es', italy: 'it', germany: 'de', france: 'fr', usa: 'us', portugal: 'pt', argentina: 'ar' };
  const found = Object.keys(map).find(c => name.includes(c));
  return `/images/flags/${found ? map[found] : 'un'}.svg`;
};
const getOddDirection = (game: LiveGame, type: string) => {
    if (type === '1') return game.homeOddDir;
    if (type === 'X') return game.drawOddDir;
    if (type === '2') return game.awayOddDir;
    return null;
};
const getOddFlash = (game: LiveGame, type: string) => {
    if (type === '1') return game.homeOddFlash;
    if (type === 'X') return game.drawOddFlash;
    if (type === '2') return game.awayOddFlash;
    return false;
};
const getOddRaw = (game: LiveGame, type: string) => {
    if (type === '1') return game.homeOdd;
    if (type === 'X') return game.drawOdd;
    if (type === '2') return game.awayOdd;
    return 0;
};
</script>

<template>
  <div class="space-y-4 pb-20 p-4">
    <div class="flex items-center gap-3 pb-2 border-b border-stake-dark/50">
      <div class="bg-red-500/10 p-2 rounded-full animate-pulse">
        <Radio class="w-6 h-6 text-red-500" />
      </div>
      <div>
        <h2 class="text-white text-2xl font-bold uppercase italic">Ao Vivo</h2>
        <p class="text-stake-text text-xs font-bold flex items-center gap-1">
          <span class="w-2 h-2 rounded-full bg-green-500 animate-ping"></span> Acontecendo agora
        </p>
      </div>
    </div>

    <div v-if="loading" class="text-stake-text animate-pulse pl-2 mt-4">Carregando jogos ao vivo...</div>

    <div v-else class="flex gap-3 overflow-x-auto pb-4 custom-scrollbar px-1 py-2">
      <div 
        v-for="sport in menuOptions" 
        :key="sport.key" 
        @click="selectedSport = sport.key" 
        class="min-w-[140px] h-20 rounded-lg relative overflow-hidden cursor-pointer transition-all duration-300 group shadow-lg flex-shrink-0 border border-transparent select-none"
        :class="selectedSport === sport.key 
            ? 'ring-2 ring-white scale-105 brightness-110 z-10 shadow-blue-500/20' 
            : 'hover:brightness-110 opacity-80 hover:opacity-100 hover:-translate-y-1'"
      >
        <div :class="`absolute inset-0 bg-gradient-to-br ${sport.color}`"></div>
        <span class="absolute bottom-2 left-3 text-white font-bold uppercase italic text-xs z-10 leading-tight drop-shadow-md">
            {{ sport.name }}
        </span>
        <span class="absolute top-2 left-2 bg-black/30 text-white text-[10px] font-bold px-2 py-0.5 rounded backdrop-blur-sm">
            {{ sport.count }}
        </span>
        <span class="absolute top-0 right-1 text-3xl opacity-20 transition-all duration-300" :class="selectedSport === sport.key ? 'scale-110 opacity-40' : 'group-hover:scale-110 group-hover:opacity-40'">
            {{ sport.icon }}
        </span>
      </div>
    </div>

    <div class="space-y-4 mt-2">
      <div v-if="events.length === 0 && !loading" class="py-12 flex flex-col items-center justify-center text-stake-text opacity-50 border border-dashed border-stake-text/20 rounded-lg bg-stake-card/30">
        <AlertCircle class="w-10 h-10 mb-2 opacity-50"/>
        <span class="text-sm font-bold">Nenhum jogo ao vivo no momento.</span>
      </div>
      
      <div v-else v-for="(games, league) in groupedEvents" :key="league" class="rounded overflow-hidden">
        <div @click="openLeagues.has(String(league)) ? openLeagues.delete(String(league)) : openLeagues.add(String(league))" class="bg-stake-card p-3 flex items-center justify-between border-l-4 border-stake-blue cursor-pointer hover:brightness-110 transition-all select-none">
          <div class="flex items-center gap-3">
            <img :src="getFlagUrl(String(league))" class="w-5 h-3.5 rounded-sm shadow-sm" />
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
                    <span class="text-sm">{{ game.currentMinute }}</span>
                </div>
                <div v-else class="flex items-center gap-1 text-red-500 font-bold mb-1">
                    <Radio class="w-4 h-4 animate-pulse" />
                    <span class="text-[10px] uppercase">JOGANDO</span>
                </div>
                <div class="text-[9px] text-stake-text/70 uppercase font-bold tracking-wider">
                    {{ game.period || 'AO VIVO' }}
                </div>
            </div>

            <div class="flex-1 w-full text-white">
              <div class="flex items-center justify-between mb-2">
                <div class="flex items-center gap-2">
                    <TeamLogo :teamName="game.homeTeam" :remoteUrl="game.homeTeamLogo" size="w-5 h-5" />
                    <span class="font-bold text-sm">{{ game.homeTeam }}</span>
                </div>
                <div class="flex items-center gap-2">
                    <span :class="['font-mono font-bold text-base px-2.5 py-0.5 rounded min-w-[2rem] text-center transition-colors bg-black/20', game.homeScore > 0 ? 'text-yellow-400' : 'text-stake-blue']">
                        {{ game.homeScore }}
                    </span>
                </div>
              </div>
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2">
                    <TeamLogo :teamName="game.awayTeam" :remoteUrl="game.awayTeamLogo" size="w-5 h-5" />
                    <span class="font-bold text-sm">{{ game.awayTeam }}</span>
                </div>
                <span :class="['font-mono font-bold text-base px-2.5 py-0.5 rounded min-w-[2rem] text-center transition-colors bg-black/20', game.awayScore > 0 ? 'text-yellow-400' : 'text-stake-blue']">
                    {{ game.awayScore }}
                </span>
              </div>
            </div>

            <div class="flex gap-2 w-full md:w-auto">
              <button 
                v-for="type in getBetTypes(game)" 
                :key="type" 
                @click="handleSelection(game, type as BetType)" 
                :disabled="isMarketSuspended(getOddRaw(game, type))"
                :class="[
                  'flex-1 md:w-24 py-2 rounded flex flex-col items-center justify-center border transition-all active:scale-95 group relative overflow-hidden', 
                  isMarketSuspended(getOddRaw(game, type)) ? 'bg-stake-card/50 border-transparent opacity-60 cursor-not-allowed' :
                  isSelected(game, type as BetType) ? 'bg-stake-blue border-stake-blue shadow-[0_0_10px_rgba(0,231,1,0.4)]' : 'bg-stake-card border-transparent hover:border-stake-text/30',
                  getOddFlash(game, type) ? 'animate-flash' : '' 
                ]"
              >
                <span :class="['text-[10px] font-bold mb-0.5 uppercase tracking-wide', isSelected(game, type as BetType) ? 'text-white' : 'text-stake-text']">
                    {{ type === '1' ? 'Casa' : type === '2' ? 'Fora' : 'Empate' }}
                </span>

                <div class="flex items-center gap-1 justify-center min-h-[20px]">
                    <Lock v-if="isMarketSuspended(getOddRaw(game, type))" class="w-4 h-4 text-stake-text/50" />
                    <template v-else>
                        <ArrowUp v-if="getOddDirection(game, type) === 'up'" class="w-3 h-3 text-[#00E701]" />
                        <ArrowDown v-if="getOddDirection(game, type) === 'down'" class="w-3 h-3 text-red-500" />
                        <span :class="['text-sm font-black transition-colors', isSelected(game, type as BetType) ? 'text-white' : 'text-white group-hover:scale-110', getOddDirection(game, type) === 'up' ? 'text-[#00E701]' : '', getOddDirection(game, type) === 'down' ? 'text-red-500' : '']">
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
</template>

<style scoped>
@keyframes flash-white {
    0% { background-color: rgba(255, 255, 255, 0.8); border-color: white; }
    100% { background-color: inherit; border-color: inherit; }
}
.animate-flash { animation: flash-white 0.8s ease-out; }
.custom-scrollbar::-webkit-scrollbar { height: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: rgba(255, 255, 255, 0.05); border-radius: 10px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: rgba(255, 255, 255, 0.2); border-radius: 10px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: rgba(255, 255, 255, 0.4); }
</style>