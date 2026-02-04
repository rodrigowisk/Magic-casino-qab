<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Swal from 'sweetalert2';
import { Trophy, ChevronLeft, ChevronDown, ChevronRight, AlertCircle, Calendar } from 'lucide-vue-next';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { useBetStore, type BetType } from '../../stores/useBetStore'; 
import tournamentService from '../../services/Tournament/TournamentService';
import sportbookService from '../../services/Sportbook/SportbookService';
import TeamLogo from '../../components/TeamLogo.vue';
import TournamentBetSlip from '../../components/Tournament/TournamentBetSlip.vue';
import TournamentRanking from '../../components/Tournament/TournamentRanking.vue';
import { normalizeCountryName } from '../../utils/countryTranslations';
import { getFlag } from '../../utils/flags';

import TournamentSportsMenu from '../../components/Tournament/TournamentSportsMenu.vue';
import TournamentHeaderCarousel from '../../components/Tournament/TournamentHeaderCarousel.vue';

const route = useRoute();
const router = useRouter();
const store = useBetStore(); 

const tournamentId = ref(Number(route.params.id));
const tournamentName = ref('Carregando...');
const fantasyBalance = ref(0);
const userRank = ref(0); 
const sportKey = ref('soccer');
const currentUser = ref('');
const tournamentEnd = ref<number>(0);

const isLoadingGames = ref(true);
const games = ref<any[]>([]);
const openLeagues = ref<Set<string>>(new Set());
const myTournaments = ref<any[]>([]); 

const showBetSlip = ref(false); 
const isMobile = ref(false);
const showRanking = ref(false);

const activeMode = ref<'prematch' | 'live'>('prematch');
const selectedSport = ref<string>('all');
const dataSelecionada = ref<string>('all');

// ✅ Lógica para esconder o menu se não tiver jogos e não estiver carregando
const showMenu = computed(() => {
    return isLoadingGames.value || games.value.length > 0;
});

let connection: HubConnection | null = null;

const checkScreenSize = () => {
    isMobile.value = window.innerWidth < 768;
    if (!isMobile.value && store.count > 0) showBetSlip.value = true;
};

onMounted(async () => {
  store.clearStore();
  loadCurrentUser();
  await loadTournamentData();
  if (tournamentName.value !== 'Carregando...') {
      addToMyTournaments({
          id: tournamentId.value,
          name: tournamentName.value,
          endDate: new Date(tournamentEnd.value).toISOString(),
          isJoined: true,
          userRank: userRank.value
      });
  }
  loadUserTournaments();
  setupSignalR();
  checkScreenSize();
  window.addEventListener('resize', checkScreenSize);
});

onUnmounted(() => {
    window.removeEventListener('resize', checkScreenSize);
    if (connection) connection.stop();
});

watch(() => route.params.id, async (newId) => {
    if (newId) {
        tournamentId.value = Number(newId);
        games.value = [];
        isLoadingGames.value = true;
        await loadTournamentData();
        if (!myTournaments.value.find(t => t.id === tournamentId.value)) loadUserTournaments();
    }
});

watch(() => store.count, (newVal) => {
    if (newVal > 0 && !isMobile.value) showBetSlip.value = true;
});

const addToMyTournaments = (tournament: any) => {
    const exists = myTournaments.value.some(t => t.id === tournament.id);
    if (!exists) myTournaments.value.push(tournament);
};

const setupSignalR = async () => {
    const signalRUrl = "/gameHub";
    connection = new HubConnectionBuilder().withUrl(signalRUrl).withAutomaticReconnect().configureLogging(LogLevel.Information).build();
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
        const stored = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
        if (stored) {
            const userData = JSON.parse(stored);
            const rawId = userData.Code || userData.code || userData.Cpf || userData.cpf || userData.id || '';
            currentUser.value = String(rawId).replace(/\D/g, ''); 
        }
    } catch (e) { console.error("Erro user:", e); }
};

const loadUserTournaments = async () => {
    if (!currentUser.value) return;
    try {
        const res = await tournamentService.listTournaments(currentUser.value);
        if (res && res.data && Array.isArray(res.data)) {
            const joined = res.data.filter((t: any) => t.isJoined === true);
            const mapped = joined.map((t: any) => ({
                id: t.id || t.Id,
                name: t.name || t.Name,
                endDate: t.endDate || t.EndDate,
                isJoined: true,
                userRank: t.rank || t.Rank || t.userRank
            }));
            mapped.forEach((t: any) => addToMyTournaments(t));
        }
    } catch (e) { console.error("Erro loading my tournaments:", e); }
};

