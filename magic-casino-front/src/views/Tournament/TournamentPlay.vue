<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, nextTick, onActivated } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import Swal from 'sweetalert2';
import { ChevronDown, ChevronRight, AlertCircle, Star } from 'lucide-vue-next';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { useBetStore, type BetType } from '../../stores/useBetStore';
import tournamentService from '../../services/Tournament/TournamentService';
import sportbookService from '../../services/Sportbook/SportbookService';
import TeamLogo from '../../components/TeamLogo.vue';
import { normalizeCountryName } from '../../utils/countryTranslations';
import { getFlag } from '../../utils/flags';
import TournamentSportsMenu from '../../components/Tournament/TournamentSportsMenu.vue';
import { useFavoritesStore } from '../../stores/useFavoritesStore';
import PageLoader from '../../components/PageLoader.vue';
import { usePageLoader } from '../../composables/usePageLoader';

const route = useRoute();
const router = useRouter();
const store = useBetStore();
const favStore = useFavoritesStore();
const emit = defineEmits(['update-header']);

const { isLoading, loadingProgress, startLoader, finishLoader } = usePageLoader();

const tournamentId = ref(Number(route.params.id));
const sportKey = ref('soccer');
const currentUser = ref('');
const tournamentEnd = ref<number>(0);

const isLoadingGames = ref(true);
const isTournamentDataLoaded = ref(false); 
const games = ref<any[]>([]);
const openLeagues = ref<Set<string>>(new Set());

// Estado visual do botão de modo (Pré/Live)
const activeMode = ref<'prematch' | 'live'>('prematch');
const selectedSport = ref<string>(''); 
const dataSelecionada = ref<string>('all');

const showMenu = computed(() => {
    return isTournamentDataLoaded.value && (isLoadingGames.value || games.value.length > 0);
});

let connection: HubConnection | null = null;

// Garante que o botão esteja em "Pré-Jogo" ao entrar na aba
onActivated(() => {
    activeMode.value = 'prematch';
});

// Navegação para a rota Live
watch(activeMode, (newVal) => {
    if (newVal === 'live') {
        router.push(`/tournament/${tournamentId.value}/live`);
    }
});

const getGameId = (game: any): string => {
    const validId = game.id || game.Id || game.gameId || game.externalId || game.ExternalId;
    if (validId) {
        return String(validId).trim();
    }
    return '';
};

// ✅ FUNÇÃO DE NAVEGAÇÃO PARA DETALHES
const goToGame = (game: any) => {
    const id = getGameId(game);
    if (!id) return;
    // Redireciona para a rota de detalhes dentro do contexto do torneio
    router.push(`/tournament/${tournamentId.value}/match/${id}`);
};

onMounted(async () => {
    favStore.fetchFavorites();
    loadCurrentUser();
    startLoader();
    try {
        await loadTournamentData();
        setupSignalR();
    } finally {
        finishLoader();
    }
});

onUnmounted(() => {
    if (connection) connection.stop();
});

watch(() => route.params.id, async (newId) => {
    if (newId) {
        tournamentId.value = Number(newId);
        games.value = [];
        isLoadingGames.value = true;
        isTournamentDataLoaded.value = false;
        openLeagues.value.clear();
        startLoader();
        try {
            await loadTournamentData();
        } finally {
            finishLoader();
        }
    }
});

watch(() => route.query.league, (newLeague) => {
    if (newLeague) {
        nextTick(() => {
            sortedGroups.value.forEach(g => openLeagues.value.add(g.key));
        });
    }
}, { immediate: true });

const toggleLeagueFavorite = (leagueName: string, event: Event) => {
    event.stopPropagation();
    favStore.toggleFavorite(leagueName, sportKey.value);
};

