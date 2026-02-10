<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, onActivated } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Swal from 'sweetalert2';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { AlertCircle, ChevronDown, ChevronRight, ArrowUp, ArrowDown, Lock, Star } from 'lucide-vue-next';
import axios from 'axios';

// Stores
import { useBetStore, type BetType } from '../../stores/useBetStore';
import { useFavoritesStore } from '../../stores/useFavoritesStore';
import tournamentService from '../../services/Tournament/TournamentService';

// Componentes
import TeamLogo from '../../components/TeamLogo.vue';
import TournamentSportsMenu from '../../components/Tournament/TournamentSportsMenu.vue';

// Utils
import { normalizeCountryName } from '../../utils/countryTranslations';
import { getFlag } from '../../utils/flags';

// --- IMPORTS DO LOADER ---
import PageLoader from '../../components/PageLoader.vue';
import { usePageLoader } from '../../composables/usePageLoader';

// --- INTERFACES ---
interface LiveGame {
  gameId: string;
  sportKey: string;
  homeTeam: string;
  awayTeam: string;
  league: string;
  leagueId?: string;
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

// --- SETUP ---
const route = useRoute();
const router = useRouter();
const store = useBetStore();
const favStore = useFavoritesStore();

// --- SETUP DO LOADER ---
const { isLoading, loadingProgress, startLoader, finishLoader } = usePageLoader();

// Dados do Torneio
const tournamentId = ref(Number(route.params.id));
const fantasyBalance = ref(0);
const currentUser = ref('');
const tournamentEnd = ref<number>(0);
const tournamentRules = ref<any>(null);
const defaultSportKey = ref('soccer');

// Estados de Carregamento Interno
const loadingGames = ref(true); 

// Dados Ao Vivo
const events = ref<LiveGame[]>([]);
const openLeagues = ref<Set<string>>(new Set());
const activeMode = ref<'prematch' | 'live'>('live');
const selectedSport = ref<string>('all');
const dataSelecionada = ref<string>('all');

let connection: HubConnection | null = null;

const showMenu = computed(() => {
    return !loadingGames.value && events.value.length > 0;
});

// Sincroniza o botão visual
onActivated(() => {
    activeMode.value = 'live';
});

// Navegação
watch(activeMode, (newVal) => {
    if(newVal === 'prematch') {
        router.push(`/tournament/${tournamentId.value}/play`);
    }
});

// --- HELPERS E NORMALIZAÇÃO ---

const normalizeSportKey = (rawKey: string): string => {
    if (!rawKey) return 'other';
    const k = String(rawKey).toLowerCase().trim();
    if (k.includes('soccer') || k.includes('futebol')) return 'soccer';
    if (k.includes('basket')) return 'basketball';
    if (k.includes('tennis') || k.includes('tênis')) return 'tennis';
    if (k.includes('volley') || k.includes('vôlei')) return 'volleyball';
    if (k.includes('hockey') || k.includes('hóquei')) return 'ice-hockey';
    if (k.includes('baseball') || k.includes('beisebol')) return 'baseball';
    if (k.includes('football') || k.includes('americano')) return 'american-football';
    if (k.includes('mma') || k.includes('ufc')) return 'mma';
    if (k.includes('boxing') || k.includes('boxe')) return 'boxing';
    return k; 
};

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
        gameId: String(e.externalId || e.gameId),
        sportKey: e.sportKey || 'soccer',
        league: e.league || 'Ao Vivo',
        leagueId: String(e.leagueId || e.LeagueId || '0'),
        currentMinute: e.gameTime || e.currentMinute || '0',
        homeScore: hScore,
        awayScore: aScore,
        homeOdd: e.rawOddsHome ?? e.homeOdd ?? 0,
        drawOdd: e.rawOddsDraw ?? e.drawOdd ?? 0,
        awayOdd: e.rawOddsAway ?? e.awayOdd ?? 0,
        period: e.status || e.period || 'Live'
    };
};

const getGameId = (game: any): string => {
    const validId = game.id || game.Id || game.gameId || game.externalId || game.ExternalId;
    return validId ? String(validId).trim() : '';
};