const loadTournamentData = async () => {
  try {
    const res = await tournamentService.getTournament(tournamentId.value, currentUser.value);
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
      let rules = null;
      if (res.data.filterRules) { try { rules = JSON.parse(res.data.filterRules); } catch(e) {} }
      await loadRealGames(sportKey.value, rules);
    }
  } catch (error: any) { console.error(error); }
};

const loadRealGames = async (sport: string, filterRules: any) => {
  isLoadingGames.value = true;
  try {
    const res = await sportbookService.getEventsBySport(sport, 1000);
    let allGames = res.data || [];
    if (filterRules?.sports) {
        const activeSport = filterRules.sports.find((s: any) => s.key === sport);
        if (activeSport?.leagues) {
             const allowed = new Set(activeSport.leagues.map((l: any) => String(l.id)));
             allGames = allGames.filter((g: any) => allowed.has(String(g.leagueId || g.league?.id)));
        }
    }
    if (tournamentEnd.value > 0) allGames = allGames.filter((g: any) => new Date(g.commenceTime).getTime() <= tournamentEnd.value);
    const hoje = new Date(); hoje.setHours(0, 0, 0, 0);
    allGames = allGames.filter((g: any) => new Date(g.commenceTime).getTime() >= hoje.getTime());
    games.value = allGames;
  } catch (error) { console.error("Erro games:", error); } finally { isLoadingGames.value = false; }
};

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

const handleBetClick = (game: any, type: BetType) => {
  const gameId = String(game.externalId || game.ExternalId || game.id);
  const priceStr = getOdd(game, type);
  const price = parseFloat(priceStr);
  if (price <= 1.0 || isNaN(price)) return;
  if (getSelectedType(gameId) === type) { store.removeSelection(gameId); return; }
  const hTeam = game.homeTeam || game.HomeTeam || '';
  const aTeam = game.awayTeam || game.AwayTeam || '';
  const selectionName = type === '1' ? hTeam : type === '2' ? aTeam : 'Empate';
  const time = game.commenceTime || game.CommenceTime || new Date().toISOString();
  store.addOrReplaceSelection(gameId, hTeam, aTeam, selectionName, price, type, time);
  if (store.count > 0) showBetSlip.value = true;
};

const handleCarouselSelect = (newId: number) => {
    if (newId === tournamentId.value) return;
    router.push(`/tournament/${newId}/play`);
};