const setupSignalR = async () => {
    const signalRUrl = "/gameHub";
    connection = new HubConnectionBuilder()
        .withUrl(signalRUrl)
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Information)
        .build();

    connection.on("RemoveGames", (idsToRemove: any[]) => {
        if (!idsToRemove || idsToRemove.length === 0) return;
        const idsSet = new Set(idsToRemove.map(id => String(id).trim()));
        if (games.value.length > 0) {
            games.value = games.value.filter(game => !idsSet.has(getGameId(game)));
        }
    });

    connection.on("GameWentLive", (liveGames: any[]) => {
        if (!liveGames || liveGames.length === 0) return;
        const liveIdsSet = new Set(liveGames.map(g => String(g.externalId || g.ExternalId || g.id || g.Id).trim()));
        if (games.value.length > 0) {
            games.value = games.value.filter(game => !liveIdsSet.has(getGameId(game)));
        }
    });

    connection.on('LiveOddsUpdate', (updatedGames: any[]) => {
        if (!updatedGames || !Array.isArray(updatedGames)) return;
        updatedGames.forEach(update => {
            const updateId = String(update.id || update.gameId || update.Id || '').trim();
            if (!updateId) return;
            const game = games.value.find(g => getGameId(g) === updateId);
            if (game) {
                if (update.homeOdd) game.rawOddsHome = update.homeOdd;
                if (update.drawOdd) game.rawOddsDraw = update.drawOdd;
                if (update.awayOdd) game.rawOddsAway = update.awayOdd;
                if (update.HomeOdd) game.RawOddsHome = update.HomeOdd;
                if (update.DrawOdd) game.RawOddsDraw = update.DrawOdd;
                if (update.AwayOdd) game.RawOddsAway = update.AwayOdd;
                if (update.time) game.commenceTime = update.time;
            }
        });
    });

    try { await connection.start(); } catch (e) { console.error("SignalR Error:", e); }
};

const loadCurrentUser = () => {
    try {
        const stored = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
        if (stored) {
            const userData = JSON.parse(stored);
            const rawId = userData.id || userData.Id || userData.userId || userData.userName || userData.user_name || userData.cpf || userData.Cpf || userData.code || userData.Code || '';
            currentUser.value = String(rawId).trim();
        }
    } catch (e) { console.error("Erro user:", e); }
};

const loadTournamentData = async () => {
    try {
        const tournamentPromise = tournamentService.getTournament(tournamentId.value, currentUser.value);
        const gamesPromise = sportbookService.getEventsBySport('soccer', 1000); 

        const [resTournament, resGames] = await Promise.all([
            tournamentPromise,
            gamesPromise
        ]);

        let rules = null;
        if (resTournament.data) {
            if (resTournament.data.isJoined === false) {
                await Swal.fire({ title: 'Acesso Negado', text: 'Inscreva-se primeiro.', icon: 'warning', confirmButtonColor: '#3b82f6', background: '#121214', color: '#fff' });
                router.push('/tournaments');
                return;
            }
            sportKey.value = resTournament.data.sport || 'soccer';
            selectedSport.value = sportKey.value;
            isTournamentDataLoaded.value = true;
            tournamentEnd.value = new Date(resTournament.data.endDate).getTime();
            
            if (resTournament.data.filterRules) { 
                try { rules = JSON.parse(resTournament.data.filterRules); } catch (e) { } 
            }
        }

        if (sportKey.value === 'soccer') {
            processGamesList(resGames.data || [], rules);
        } else {
            await loadRealGames(sportKey.value, rules);
        }

    } catch (error: any) { 
        console.error(error); 
    }
};

const processGamesList = (allGames: any[], filterRules: any) => {
    let filtered = allGames;

    const startOfLocalDay = new Date();
    startOfLocalDay.setHours(0, 0, 0, 0);
    const minTime = startOfLocalDay.getTime();

    if (filterRules?.sports) {
        const activeSport = filterRules.sports.find((s: any) => s.key === sportKey.value);
        if (activeSport?.leagues) {
            const allowed = new Set(activeSport.leagues.map((l: any) => String(l.id)));
            filtered = filtered.filter((g: any) => allowed.has(String(g.leagueId || g.league?.id)));
        }
    }

    if (tournamentEnd.value > 0) {
        const TECH_BUFFER = 30 * 60 * 1000; 
        filtered = filtered.filter((g: any) => {
            const gameTime = new Date(g.commenceTime).getTime();
            return gameTime <= (tournamentEnd.value + TECH_BUFFER);
        });
    }

    filtered = filtered.filter((g: any) => {
        const gameTime = new Date(g.commenceTime).getTime();
        return gameTime >= minTime;
    });

    games.value = filtered;
    isLoadingGames.value = false;
};