const isGameAllowedInTournament = (game: LiveGame): boolean => {
    if (tournamentEnd.value > 0 && new Date(game.commenceTime).getTime() > tournamentEnd.value) {
        return false;
    }
    const gameSportNormalized = normalizeSportKey(game.sportKey);
    if (tournamentRules.value && tournamentRules.value.sports && Array.isArray(tournamentRules.value.sports) && tournamentRules.value.sports.length > 0) {
        const allowedSportRule = tournamentRules.value.sports.find((s: any) => 
            normalizeSportKey(s.key) === gameSportNormalized
        );
        if (!allowedSportRule) return false;
        if (allowedSportRule.leagues && Array.isArray(allowedSportRule.leagues) && allowedSportRule.leagues.length > 0) {
            const allowedLeagueIds = new Set(allowedSportRule.leagues.map((l: any) => String(l.id)));
            const gameLgId = String(game.leagueId || '');
            if (!allowedLeagueIds.has(gameLgId)) return false;
        }
        return true;
    }
    return gameSportNormalized === normalizeSportKey(defaultSportKey.value);
};

const loadCurrentUser = () => {
    try {
        const stored = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
        if (stored) {
            const userData = JSON.parse(stored);
            const rawId = userData.cpf || userData.Cpf || userData.code || userData.Code || '';
            currentUser.value = String(rawId).replace(/\D/g, '');
        }
    } catch (e) { console.error(e); }
};

const loadTournamentData = async () => {
    try {
        const res = await tournamentService.getTournament(tournamentId.value, currentUser.value);
        if (res.data) {
            if (res.data.isJoined === false) {
                await Swal.fire({ title: 'Acesso Negado', text: 'Inscreva-se primeiro.', icon: 'warning', background: '#121214', color: '#fff' });
                router.push('/tournaments');
                return;
            }
            fantasyBalance.value = res.data.currentFantasyBalance ?? 1000;
            tournamentEnd.value = new Date(res.data.endDate).getTime();
            defaultSportKey.value = res.data.sport || 'soccer';
            
            if (res.data.filterRules) {
                try { 
                    tournamentRules.value = JSON.parse(res.data.filterRules); 
                } catch(e) {
                    console.error("Erro ao parsear regras do torneio:", e);
                }
            }

            await fetchInitialLiveEvents();
        }
    } catch (error) { 
        console.error("Erro torneio:", error); 
    }
};

const fetchInitialLiveEvents = async () => {
    loadingGames.value = true;
    try {
        const response = await axios.get('/sportbook/api/LiveEvents');
        if (response.data && Array.isArray(response.data)) {
            const allLive = response.data.map(normalizeGame);
            events.value = allLive.filter(isGameAllowedInTournament);
            
            events.value.forEach(e => {
                const { country, league } = normalizeCountryName(e.countryCode || '', e.league);
                const key = `${country} • ${league}`;
                openLeagues.value.add(key);
            });
        }
    } catch (e) {
        console.error("Erro Live Load:", e);
    } finally {
        loadingGames.value = false;
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
            const game = events.value.find(g => getGameId(g) === updateId);
            
            if (game) {
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
            if (isGameAllowedInTournament(newGame)) {
                const exists = events.value.some(g => g.gameId === newGame.gameId);
                if (!exists) events.value.push(newGame);
            }
        });
    });

    connection.on('RemoveGames', (endedIds: any[]) => {
        if (!endedIds || endedIds.length === 0) return;
        const idsSet = new Set(endedIds.map((id: any) => String(id).trim()));
        if (events.value.length > 0) {
            events.value = events.value.filter(e => !idsSet.has(getGameId(e)));
        }
    });

    try { await connection.start(); } catch (err) { console.error("SignalR Live Error:", err); }
};

onMounted(async () => {
    favStore.fetchFavorites();
    loadCurrentUser();
    
    // START LOADER
    startLoader();
    try {
        await loadTournamentData();
        await setupSignalR();
    } finally {
        // STOP LOADER
        finishLoader();
    }
});