const sortedGroups = computed(() => {
  const groups: Record<string, any[]> = {};
  games.value.forEach(game => {
    if (dataSelecionada.value !== 'all') {
        const d = new Date(game.commenceTime);
        const gameDate = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
        if (gameDate !== dataSelecionada.value) return; 
    }
    if (selectedSport.value !== 'all') {
        const gSport = (game.sportKey || game.Sport || game.sport || 'soccer').toLowerCase(); 
        if (!gSport.includes(selectedSport.value.toLowerCase())) return;
    }
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
  const result = Object.keys(groups).map(key => {
    const group = groups[key];
    const meta = (group as any).meta;
    return { key: key, displayName: meta.displayName, country: meta.country, countryCode: meta.countryCode, games: group };
  }).sort((a, b) => a.key.localeCompare(b.key));
  if (openLeagues.value.size === 0 && result.length > 0) result.forEach(g => openLeagues.value.add(g.key));
  return result;
});

const toggleLeague = (key: string) => {
  if (openLeagues.value.has(key)) openLeagues.value.delete(key); else openLeagues.value.add(key);
};

const formatTime = (d: string) => new Date(d).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
const formatDate = (d: string) => {
    const date = new Date(d); date.setHours(0,0,0,0);
    const today = new Date(); today.setHours(0,0,0,0);
    const tomorrow = new Date(today); tomorrow.setDate(tomorrow.getDate() + 1);
    if (date.getTime() === today.getTime()) return 'HOJE';
    if (date.getTime() === tomorrow.getTime()) return 'AMANHÃ';
    return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
};
const handleImageError = (event: Event) => { (event.target as HTMLImageElement).style.display = 'none'; };
</script>

<template>
  <div class="flex h-full bg-[#0f172a] text-slate-300 font-sans relative overflow-hidden">
    
    <div class="flex-1 flex flex-col h-full overflow-y-auto custom-scrollbar relative">
        
        <TournamentHeaderCarousel 
            :tournaments="myTournaments"
            :active-tournament-id="tournamentId"
            :fantasy-balance="fantasyBalance"
            :user-rank="userRank"
            @select="handleCarouselSelect"
            @open-history="router.push(`/tournament/${tournamentId}/my-bets`)"
            @open-ranking="showRanking = true"
            class="!mb-0" 
        />

        <div v-if="showMenu" class="sticky top-0 z-30 shadow-xl border-b border-white/5 -mt-8 md:-mt-10">
            <TournamentSportsMenu 
                :games="games"
                v-model:activeMode="activeMode"
                v-model:selectedSport="selectedSport"
                v-model:selectedDate="dataSelecionada"
            />
        </div>

        <div class="max-w-[1200px] mx-auto px-2 md:px-4 space-y-3 w-full pb-20 pt-2">
             <div v-if="isLoadingGames" class="space-y-3 pt-2">
                 <div v-for="i in 5" :key="i" class="bg-[#1a2c38] h-20 rounded animate-pulse border border-white/5"></div>
             </div>
             
             <div v-else-if="sortedGroups.length === 0" class="flex flex-col items-center justify-center min-h-[400px] text-center p-8">
                  <div class="bg-[#1e293b] p-6 rounded-full mb-4 border border-white/5 shadow-2xl">
                      <AlertCircle class="w-12 h-12 text-slate-500" />
                  </div>
                  <h3 class="text-xl font-bold text-white mb-2">Sem Jogos Disponíveis</h3>
                  <p class="text-sm text-slate-400 max-w-md mx-auto">
                      Não há jogos compatíveis com as regras deste torneio no momento ou o torneio já foi finalizado.
                  </p>
                  <button @click="router.push('/tournaments')" class="mt-6 px-6 py-2 bg-blue-600 hover:bg-blue-500 text-white font-bold rounded-lg transition-all shadow-lg hover:shadow-blue-500/20 text-sm uppercase tracking-wide">
                      Voltar para o Lobby
                  </button>
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
                     <div v-for="game in group.games" :key="game.id">
                        
                        <div class="md:hidden flex flex-col gap-1 p-2 border-b border-white/5 hover:bg-white/[0.02]">
                            <div class="flex items-center justify-center gap-1.5 text-[10px] font-bold text-yellow-400 uppercase tracking-wider">
                                <Calendar class="w-3 h-3 text-yellow-400" />
                                <span>{{ formatDate(game.commenceTime) }} - {{ formatTime(game.commenceTime) }}H</span>
                            </div>

                            <div class="grid grid-cols-[1fr_auto_1fr] items-center gap-2">
                                <div class="flex items-center justify-end gap-2 text-right">
                                    <span class="text-xs font-bold text-white truncate leading-tight">{{ game.homeTeam }}</span>
                                    <TeamLogo :teamName="game.homeTeam" :remoteUrl="game.homeTeamLogo" size="w-6 h-6" />
                                </div>

                                <span class="text-[9px] font-bold text-blue-500 bg-blue-500/10 px-1.5 py-0.5 rounded">VS</span>

                                <div class="flex items-center justify-start gap-2 text-left">
                                    <TeamLogo :teamName="game.awayTeam" :remoteUrl="game.awayTeamLogo" size="w-6 h-6" />
                                    <span class="text-xs font-bold text-white truncate leading-tight">{{ game.awayTeam }}</span>
                                </div>
                            </div>

                            <div class="flex gap-1 w-full">
                                <button @click="handleBetClick(game, '1')" :disabled="parseFloat(getOdd(game, '1')) <= 1.0"
                                    :class="['flex-1 h-9 rounded-sm flex flex-col items-center justify-center transition-all group border border-transparent',
                                    parseFloat(getOdd(game, '1')) <= 1.0 ? 'opacity-50 cursor-not-allowed bg-[#1a2c38]' : getSelectedType(String(game.externalId || game.id)) === '1' ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]']">
                                    <span class="text-[9px] font-bold uppercase tracking-wide opacity-70" :class="getSelectedType(String(game.externalId || game.id)) === '1' ? 'text-white' : 'text-gray-400'">1</span>
                                    <span class="text-xs font-bold leading-none" :class="getSelectedType(String(game.externalId || game.id)) === '1' ? 'text-white' : 'text-white group-hover:text-blue-400'">{{ getOdd(game, '1') }}</span>
                                </button>

                                <button v-if="parseFloat(getOdd(game, 'X')) > 1.01" @click="handleBetClick(game, 'X')" 
                                    :class="['flex-1 h-9 rounded-sm flex flex-col items-center justify-center transition-all group border border-transparent',
                                    getSelectedType(String(game.externalId || game.id)) === 'X' ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]']">
                                    <span class="text-[9px] font-bold uppercase tracking-wide opacity-70" :class="getSelectedType(String(game.externalId || game.id)) === 'X' ? 'text-white' : 'text-gray-400'">X</span>
                                    <span class="text-xs font-bold leading-none" :class="getSelectedType(String(game.externalId || game.id)) === 'X' ? 'text-white' : 'text-white group-hover:text-blue-400'">{{ getOdd(game, 'X') }}</span>
                                </button>

                                <button @click="handleBetClick(game, '2')" :disabled="parseFloat(getOdd(game, '2')) <= 1.0"
                                    :class="['flex-1 h-9 rounded-sm flex flex-col items-center justify-center transition-all group border border-transparent',
                                    parseFloat(getOdd(game, '2')) <= 1.0 ? 'opacity-50 cursor-not-allowed bg-[#1a2c38]' : getSelectedType(String(game.externalId || game.id)) === '2' ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]']">
                                    <span class="text-[9px] font-bold uppercase tracking-wide opacity-70" :class="getSelectedType(String(game.externalId || game.id)) === '2' ? 'text-white' : 'text-gray-400'">2</span>
                                    <span class="text-xs font-bold leading-none" :class="getSelectedType(String(game.externalId || game.id)) === '2' ? 'text-white' : 'text-white group-hover:text-blue-400'">{{ getOdd(game, '2') }}</span>
                                </button>
                            </div>
                        </div>

                        <div class="hidden md:flex py-2 px-2 border-b border-white/5 flex-row items-center gap-2 transition-all hover:bg-white/[0.02]">
                            <div class="flex flex-col items-center justify-center gap-0.5 w-[60px] text-center">
                                <div class="text-[9px] font-bold text-blue-400 leading-none">{{ formatDate(game.commenceTime) }}</div>
                                <div class="text-white text-[10px] font-bold leading-none">{{ formatTime(game.commenceTime) }}</div>
                            </div>

                            <div class="flex-1 w-full text-white border-l border-white/5 pl-3">
                                <div class="flex flex-col gap-1.5 justify-center h-full">
                                    <div class="flex items-center gap-2"><TeamLogo :teamName="game.homeTeam" :remoteUrl="game.homeTeamLogo" size="w-4 h-4" /><span class="font-medium text-xs text-white/90 truncate">{{ game.homeTeam }}</span></div>
                                    <div class="flex items-center gap-2"><TeamLogo :teamName="game.awayTeam" :remoteUrl="game.awayTeamLogo" size="w-4 h-4" /><span class="font-medium text-xs text-white/90 truncate">{{ game.awayTeam }}</span></div>
                                </div>
                            </div>

                            <div class="flex gap-1 w-auto">
                                <button @click="handleBetClick(game, '1')" :disabled="parseFloat(getOdd(game, '1')) <= 1.0"
                                    :class="['w-[70px] h-auto py-1.5 rounded-sm flex flex-col items-center justify-center transition-all group border border-transparent',
                                    parseFloat(getOdd(game, '1')) <= 1.0 ? 'opacity-50 cursor-not-allowed bg-[#1a2c38]' : getSelectedType(String(game.externalId || game.id)) === '1' ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]']">
                                    <span class="text-[9px] font-bold uppercase mb-0.5 tracking-wide" :class="getSelectedType(String(game.externalId || game.id)) === '1' ? 'text-white' : 'text-gray-400'">1</span>
                                    <span class="text-xs font-bold" :class="getSelectedType(String(game.externalId || game.id)) === '1' ? 'text-white' : 'text-white group-hover:text-blue-400'">{{ getOdd(game, '1') }}</span>
                                </button>

                                <button v-if="parseFloat(getOdd(game, 'X')) > 1.01" @click="handleBetClick(game, 'X')" 
                                    :class="['w-[70px] h-auto py-1.5 rounded-sm flex flex-col items-center justify-center transition-all group border border-transparent',
                                    getSelectedType(String(game.externalId || game.id)) === 'X' ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]']">
                                    <span class="text-[9px] font-bold uppercase mb-0.5 tracking-wide" :class="getSelectedType(String(game.externalId || game.id)) === 'X' ? 'text-white' : 'text-gray-400'">X</span>
                                    <span class="text-xs font-bold" :class="getSelectedType(String(game.externalId || game.id)) === 'X' ? 'text-white' : 'text-white group-hover:text-blue-400'">{{ getOdd(game, 'X') }}</span>
                                </button>

                                <button @click="handleBetClick(game, '2')" :disabled="parseFloat(getOdd(game, '2')) <= 1.0"
                                    :class="['w-[70px] h-auto py-1.5 rounded-sm flex flex-col items-center justify-center transition-all group border border-transparent',
                                    parseFloat(getOdd(game, '2')) <= 1.0 ? 'opacity-50 cursor-not-allowed bg-[#1a2c38]' : getSelectedType(String(game.externalId || game.id)) === '2' ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]']">
                                    <span class="text-[9px] font-bold uppercase mb-0.5 tracking-wide" :class="getSelectedType(String(game.externalId || game.id)) === '2' ? 'text-white' : 'text-gray-400'">2</span>
                                    <span class="text-xs font-bold" :class="getSelectedType(String(game.externalId || game.id)) === '2' ? 'text-white' : 'text-white group-hover:text-blue-400'">{{ getOdd(game, '2') }}</span>
                                </button>
                            </div>
                        </div>

                     </div>
                 </div>
             </div>
        </div>
    </div>

    <div v-show="showBetSlip" class="hidden md:flex w-[320px] bg-[#1e293b] border-l border-slate-800 shadow-2xl flex-col h-full z-30">
      <TournamentBetSlip :is-open="true" :tournament-id="tournamentId" :fantasy-balance="fantasyBalance" @close="showBetSlip = false" @balance-updated="loadTournamentData" />
    </div>

    <div v-if="isMobile && showBetSlip" class="fixed bottom-0 left-0 w-full z-[150] bg-[#1e293b] shadow-[0_-5px_30px_rgba(0,0,0,0.8)] border-t border-gray-700 rounded-t-2xl h-[50vh] flex flex-col">
        <TournamentBetSlip :is-open="true" :tournament-id="tournamentId" :fantasy-balance="fantasyBalance" :is-mobile="true" @close="showBetSlip = false" @balance-updated="loadTournamentData" />
    </div>

    <button v-if="!showBetSlip && store.count > 0" @click="showBetSlip = true" class="fixed bottom-6 right-6 z-50 bg-[#1e293b]/90 hover:bg-[#1e293b] text-white border border-yellow-500/30 shadow-2xl shadow-black/80 rounded-md px-4 py-3 flex items-center gap-3 transition-all hover:scale-105 group animate-bounce-in">
        <div class="relative"><Trophy class="w-5 h-5 text-yellow-500 group-hover:rotate-12 transition-transform" /><span class="absolute -top-2 -right-2 bg-red-500 text-white text-[10px] w-5 h-5 flex items-center justify-center rounded-full font-bold border-2 border-[#1e293b]">{{ store.count }}</span></div>
        <span class="font-bold text-sm uppercase tracking-wide">Cupom</span><ChevronLeft class="w-4 h-4 text-gray-400 group-hover:text-white" />
    </button>

    <TournamentRanking v-if="showRanking" :is-open="showRanking" :tournament-id="tournamentId" :current-user-id="currentUser" @close="showRanking = false" />
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; height: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #0f172a; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #475569; }

@keyframes bounce-in {
    0% { transform: scale(0.5); opacity: 0; }
    50% { transform: scale(1.1); }
    100% { transform: scale(1); opacity: 1; }
}
.animate-bounce-in { animation: bounce-in 0.3s ease-out; }
</style>