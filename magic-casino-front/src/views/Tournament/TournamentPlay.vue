<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Swal from 'sweetalert2';
import { ChevronDown, ChevronRight, ArrowLeft, Calendar, Trophy, Coins } from 'lucide-vue-next';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { useBetStore, type BetType } from '../../stores/useBetStore'; 
import tournamentService from '../../services/Tournament/TournamentService';
import sportbookService from '../../services/Sportbook/SportbookService';
import TeamLogo from '../../components/TeamLogo.vue';
import TournamentBetSlip from '../../components/Tournament/TournamentBetSlip.vue';
import TournamentRanking from '../../components/Tournament/TournamentRanking.vue';
import { normalizeCountryName } from '../../utils/countryTranslations';
import { getFlag } from '../../utils/flags';

const route = useRoute();
const router = useRouter();
const store = useBetStore(); 

const tournamentId = Number(route.params.id);
const tournamentName = ref('Carregando...');
const fantasyBalance = ref(0);
const userRank = ref(0); 
const sportKey = ref('soccer');
const timeRemaining = ref('...');
const currentUser = ref('');
const tournamentEnd = ref<number>(0);

const isLoadingGames = ref(true);
const games = ref<any[]>([]);
const openLeagues = ref<Set<string>>(new Set());

const showBetSlip = ref(false);
const showRanking = ref(false);

let connection: HubConnection | null = null;

onMounted(async () => {
  store.clearStore();
  loadCurrentUser();
  await loadTournamentData();
  setupSignalR();
});

onUnmounted(() => {
    if (connection) connection.stop();
});

const setupSignalR = async () => {
    const signalRUrl = "/gameHub";
    connection = new HubConnectionBuilder()
        .withUrl(signalRUrl)
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Information)
        .build();

    connection.on('LiveOddsUpdate', (updatedGames: any[]) => {
        if (!updatedGames || !Array.isArray(updatedGames)) return;
        updatedGames.forEach(update => {
            const updateId = String(update.id || update.gameId).trim();
            const game = games.value.find(g => String(g.externalId || g.id).trim() === updateId);
            if (game) {
                if (update.homeOdd) game.rawOddsHome = update.homeOdd;
                if (update.drawOdd) game.rawOddsDraw = update.drawOdd;
                if (update.awayOdd) game.rawOddsAway = update.awayOdd;
                if (update.time) game.commenceTime = update.time;
            }
        });
    });

    try { await connection.start(); } catch (e) { console.error("SignalR List Error:", e); }
};

const loadCurrentUser = () => {
    try {
        const storedString = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
        if (storedString) {
            const userData = JSON.parse(storedString);
            const rawId = userData.Code || userData.code || userData.Cpf || userData.cpf || userData.id || '';
            currentUser.value = String(rawId).replace(/\D/g, ''); 
        }
    } catch (e) { console.error("Erro user:", e); }
};

const loadTournamentData = async () => {
  try {
    const res = await tournamentService.getTournament(tournamentId, currentUser.value);
    if (res.data) {
      if (res.data.isJoined === false) {
          await Swal.fire({ title: 'Acesso Negado', text: 'Inscreva-se primeiro.', icon: 'warning', confirmButtonColor: '#3b82f6', background: '#121214', color: '#fff' });
          router.push('/tournaments'); 
          return;
      }

      tournamentName.value = res.data.name;
      fantasyBalance.value = res.data.currentFantasyBalance ?? res.data.initialFantasyBalance ?? 1000;
      sportKey.value = res.data.sport || 'soccer';
      userRank.value = res.data.rank || 0;
      tournamentEnd.value = new Date(res.data.endDate).getTime();
      calculateTimeRemaining(res.data.endDate);
      
      let rules = null;
      if (res.data.filterRules) {
          try { rules = JSON.parse(res.data.filterRules); } catch(e) {}
      }
      
      await loadRealGames(sportKey.value, rules);
    }
  } catch (error: any) {
    router.push('/tournaments');
  }
};