const loadRealGames = async (sport: string, filterRules: any) => {
    isLoadingGames.value = true;
    try {
        const res = await sportbookService.getEventsBySport(sport, 1000);
        processGamesList(res.data || [], filterRules);
    } catch (error) { 
        console.error("Erro games:", error); 
        isLoadingGames.value = false;
    } 
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
    const gameId = getGameId(game);
    const priceStr = getOdd(game, type);
    const price = parseFloat(priceStr);
    if (price <= 1.0 || isNaN(price)) return;
    if (getSelectedType(gameId) === type) { store.removeSelection(gameId); return; }
    const hTeam = game.homeTeam || game.HomeTeam || '';
    const aTeam = game.awayTeam || game.AwayTeam || '';
    const selectionName = type === '1' ? hTeam : type === '2' ? aTeam : 'Empate';
    const time = game.commenceTime || game.CommenceTime || new Date().toISOString();
    
    // ✅ CORREÇÃO: Cast (store as any) para evitar erro TS2554 (8 argumentos)
    (store as any).addOrReplaceSelection(gameId, hTeam, aTeam, selectionName, price, type, time, {
        isTournament: true,
        tournamentId: tournamentId.value
    });
};

const sortedGroups = computed(() => {
    const groups: Record<string, any[]> = {};
    const activeLeagueQuery = route.query.league as string | undefined;

    games.value.forEach(game => {
        if (!getGameId(game)) return;
        
        if (dataSelecionada.value !== 'all') {
            const d = new Date(game.commenceTime);
            const year = d.getFullYear();
            const month = String(d.getMonth() + 1).padStart(2, '0');
            const day = String(d.getDate()).padStart(2, '0');
            const gameDate = `${year}-${month}-${day}`;
            
            if (gameDate !== dataSelecionada.value) return;
        }

        if (selectedSport.value !== 'all' && selectedSport.value !== '') {
            const gSport = (game.sportKey || game.Sport || game.sport || 'soccer').toLowerCase();
            if (!gSport.includes(selectedSport.value.toLowerCase())) return;
        }

        if (activeLeagueQuery) {
            const gameLeague = game.league || game.League || '';
            if (gameLeague !== activeLeagueQuery) return;
        }

        const rawLg = game.league || game.League || 'Outros';
        const rawCountry = game.countryCode ? 'Country' : 'Internacional';
        const { country: cleanCountry, league: cleanLeague } = normalizeCountryName(String(rawCountry), String(rawLg));
        const groupKey = `${cleanCountry} • ${cleanLeague}`;
        
        if (!groups[groupKey]) {
            groups[groupKey] = [];
            (groups[groupKey] as any).meta = { displayName: cleanLeague, country: cleanCountry, countryCode: game.countryCode, rawLeague: rawLg };
        }
        groups[groupKey].push(game);
    });

    const result = Object.keys(groups).map(key => {
        const group = groups[key];
        const meta = (group as any).meta;
        return { key: key, displayName: meta.displayName, country: meta.country, countryCode: meta.countryCode, rawLeague: meta.rawLeague, games: group, isFavorite: favStore.isFavorite(meta.rawLeague) };
    });

    if (activeLeagueQuery && result.length > 0) {
        result.forEach(g => openLeagues.value.add(g.key));
    } else if (openLeagues.value.size === 0 && result.length > 0) {
        result.forEach(g => openLeagues.value.add(g.key));
    }

    return result.sort((a, b) => (a.isFavorite === b.isFavorite ? a.key.localeCompare(b.key) : a.isFavorite ? -1 : 1));
});

const toggleLeague = (key: string) => {
    if (openLeagues.value.has(key)) openLeagues.value.delete(key); else openLeagues.value.add(key);
};