onUnmounted(() => {
    if (connection) connection.stop();
});

watch(() => route.params.id, async (newId) => {
    if (newId && Number(newId) !== tournamentId.value) {
        tournamentId.value = Number(newId);
        events.value = []; 
        loadingGames.value = true;
        
        // RESTART LOADER ON CHANGE
        startLoader();
        try {
            await loadTournamentData();
        } finally {
            finishLoader();
        }
    }
});

// --- COMPUTED & LOGICA UI ---

const sortedGroups = computed(() => {
    const groups: Record<string, LiveGame[]> = {};

    const activeLeagueQuery = route.query.league as string | undefined;

    events.value.forEach(game => {
        if (!getGameId(game)) return; 
        if (selectedSport.value !== 'all' && normalizeSportKey(game.sportKey) !== normalizeSportKey(selectedSport.value)) return;

        if (activeLeagueQuery) {
            const gameLeague = game.league || 'Ao Vivo';
            if (gameLeague !== activeLeagueQuery) return;
        }

        const { country: cleanCountry, league: cleanLeague } = normalizeCountryName(game.countryCode || 'INT', game.league);
        const groupKey = `${cleanCountry} • ${cleanLeague}`;
        
        if (!groups[groupKey]) {
            groups[groupKey] = [];
            (groups[groupKey] as any).meta = {
                displayName: cleanLeague,
                country: cleanCountry,
                countryCode: game.countryCode,
                rawLeague: game.league
            };
        }
        groups[groupKey].push(game);
    });

    
    const result = Object.keys(groups).map(key => {
        const group = groups[key];
        const meta = (group as any).meta;
        return {
            key: key,
            displayName: meta.displayName,
            country: meta.country,
            countryCode: meta.countryCode,
            rawLeague: meta.rawLeague,
            games: group,
            isFavorite: favStore.isFavorite(meta.rawLeague)
        };
    });

    result.forEach(g => openLeagues.value.add(g.key));

    return result.sort((a, b) => {
        if (a.isFavorite && !b.isFavorite) return -1;
        if (!a.isFavorite && b.isFavorite) return 1;
        return a.key.localeCompare(b.key);
    });
});

const isMarketSuspended = (odd: number) => !odd || odd <= 1.01;

const handleSelection = (game: LiveGame, type: BetType) => {
    const price = type === '1' ? game.homeOdd : type === '2' ? game.awayOdd : game.drawOdd;
    if (isMarketSuspended(price)) return;
    
    const gameId = getGameId(game);
    const currentSelection = store.selections.find(s => s.id === gameId);
    
    if (currentSelection?.type === type) {
        store.removeSelection(gameId);
    } else {
        const selectionName = type === '1' ? game.homeTeam : type === '2' ? game.awayTeam : 'Empate';
        store.addOrReplaceSelection(
            gameId, 
            game.homeTeam, 
            game.awayTeam, 
            selectionName, 
            price, 
            type, 
            game.commenceTime
        );
    }
};

const toggleLeague = (key: string) => {
    if (openLeagues.value.has(key)) openLeagues.value.delete(key); else openLeagues.value.add(key);
};
const toggleLeagueFavorite = (leagueName: string, event: Event) => {
    event.stopPropagation();
    favStore.toggleFavorite(leagueName, 'live');
};

const getOddValue = (game: LiveGame, type: string) => (type === '1' ? game.homeOdd : type === 'X' ? game.drawOdd : game.awayOdd).toFixed(2);
const getOddDirection = (game: LiveGame, type: string) => { if (type === '1') return game.homeOddDir; if (type === 'X') return game.drawOddDir; return game.awayOddDir; };
const isSelected = (game: LiveGame, type: BetType) => store.selections.find(s => s.id === getGameId(game))?.type === type;
const handleImageError = (event: Event) => { (event.target as HTMLImageElement).style.display = 'none'; };

</script>