const loadRealGames = async (sport: string, filterRules: any) => {
  isLoadingGames.value = true;
  try {
    const res = await sportbookService.getEventsBySport(sport, 1000);
    let allGames = res.data || [];

    if (filterRules) {
        if (filterRules.sports) {
            const activeSport = filterRules.sports.find((s: any) => s.key === sport);
            if (activeSport?.leagues) {
                 const allowed = new Set(activeSport.leagues.map((l: any) => String(l.id)));
                 allGames = allGames.filter((g: any) => allowed.has(String(g.leagueId || g.league?.id)));
            }
        } 
    }
    
    if (tournamentEnd.value > 0) {
        allGames = allGames.filter((g: any) => new Date(g.commenceTime).getTime() <= tournamentEnd.value);
    }

    games.value = allGames;
    sortedGroups.value.forEach(g => openLeagues.value.add(g.key));

  } catch (error) { console.error("Erro games:", error); } finally { isLoadingGames.value = false; }
};

// --- GETTERS DE ODDS ---
const getOdd = (game: any, type: BetType): string => {
  let val: number | undefined | null = 0;
  if (type === '1') val = game.rawOddsHome ?? game.RawOddsHome;
  else if (type === '2') val = game.rawOddsAway ?? game.RawOddsAway;
  else val = game.rawOddsDraw ?? game.RawOddsDraw;

  if (!val || val <= 1.0) return '0.00';
  return Number(val).toFixed(2);
};

const getSelectedType = (gameId: string): BetType | null => {
  const found = store.selections.find(s => s.id === gameId);
  return found ? found.type : null;
};

// --- CLICK HANDLER ---
const handleBetClick = (game: any, type: BetType) => {
  const gameId = String(game.externalId || game.ExternalId || game.id);
  const priceStr = getOdd(game, type);
  const price = parseFloat(priceStr);

  if (price <= 1.0 || isNaN(price)) return;

  if (getSelectedType(gameId) === type) {
    store.removeSelection(gameId);
    return;
  }

  const hTeam = game.homeTeam || game.HomeTeam || '';
  const aTeam = game.awayTeam || game.AwayTeam || '';
  const selectionName = type === '1' ? hTeam : type === '2' ? aTeam : 'Empate';
  const time = game.commenceTime || game.CommenceTime || new Date().toISOString();

  store.addOrReplaceSelection(gameId, hTeam, aTeam, selectionName, price, type, time);
  
  if (store.count > 0) showBetSlip.value = true;
};

const sortedGroups = computed(() => {
  const groups: Record<string, any[]> = {};
  games.value.forEach(game => {
    const rawLg = game.league || game.League || 'Outros';
    const rawCountry = game.countryCode ? 'Country' : 'Internacional';
    const { country: cleanCountry, league: cleanLeague } = normalizeCountryName(String(rawCountry), String(rawLg));
    const groupKey = `${cleanCountry} • ${cleanLeague}`;
    
    if (!groups[groupKey]) {
        groups[groupKey] = [];
        (groups[groupKey] as any).meta = { displayName: cleanLeague, country: cleanCountry, countryCode: game.countryCode };
    }
    groups[groupKey].push(game);
  });

  return Object.keys(groups).map(key => {
    const group = groups[key];
    const meta = (group as any).meta;
    return { key: key, displayName: meta.displayName, country: meta.country, countryCode: meta.countryCode, games: group };
  }).sort((a, b) => a.key.localeCompare(b.key));
});

const toggleLeague = (key: string) => {
  if (openLeagues.value.has(key)) openLeagues.value.delete(key);
  else openLeagues.value.add(key);
};

// Utils
const calculateTimeRemaining = (endDateStr: string) => {
    const diff = new Date(endDateStr).getTime() - new Date().getTime();
    const days = Math.floor(diff / (1000 * 60 * 60 * 24));
    timeRemaining.value = diff > 0 ? `${days} dias restantes` : 'Encerrado';
};
const formatTime = (d: string) => new Date(d).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
const formatDate = (d: string) => {
    const date = new Date(d);
    date.setHours(0,0,0,0);
    const today = new Date();
    today.setHours(0,0,0,0);
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);
    
    if (date.getTime() === today.getTime()) return 'HOJE';
    if (date.getTime() === tomorrow.getTime()) return 'AMANHÃ';
    return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
};
const handleImageError = (event: Event) => { (event.target as HTMLImageElement).style.display = 'none'; };
</script>