const formatTime = (d: string) => new Date(d).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
const formatDate = (d: string) => {
    const date = new Date(d); date.setHours(0, 0, 0, 0);
    const today = new Date(); today.setHours(0, 0, 0, 0);
    const tomorrow = new Date(today); tomorrow.setDate(tomorrow.getDate() + 1);
    if (date.getTime() === today.getTime()) return 'HOJE';
    if (date.getTime() === tomorrow.getTime()) return 'AMANHÃ';
    return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
};
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
            
            <div v-if="showMenu" class="sticky top-0 z-30 shadow-xl border-b border-white/5 bg-[#0f172a]">
                <TournamentSportsMenu :games="games" v-model:activeMode="activeMode"
                    v-model:selectedSport="selectedSport" v-model:selectedDate="dataSelecionada" />
            </div>

            <div class="max-w-[1200px] mx-auto px-2 md:px-4 space-y-3 w-full pb-20 pt-2">
                
                <div v-if="isLoadingGames && !isLoading" class="space-y-3 pt-2">
                    <div v-for="i in 5" :key="i" class="bg-[#1a2c38] h-20 rounded animate-pulse border border-white/5"></div>
                </div>

                <div v-else-if="sortedGroups.length === 0" class="flex flex-col items-center justify-center min-h-[400px] text-center p-8">
                    <div class="bg-[#1e293b] p-6 rounded-full mb-4 border border-white/5 shadow-2xl">
                        <AlertCircle class="w-12 h-12 text-slate-500" />
                    </div>
                    <h3 class="text-xl font-bold text-white mb-2">Sem Jogos Disponíveis</h3>
                    <button @click="router.push('/tournaments')" class="mt-6 px-6 py-2 bg-blue-600 hover:bg-blue-500 text-white font-bold rounded-lg transition-all shadow-lg text-sm uppercase tracking-wide">Voltar para o Lobby</button>
                </div>

                <div v-else v-for="group in sortedGroups" :key="group.key" class="rounded overflow-hidden">
                    <div @click="toggleLeague(group.key)" class="bg-[#1a2c38]/90 backdrop-blur-sm p-2 flex items-center justify-between border-l-2 cursor-pointer hover:bg-[#213746] transition-all select-none" :class="group.isFavorite ? 'border-yellow-500 bg-yellow-500/5' : 'border-blue-500'">
                        <div class="flex items-center gap-2">
                            <button @click="toggleLeagueFavorite(group.rawLeague, $event)" class="p-1 rounded hover:bg-white/5 transition-all transform active:scale-95 group/star">
                                <Star class="w-3.5 h-3.5 transition-colors duration-200" :class="group.isFavorite ? 'text-yellow-400 fill-yellow-400' : 'text-gray-500 fill-transparent group-hover/star:text-yellow-200'" />
                            </button>
                            <img :src="getFlag(group.country === 'Brasil' ? 'Brazil' : group.country, group.countryCode)" class="w-4 h-3 rounded-[1px] shadow-sm object-cover bg-black/20" @error="handleImageError" />
                            <h3 class="text-white font-bold text-xs uppercase tracking-wide" :class="group.isFavorite ? 'text-yellow-100' : ''">
                                {{ group.country }} • <span class="text-gray-300">{{ group.displayName }}</span>
                            </h3>
                            <span class="text-[9px] text-gray-400 font-bold bg-black/20 px-1.5 py-0.5 rounded-full">{{ group.games?.length || 0 }}</span>
                        </div>
                        <component :is="openLeagues.has(group.key) ? ChevronDown : ChevronRight" class="w-4 h-4 text-gray-400" />
                    </div>

                    <div v-show="openLeagues.has(group.key)" class="bg-[#0f172a] border-x border-b border-[#1a2c38]">
                        <div v-for="game in group.games" :key="game.id">
                            
                            <div class="flex py-2 px-2 border-b border-white/5 flex-row items-center gap-2 transition-all hover:bg-white/[0.02]">
                                
                                <div class="flex flex-col items-center justify-center gap-0.5 w-[50px] md:w-[60px] text-center shrink-0">
                                    <div class="text-[9px] font-bold text-blue-400 leading-none">{{ formatDate(game.commenceTime) }}</div>
                                    <div class="text-white text-[10px] font-bold leading-none">{{ formatTime(game.commenceTime) }}</div>
                                </div>

                                <div class="flex-1 w-full text-white border-l border-white/5 pl-2 md:pl-3 min-w-0 cursor-pointer hover:text-blue-400 transition-colors"
                                     @click="goToGame(game)">
                                    <div class="flex flex-col gap-1.5 justify-center h-full">
                                        <div class="flex items-center gap-2">
                                            <TeamLogo :teamName="game.homeTeam" :remoteUrl="game.homeTeamLogo" size="w-4 h-4" />
                                            <span class="font-medium text-xs text-white/90 truncate">{{ game.homeTeam }}</span>
                                        </div>
                                        <div class="flex items-center gap-2">
                                            <TeamLogo :teamName="game.awayTeam" :remoteUrl="game.awayTeamLogo" size="w-4 h-4" />
                                            <span class="font-medium text-xs text-white/90 truncate">{{ game.awayTeam }}</span>
                                        </div>
                                    </div>
                                </div>

                                <div class="flex gap-1 w-auto shrink-0">
                                    <button @click="handleBetClick(game, '1')" :disabled="parseFloat(getOdd(game, '1')) <= 1.0" 
                                        :class="['w-[50px] md:w-[70px] h-auto py-1.5 rounded-sm flex flex-col items-center justify-center transition-all group border border-transparent', 
                                        parseFloat(getOdd(game, '1')) <= 1.0 ? 'opacity-50 cursor-not-allowed bg-[#1a2c38]' : getSelectedType(getGameId(game)) === '1' ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]']">
                                        <span class="text-[9px] font-bold uppercase mb-0.5 tracking-wide" :class="getSelectedType(getGameId(game)) === '1' ? 'text-white' : 'text-gray-400'">1</span>
                                        <span class="text-xs font-bold" :class="getSelectedType(getGameId(game)) === '1' ? 'text-white' : 'text-white group-hover:text-blue-400'">{{ getOdd(game, '1') }}</span>
                                    </button>

                                    <button v-if="parseFloat(getOdd(game, 'X')) > 1.01" @click="handleBetClick(game, 'X')" 
                                        :class="['w-[50px] md:w-[70px] h-auto py-1.5 rounded-sm flex flex-col items-center justify-center transition-all group border border-transparent', 
                                        getSelectedType(getGameId(game)) === 'X' ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]']">
                                        <span class="text-[9px] font-bold uppercase mb-0.5 tracking-wide" :class="getSelectedType(getGameId(game)) === 'X' ? 'text-white' : 'text-gray-400'">X</span>
                                        <span class="text-xs font-bold" :class="getSelectedType(getGameId(game)) === 'X' ? 'text-white' : 'text-white group-hover:text-blue-400'">{{ getOdd(game, 'X') }}</span>
                                    </button>

                                    <button @click="handleBetClick(game, '2')" :disabled="parseFloat(getOdd(game, '2')) <= 1.0" 
                                        :class="['w-[50px] md:w-[70px] h-auto py-1.5 rounded-sm flex flex-col items-center justify-center transition-all group border border-transparent', 
                                        parseFloat(getOdd(game, '2')) <= 1.0 ? 'opacity-50 cursor-not-allowed bg-[#1a2c38]' : getSelectedType(getGameId(game)) === '2' ? 'bg-blue-600 shadow-md' : 'bg-[#1a2c38] hover:bg-[#213746]']">
                                        <span class="text-[9px] font-bold uppercase mb-0.5 tracking-wide" :class="getSelectedType(getGameId(game)) === '2' ? 'text-white' : 'text-gray-400'">2</span>
                                        <span class="text-xs font-bold" :class="getSelectedType(getGameId(game)) === '2' ? 'text-white' : 'text-white group-hover:text-blue-400'">{{ getOdd(game, '2') }}</span>
                                    </button>
                                </div>
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
@keyframes bounce-in { 0% { transform: scale(0.5); opacity: 0; } 50% { transform: scale(1.1); } 100% { transform: scale(1); opacity: 1; } }
.animate-bounce-in { animation: bounce-in 0.3s ease-out; }
</style>