<template>
  <div class="flex h-full bg-[#0f172a] text-slate-300 font-sans relative overflow-hidden">
    
    <PageLoader 
        :is-loading="isLoading" 
        :progress="loadingProgress" 
        :is-absolute="true" 
        loading-text="Carregando Jogos..."
    />

    <div class="flex-1 flex flex-col h-full overflow-y-auto custom-scrollbar relative">
        
        <div v-if="showMenu" class="sticky top-0 z-30 shadow-xl border-b border-white/5 -mt-0 md:-mt-0 bg-[#0f172a]">
            <TournamentSportsMenu 
                :games="events" 
                v-model:activeMode="activeMode"
                v-model:selectedSport="selectedSport"
                v-model:selectedDate="dataSelecionada"
            />
        </div>

        <div class="max-w-[1200px] mx-auto px-2 md:px-4 space-y-3 w-full pb-20 pt-4">
            
            <div v-if="loadingGames && !isLoading" class="space-y-3 pt-2">
                <div v-for="i in 5" :key="i" class="bg-[#1a2c38] h-20 rounded animate-pulse border border-white/5"></div>
            </div>

            <div v-else-if="sortedGroups.length === 0" class="flex flex-col items-center justify-center min-h-[300px] text-center p-8 bg-[#1a2c38]/30 rounded-lg border border-dashed border-white/10 mt-4">
                <AlertCircle class="w-12 h-12 text-slate-500 mb-4" />
                <h3 class="text-xl font-bold text-white mb-2">Sem Jogos Ao Vivo</h3>
                <p class="text-sm text-slate-400">
                    Nenhum jogo ao vivo compatível com este torneio no momento.
                </p>
                <button @click="router.push(`/tournament/${tournamentId}/play`)" class="mt-4 text-blue-400 hover:text-blue-300 font-bold text-sm border border-blue-500/30 px-4 py-2 rounded hover:bg-blue-500/10 transition-all">
                    Ir para Pré-Jogo
                </button>
            </div>

            <div v-else v-for="group in sortedGroups" :key="group.key" class="rounded overflow-hidden">
                
                <div @click="toggleLeague(group.key)" class="bg-[#1a2c38]/90 backdrop-blur-sm p-2 flex items-center justify-between border-l-2 cursor-pointer hover:bg-[#213746] transition-all select-none"
                     :class="group.isFavorite ? 'border-yellow-500 bg-yellow-500/5' : 'border-blue-500'">
                    
                    <div class="flex items-center gap-2">
                        <button @click="toggleLeagueFavorite(group.rawLeague, $event)" class="p-1 rounded hover:bg-white/5 group/star">
                            <Star class="w-3.5 h-3.5 transition-colors" :class="group.isFavorite ? 'text-yellow-400 fill-yellow-400' : 'text-gray-500 fill-transparent group-hover/star:text-yellow-200'" />
                        </button>
                        <img :src="getFlag(group.country === 'Brasil' ? 'Brazil' : group.country, group.countryCode)" class="w-4 h-3 rounded-[1px] shadow-sm bg-black/20" @error="handleImageError" />
                        <h3 class="text-white font-bold text-xs uppercase tracking-wide">
                            {{ group.country }} • <span class="text-gray-300">{{ group.displayName }}</span>
                        </h3>
                    </div>
                    <component :is="openLeagues.has(group.key) ? ChevronDown : ChevronRight" class="w-4 h-4 text-gray-400" />
                </div>

                <div v-show="openLeagues.has(group.key)" class="bg-[#0f172a] border-x border-b border-[#1a2c38]">
                    <div v-for="game in group.games" :key="game.gameId" class="py-2 px-2 border-b border-white/5 flex flex-row items-center gap-2 transition-all hover:bg-white/[0.02]">
                        
                        <div class="flex items-center bg-[#1e293b] border border-blue-500/20 text-white rounded-[4px] h-[40px] min-w-[50px] w-[50px] shadow-sm relative overflow-hidden shrink-0 select-none">
                             <div class="flex flex-col items-center justify-center w-[24px] h-full bg-[#0f172a]/80 border-r border-white/5">
                                  <div v-if="game.period === 'Live'" class="relative flex h-1 w-1 mb-0.5">
                                      <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-500 opacity-75"></span>
                                      <span class="relative inline-flex rounded-full h-1 w-1 bg-red-500"></span>
                                  </div>
                                  <span class="text-[10px] font-bold leading-none text-[#00E701]">{{ game.currentMinute }}'</span>
                             </div>
                             
                             <div class="flex flex-col items-center justify-center flex-1 h-full gap-0.5 bg-[#1e293b]">
                                  <span class="text-[11px] font-bold leading-none">{{ game.homeScore }}</span>
                                  <span class="text-[11px] font-bold leading-none">{{ game.awayScore }}</span>
                             </div>
                        </div>

                        <div class="flex flex-col justify-center gap-1 flex-1 min-w-0 h-[40px]">
                            <div class="flex items-center gap-1.5">
                                <TeamLogo :teamName="game.homeTeam" :remoteUrl="game.homeTeamLogo" size="w-3.5 h-3.5" />
                                <span class="text-[11px] font-medium text-white/90 truncate leading-none pt-0.5">{{ game.homeTeam }}</span>
                            </div>
                            <div class="flex items-center gap-1.5">
                                <TeamLogo :teamName="game.awayTeam" :remoteUrl="game.awayTeamLogo" size="w-3.5 h-3.5" />
                                <span class="text-[11px] font-medium text-white/90 truncate leading-none pt-0.5">{{ game.awayTeam }}</span>
                            </div>
                        </div>

                        <div class="flex gap-1 shrink-0">
                            <button v-for="type in ['1','X','2']" :key="type" 
                                @click="handleSelection(game, type as BetType)" 
                                :disabled="isMarketSuspended(type === '1' ? game.homeOdd : type === 'X' ? game.drawOdd : game.awayOdd)"
                                :class="['w-[48px] md:w-[60px] h-[40px] rounded-sm flex flex-col items-center justify-center transition-all group relative overflow-hidden border border-transparent',
                                isMarketSuspended(type === '1' ? game.homeOdd : type === 'X' ? game.drawOdd : game.awayOdd) ? 'opacity-50 cursor-not-allowed bg-[#1a2c38]' : 
                                isSelected(game, type as BetType) ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]',
                                (type === '1' ? game.homeOddFlash : type === 'X' ? game.drawOddFlash : game.awayOddFlash) ? 'animate-flash' : '']">

                                <span class="text-[8px] font-bold uppercase mb-0.5 tracking-wide opacity-70" :class="isSelected(game, type as BetType) ? 'text-white' : 'text-gray-400'">
                                    {{ type === '1' ? '1' : type === 'X' ? 'X' : '2' }}
                                </span>

                                <div class="flex items-center gap-0.5 justify-center w-full">
                                    <Lock v-if="isMarketSuspended(type === '1' ? game.homeOdd : type === 'X' ? game.drawOdd : game.awayOdd)" class="w-3 h-3 text-gray-500" />
                                    <template v-else>
                                         <ArrowUp v-if="getOddDirection(game, type) === 'up'" class="w-2 h-2 text-[#00E701]" />
                                         <ArrowDown v-if="getOddDirection(game, type) === 'down'" class="w-2 h-2 text-red-500" />
                                         <span class="text-[11px] font-bold transition-colors leading-none" 
                                            :class="[isSelected(game, type as BetType) ? 'text-white' : 'text-white group-hover:text-blue-400',
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
.custom-scrollbar::-webkit-scrollbar { width: 4px; height: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #0f172a; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #475569; }

@keyframes flash-white {
    0% { background-color: rgba(255, 255, 255, 0.3); }
    100% { background-color: inherit; }
}
.animate-flash { animation: flash-white 0.5s ease-out; }

@keyframes bounce-in {
    0% { transform: scale(0.5); opacity: 0; }
    50% { transform: scale(1.1); }
    100% { transform: scale(1); opacity: 1; }
}
.animate-bounce-in { animation: bounce-in 0.3s ease-out; }
</style>