<template>
  <div class="min-h-screen pb-20 bg-[#0f172a]">
    
    <div class="sticky top-0 z-40 bg-[#1a2c38] border-b border-white/10 shadow-lg p-4 mb-4">
      <div class="max-w-[1200px] mx-auto flex flex-col md:flex-row justify-between items-center gap-4">
        
        <div class="flex items-center gap-3 w-full md:w-auto">
          <button @click="router.push('/tournaments')" class="bg-white/5 hover:bg-white/10 p-2 rounded transition">
            <ArrowLeft class="w-5 h-5 text-gray-400" />
          </button>
          <div>
            <h1 class="text-white font-black uppercase italic tracking-wide text-lg leading-none">
              {{ tournamentName }}
            </h1>
            <div class="flex items-center gap-2 text-xs text-gray-400 mt-1">
              <Calendar class="w-3 h-3" />
              <span>Encerra em: {{ timeRemaining }}</span>
            </div>
          </div>
        </div>

        <div class="flex gap-3 w-full md:w-auto">
          <button @click="showRanking = true" class="flex-1 md:flex-none bg-blue-600 hover:bg-blue-500 border border-blue-400/50 rounded px-4 py-2 flex items-center gap-3 transition-all cursor-pointer group shadow-lg shadow-blue-900/20 active:scale-95">
            <div class="bg-white/20 p-1.5 rounded-full"><Trophy class="w-4 h-4 text-white group-hover:scale-110 transition-transform" /></div>
            <div class="flex flex-col items-start">
              <span class="text-[9px] text-blue-100 uppercase font-bold tracking-wider">Ranking</span>
              <span class="text-white font-bold leading-none text-sm">{{ userRank > 0 ? `#${userRank}` : 'VER TODOS' }}</span>
            </div>
          </button>
          
          <div class="flex-1 md:flex-none bg-black/30 border border-white/5 rounded px-4 py-2 flex items-center gap-3">
            <Coins class="w-5 h-5 text-green-400" />
            <div class="flex flex-col">
              <span class="text-[10px] text-gray-400 uppercase font-bold">Saldo Torneio</span>
              <span class="text-green-400 font-mono font-bold leading-none">{{ fantasyBalance.toFixed(2) }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="max-w-[1200px] mx-auto px-2 md:px-4 space-y-3">
      <div v-if="isLoadingGames" class="space-y-3 pt-2">
         <div v-for="i in 5" :key="i" class="bg-[#1a2c38] h-20 rounded animate-pulse border border-white/5"></div>
      </div>

      <div v-else-if="sortedGroups.length === 0" class="py-10 text-center text-gray-500 opacity-50 border border-dashed border-gray-700 rounded bg-[#1a2c38]">
         <p>Nenhum jogo disponível.</p>
      </div>

      <div v-else v-for="group in sortedGroups" :key="group.key" class="rounded overflow-hidden">
        <div @click="toggleLeague(group.key)" class="bg-[#1a2c38]/90 backdrop-blur-sm p-2 flex items-center justify-between border-l-2 border-blue-500 cursor-pointer hover:bg-[#213746] transition-all select-none">
          <div class="flex items-center gap-2">
            <img :src="getFlag(group.country === 'Brasil' ? 'Brazil' : group.country, group.countryCode)" class="w-4 h-3 rounded-[1px] shadow-sm object-cover bg-black/20" @error="handleImageError" />
            <h3 class="text-white font-bold text-xs uppercase tracking-wide">{{ group.country }} • <span class="text-gray-300">{{ group.displayName }}</span></h3>
            <span class="text-[9px] text-gray-400 font-bold bg-black/20 px-1.5 py-0.5 rounded-full">{{ group.games?.length || 0 }}</span>
          </div>
          <component :is="openLeagues.has(group.key) ? ChevronDown : ChevronRight" class="w-4 h-4 text-gray-400" />
        </div>

        <div v-show="openLeagues.has(group.key)" class="bg-[#0f172a] border-x border-b border-[#1a2c38]">
          <div v-for="game in group.games" :key="game.id" class="py-2 px-2 border-b border-white/5 flex flex-col md:flex-row items-center gap-2 transition-all hover:bg-white/[0.02]">
            <div class="flex flex-row md:flex-col items-center justify-start md:justify-center gap-2 md:gap-0.5 min-w-[60px] md:w-[60px] text-left md:text-center mr-2 md:mr-0 border-r md:border-r-0 md:border-b-0 border-white/10 pr-2 md:pr-0 h-full">
               <div class="text-[9px] font-bold text-blue-400 leading-none">{{ formatDate(game.commenceTime) }}</div>
               <div class="text-white text-[10px] font-bold leading-none">{{ formatTime(game.commenceTime) }}</div>
            </div>

            <div class="flex-1 w-full text-white md:border-l md:border-white/5 md:pl-3">
               <div class="flex flex-col gap-1.5 justify-center h-full">
                 <div class="flex items-center gap-2"><TeamLogo :teamName="game.homeTeam" :remoteUrl="game.homeTeamLogo" size="w-4 h-4" /><span class="font-medium text-xs text-white/90 truncate">{{ game.homeTeam }}</span></div>
                 <div class="flex items-center gap-2"><TeamLogo :teamName="game.awayTeam" :remoteUrl="game.awayTeamLogo" size="w-4 h-4" /><span class="font-medium text-xs text-white/90 truncate">{{ game.awayTeam }}</span></div>
               </div>
            </div>

            <div class="flex gap-1 w-full md:w-auto mt-2 md:mt-0">
               <button @click="handleBetClick(game, '1')" :disabled="parseFloat(getOdd(game, '1')) <= 1.0"
                 :class="['flex-1 md:w-[70px] h-auto py-1.5 rounded-sm flex flex-col items-center justify-center transition-all group border border-transparent',
                   parseFloat(getOdd(game, '1')) <= 1.0 ? 'opacity-50 cursor-not-allowed bg-[#1a2c38]' : getSelectedType(String(game.externalId || game.id)) === '1' ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]']">
                 <span class="text-[9px] font-bold uppercase mb-0.5 tracking-wide" :class="getSelectedType(String(game.externalId || game.id)) === '1' ? 'text-white' : 'text-gray-400'">1</span>
                 <span class="text-xs font-bold" :class="getSelectedType(String(game.externalId || game.id)) === '1' ? 'text-white' : 'text-white group-hover:text-blue-400'">{{ getOdd(game, '1') }}</span>
               </button>

               <button v-if="parseFloat(getOdd(game, 'X')) > 1.01" @click="handleBetClick(game, 'X')" 
                 :class="['flex-1 md:w-[70px] h-auto py-1.5 rounded-sm flex flex-col items-center justify-center transition-all group border border-transparent',
                   getSelectedType(String(game.externalId || game.id)) === 'X' ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]']">
                 <span class="text-[9px] font-bold uppercase mb-0.5 tracking-wide" :class="getSelectedType(String(game.externalId || game.id)) === 'X' ? 'text-white' : 'text-gray-400'">X</span>
                 <span class="text-xs font-bold" :class="getSelectedType(String(game.externalId || game.id)) === 'X' ? 'text-white' : 'text-white group-hover:text-blue-400'">{{ getOdd(game, 'X') }}</span>
               </button>

               <button @click="handleBetClick(game, '2')" :disabled="parseFloat(getOdd(game, '2')) <= 1.0"
                 :class="['flex-1 md:w-[70px] h-auto py-1.5 rounded-sm flex flex-col items-center justify-center transition-all group border border-transparent',
                   parseFloat(getOdd(game, '2')) <= 1.0 ? 'opacity-50 cursor-not-allowed bg-[#1a2c38]' : getSelectedType(String(game.externalId || game.id)) === '2' ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]']">
                 <span class="text-[9px] font-bold uppercase mb-0.5 tracking-wide" :class="getSelectedType(String(game.externalId || game.id)) === '2' ? 'text-white' : 'text-gray-400'">2</span>
                 <span class="text-xs font-bold" :class="getSelectedType(String(game.externalId || game.id)) === '2' ? 'text-white' : 'text-white group-hover:text-blue-400'">{{ getOdd(game, '2') }}</span>
               </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <TournamentBetSlip 
      v-if="store.count > 0 || showBetSlip"
      :is-open="showBetSlip"
      :tournament-id="tournamentId" 
      :fantasy-balance="fantasyBalance"
      @toggle="showBetSlip = !showBetSlip"
      @balance-updated="loadTournamentData" 
    />

    <TournamentRanking 
      v-if="showRanking"
      :is-open="showRanking"
      :tournament-id="tournamentId"
      :current-user-id="currentUser"
      @close="showRanking = false"
    />
  </div>
</template>