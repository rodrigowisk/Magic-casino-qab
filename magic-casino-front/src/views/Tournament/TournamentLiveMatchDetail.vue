<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ArrowLeft, ChevronDown, ChevronRight, Trophy, BarChart3, Lock, Timer } from 'lucide-vue-next';
import Swal from 'sweetalert2';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';

// --- SERVIÇOS ---
import SportsService from '../../services/SportsService'; 
import TournamentService from '../../services/Tournament/TournamentService';

// --- COMPONENTES & STORES ---
import TeamLogo from '../../components/TeamLogo.vue';
import PageLoader from '../../components/PageLoader.vue';
import { usePageLoader } from '../../composables/usePageLoader';
import { useBetStore, type BetType } from '../../stores/useBetStore';

// --- HELPERS ---
const asString = (v: string | number | null | undefined | string[]): string => {
    if (Array.isArray(v)) return v[0] || '';
    return String(v ?? '').trim();
};

const route = useRoute();
const router = useRouter();
const betStore = useBetStore();
const { isLoading, loadingProgress, startLoader, finishLoader } = usePageLoader();

// --- ESTADOS ---
const tournamentId = ref(Number(route.params.id));
const gameId = ref(asString(route.params.gameId)); 
const event = ref<any>(null);
const currentUser = ref('');
const expandedMarkets = ref<Set<string>>(new Set());
let connection: HubConnection | null = null;

// --- CARREGAMENTO ---
const loadCurrentUser = () => {
    try {
        const stored = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
        if (stored) {
            const u = JSON.parse(stored);
            const rawId = u.cpf || u.Cpf || u.code || u.Code || u.id || '';
            currentUser.value = String(rawId).replace(/\D/g, '');
        }
    } catch (e) { console.error(e); }
};

const initPage = async () => {
    startLoader();
    try {
        loadCurrentUser();

        // 1. Validação de Torneio
        const tourRes = await TournamentService.getTournament(tournamentId.value, currentUser.value);
        if (!tourRes.data || tourRes.data.isJoined === false) {
            await Swal.fire({ 
                title: 'Acesso Negado', 
                text: 'Você precisa entrar no torneio para ver este jogo.', 
                icon: 'warning', 
                background: '#1e293b', 
                color: '#fff',
                confirmButtonColor: '#3b82f6'
            });
            router.push(`/tournaments`);
            return;
        }

        // 2. Buscar Detalhes Iniciais
        await fetchDetails();

        // 3. Iniciar SignalR para atualizações ao vivo
        await setupSignalR();

    } catch (err) {
        console.error("Erro ao carregar:", err);
        Swal.fire({ toast: true, icon: 'error', title: 'Jogo indisponível.', background: '#1e293b', color: '#fff' });
        router.back();
    } finally {
        finishLoader();
    }
};

const fetchDetails = async () => {
    const eventRes = await SportsService.getEventDetails(gameId.value);
    if (eventRes) {
        const rawOdds = eventRes.odds || eventRes.Odds;
        const safeOdds = Array.isArray(rawOdds) ? rawOdds : [];

        // Normalização do Placar
        let hScore = eventRes.homeScore || 0;
        let aScore = eventRes.awayScore || 0;
        if (eventRes.score && typeof eventRes.score === 'string' && eventRes.score.includes('-')) {
            const parts = eventRes.score.split('-');
            hScore = parseInt(parts[0]) || 0;
            aScore = parseInt(parts[1]) || 0;
        }

        event.value = {
            ...eventRes,
            id: String(eventRes.id || eventRes.externalId || eventRes.gameId),
            homeTeam: eventRes.homeTeam || eventRes.HomeTeam,
            awayTeam: eventRes.awayTeam || eventRes.AwayTeam,
            league: eventRes.league || eventRes.League,
            commenceTime: eventRes.commenceTime || eventRes.CommenceTime,
            homeScore: hScore,
            awayScore: aScore,
            currentMinute: eventRes.gameTime || eventRes.currentMinute || eventRes.time || '0',
            period: eventRes.status || eventRes.period || 'Scheduled', 
            odds: safeOdds
        };
    } else {
        throw new Error("Jogo não encontrado.");
    }
};

// --- SIGNALR (AO VIVO) ---
const setupSignalR = async () => {
    const signalRUrl = "/gameHub";
    connection = new HubConnectionBuilder()
        .withUrl(signalRUrl)
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Information)
        .build();

    connection.on('LiveOddsUpdate', (updatedGames: any[]) => {
        if (!event.value || !updatedGames || !Array.isArray(updatedGames)) return;
        
        const update = updatedGames.find(u => {
            const uId = String(u.id || u.gameId).trim();
            return uId === event.value.id;
        });

        if (update) {
            if (update.time) event.value.currentMinute = update.time;
            if (update.status) event.value.period = update.status;
            
            if (update.score && update.score.includes('-')) {
                const parts = update.score.split('-');
                event.value.homeScore = parseInt(parts[0]) || 0;
                event.value.awayScore = parseInt(parts[1]) || 0;
            }

            if (update.homeOdd || update.drawOdd || update.awayOdd) {
                updateMainMarketOdds(update);
            }
        }
    });

    try { await connection.start(); } catch (err) { console.error("SignalR Detail Error:", err); }
};

const updateMainMarketOdds = (update: any) => {
    if (!event.value.odds || !Array.isArray(event.value.odds)) return;
    
    const market = event.value.odds.find((m: any) => {
        const name = translateMarket(m.marketName || m.MarketName).toLowerCase();
        return name === 'resultado final';
    });

    if (market) {
        if (update.homeOdd) {
            const homeOutcome = event.value.odds.find((o: any) => o.marketName === market.marketName && (o.outcomeName === '1' || o.outcomeName === event.value.homeTeam));
            if (homeOutcome) homeOutcome.price = update.homeOdd;
        }
        if (update.drawOdd) {
            const drawOutcome = event.value.odds.find((o: any) => o.marketName === market.marketName && (o.outcomeName === 'X' || o.outcomeName === 'Draw' || o.outcomeName === 'Empate'));
            if (drawOutcome) drawOutcome.price = update.drawOdd;
        }
        if (update.awayOdd) {
            const awayOutcome = event.value.odds.find((o: any) => o.marketName === market.marketName && (o.outcomeName === '2' || o.outcomeName === event.value.awayTeam));
            if (awayOutcome) awayOutcome.price = update.awayOdd;
        }
    }
};

// --- COMPUTED & FORMATTERS ---
const isLive = computed(() => {
    if (!event.value) return false;
    const p = (event.value.period || '').toLowerCase();
    // Verifica se é status de jogo ao vivo
    return p.includes('live') || p.includes('1h') || p.includes('2h') || p.includes('half') || (event.value.currentMinute && event.value.currentMinute !== '0');
});

const formatGameDate = (dateStr: string) => {
    if (!dateStr) return '--/--';
    const date = new Date(dateStr);
    return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
};

const formatGameTime = (dateStr: string) => {
    if (!dateStr) return '--:--';
    const date = new Date(dateStr);
    return date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
};

// --- LÓGICA DE MERCADOS ---
const translateMarket = (key: string) => {
    const k = asString(key).toLowerCase().trim();
    if (['1x2', 'full time result', 'match winner', 'money line', 'resultado final'].includes(k)) return 'Resultado Final';
    if (k.includes('double chance') || k.includes('dupla chance')) return 'Dupla Hipótese';
    if (k.includes('goals over under') || k.includes('total goals')) return 'Gols Mais/Menos';
    if (k.includes('handicap')) return 'Handicap';
    if (k.includes('both teams to score') || k === 'btts' || k.includes('ambos marcam')) return 'Ambos Marcam';
    if (k.includes('correct score')) return 'Placar Correto';
    return key;
};

const groupedMarkets = computed<Record<string, any[]>>(() => {
    const evt = event.value;
    if (!evt) return {};

    const raw = evt.odds || evt.Odds;
    const oddsList: any[] = Array.isArray(raw) ? raw : [];
    const groups: Record<string, any[]> = {};

    oddsList.forEach((odd: any) => {
        const name = translateMarket(odd.marketName || odd.MarketName);
        if (!groups[name]) groups[name] = [];
        groups[name].push(odd);
    });

    const priority = ['Resultado Final', 'Gols Mais/Menos', 'Ambos Marcam', 'Dupla Hipótese'];
    const sorted: Record<string, any[]> = {};
    
    priority.forEach(p => { 
        if (groups[p]) sorted[p] = groups[p]; 
    });
    
    Object.keys(groups).sort().forEach(k => { 
        if (!priority.includes(k) && groups[k]) sorted[k] = groups[k]; 
    });

    return sorted;
});

watch(groupedMarkets, (newVal) => {
    if (!newVal) return;
    const important = ['Resultado Final', 'Gols Mais/Menos', 'Ambos Marcam'];
    Object.keys(newVal).forEach(k => {
        if (important.includes(k) || expandedMarkets.value.size < 3) expandedMarkets.value.add(k);
    });
}, { immediate: true });

const toggleMarket = (name: string) => {
    if (expandedMarkets.value.has(name)) expandedMarkets.value.delete(name);
    else expandedMarkets.value.add(name);
};

// --- AÇÕES DE APOSTA ---
const getSelectionUniqueId = (gId: string, market: string, outcome: string): string => {
    return `TOURNAMENT_${gId}_${market}_${asString(outcome)}`.replace(/\s+/g, '');
};

const isSelected = (odd: any, marketNameGrouped: string): boolean => {
    if (!event.value) return false;
    const uniqueId = getSelectionUniqueId(event.value.id, marketNameGrouped, odd.outcomeName || odd.OutcomeName);
    return betStore.selections.some(s => s.id === uniqueId);
};

const getSelectedCountForMarket = (marketNameGrouped: string) => {
    if (!event.value) return 0;
    return betStore.selections.filter(s => {
        return s.id && s.id.startsWith('TOURNAMENT_') && s.id.includes(`_${event.value.id}_`) && s.id.includes(marketNameGrouped.replace(/\s+/g, ''));
    }).length;
};

const getBetType = (odd: any, marketGrouped: string): string => {
    if (marketGrouped === 'Resultado Final' && event.value) {
        const out = asString(odd.outcomeName || odd.OutcomeName).toLowerCase();
        const home = asString(event.value.homeTeam).toLowerCase();
        const away = asString(event.value.awayTeam).toLowerCase();
        if (out === '1' || out === home || out.includes(home)) return '1';
        if (out === '2' || out === away || out.includes(away)) return '2';
        if (['x', 'draw', 'empate'].includes(out)) return 'X';
    }
    return 'O'; 
};

const handleSelection = (odd: any, marketNameGrouped: string) => {
    if (!event.value) return;
    
    const outcomeName = asString(odd.outcomeName || odd.OutcomeName);
    const price = Number(odd.price || odd.Price);
    
    if (!price || price <= 1.0) return;

    const uniqueId = getSelectionUniqueId(event.value.id, marketNameGrouped, outcomeName);
    const alreadySelected = betStore.selections.find(s => s.id === uniqueId);

    if (alreadySelected) {
        betStore.removeSelection(uniqueId);
        return;
    }

    const existingInMarket = betStore.selections.find(s => {
        return s.id.startsWith(`TOURNAMENT_${event.value.id}`) && s.id.includes(marketNameGrouped.replace(/\s+/g, ''));
    });
    if (existingInMarket) {
        betStore.removeSelection(existingInMarket.id);
    }

    const betType = getBetType(odd, marketNameGrouped);
    let selName = outcomeName;
    if (betType === '1') selName = event.value.homeTeam;
    if (betType === '2') selName = event.value.awayTeam;
    if (betType === 'X') selName = 'Empate';

    (betStore as any).addOrReplaceSelection(
        uniqueId,
        event.value.homeTeam,
        event.value.awayTeam,
        selName,
        price,
        betType as BetType,
        event.value.commenceTime,
        {
            isTournament: true,
            tournamentId: tournamentId.value,
            marketName: marketNameGrouped,
            isLive: isLive.value 
        }
    );
};

const formatOutcomeName = (val: string | number) => {
    let str = asString(val)
        .replace(/ or /gi, ' / ')
        .replace(/Draw/gi, 'Empate')
        .replace(/Home/gi, 'Casa')
        .replace(/Away/gi, 'Fora');
    if (str.toLowerCase() === 'yes') return 'Sim';
    if (str.toLowerCase() === 'no') return 'Não';
    return str;
};

onMounted(initPage);
onUnmounted(() => {
    if (connection) connection.stop();
});
</script>

<template>
    <div class="flex h-full bg-[#0f172a] text-slate-300 font-sans relative overflow-hidden">
        
        <PageLoader 
            :is-loading="isLoading" 
            :progress="loadingProgress" 
            :is-absolute="true" 
            loading-text="Carregando Detalhes..."
        />

        <div class="flex-1 flex flex-col h-full overflow-y-auto custom-scrollbar relative">
            
            <div class="max-w-[1200px] mx-auto px-2 md:px-4 space-y-4 w-full pb-24 pt-4">
                
                <div class="flex items-center gap-3 pb-2 border-b border-white/5">
                    <button @click="router.back()"
                        class="bg-white/5 hover:bg-white/10 p-2 rounded-full text-white transition-all active:scale-95 group">
                        <ArrowLeft class="w-4 h-4 group-hover:-translate-x-0.5 transition-transform" />
                    </button>
                    <div class="flex flex-col">
                        <div v-if="isLive" class="flex items-center gap-1.5 text-red-500 animate-pulse">
                            <span class="w-2 h-2 bg-red-500 rounded-full"></span>
                            <span class="text-[10px] font-black uppercase tracking-widest">AO VIVO</span>
                        </div>
                        <h2 class="text-white text-sm font-bold uppercase tracking-wide truncate max-w-[200px] md:max-w-none">
                            {{ event?.league || 'Detalhes do Jogo' }}
                        </h2>
                    </div>
                </div>

                <div v-if="event" class="space-y-6 animate-fade-in">

                    <div class="relative rounded-xl p-6 border border-white/10 shadow-2xl overflow-hidden group min-h-[180px] flex items-center bg-[#1a2c38]">
                        <div class="absolute inset-0 z-0">
                            <img src="/images/backgrouns-sport/backgrounds1.png" alt="Stadium" class="w-full h-full object-cover opacity-20 mix-blend-overlay" />
                        </div>
                        <div class="absolute inset-0 z-0 bg-gradient-to-t from-[#0f172a] via-[#0f172a]/80 to-transparent"></div>

                        <div class="relative z-10 flex items-center justify-between gap-4 w-full">
                            
                            <div class="flex-1 flex flex-col items-center gap-3 text-center">
                                <TeamLogo :teamName="event.homeTeam" :remoteUrl="event.homeTeamLogo" size="w-20 h-20 md:w-24 md:h-24" class="drop-shadow-2xl transition-transform group-hover:scale-105" />
                                <h1 class="text-white font-black text-lg md:text-xl leading-tight line-clamp-2 drop-shadow-md">{{ event.homeTeam }}</h1>
                            </div>

                            <div class="flex flex-col items-center justify-center min-w-[140px] px-2 z-20">
                                
                                <div v-if="isLive" class="flex flex-col items-center">
                                    <div class="bg-red-600/90 text-white px-3 py-1 rounded-t-lg font-black text-xs uppercase tracking-widest flex items-center gap-1 shadow-lg">
                                        <Timer class="w-3 h-3 animate-spin-slow" />
                                        <span>{{ event.currentMinute }}'</span>
                                    </div>
                                    <div class="bg-[#0f172a] bg-opacity-90 backdrop-blur-md border border-white/10 px-6 py-3 rounded-b-xl shadow-2xl flex items-center gap-4">
                                        <span class="text-4xl md:text-5xl font-black text-white">{{ event.homeScore }}</span>
                                        <span class="text-gray-500 text-2xl font-light">:</span>
                                        <span class="text-4xl md:text-5xl font-black text-white">{{ event.awayScore }}</span>
                                    </div>
                                    <span class="text-[10px] text-gray-400 font-bold uppercase tracking-wide mt-2">{{ event.period || 'Ao Vivo' }}</span>
                                </div>

                                <div v-else class="flex flex-col items-center justify-center">
                                    
                                    <div class="w-10 h-10 rounded-full bg-[#1e293b]/80 border border-blue-500/30 flex items-center justify-center mb-2 shadow-lg backdrop-blur-sm">
                                        <span class="text-blue-400 font-black text-xs tracking-widest">VS</span>
                                    </div>

                                    <div class="bg-[#0f172a] border border-white/10 px-4 py-1.5 rounded flex items-center gap-2 mb-1 shadow-inner">
                                        <Timer class="w-3.5 h-3.5 text-blue-400" />
                                        <span class="text-lg font-bold text-white font-mono leading-none">
                                            {{ formatGameTime(event.commenceTime) }}
                                        </span>
                                    </div>

                                    <span class="text-slate-500 text-[10px] font-bold tracking-wide">
                                        {{ formatGameDate(event.commenceTime) }}
                                    </span>

                                </div>

                            </div>

                            <div class="flex-1 flex flex-col items-center gap-3 text-center">
                                <TeamLogo :teamName="event.awayTeam" :remoteUrl="event.awayTeamLogo" size="w-20 h-20 md:w-24 md:h-24" class="drop-shadow-2xl transition-transform group-hover:scale-105" />
                                <h1 class="text-white font-black text-lg md:text-xl leading-tight line-clamp-2 drop-shadow-md">{{ event.awayTeam }}</h1>
                            </div>
                        </div>
                    </div>

                    <div class="space-y-3">
                        <div v-for="(odds, marketName) in groupedMarkets" :key="marketName" 
                            class="bg-[#1a2c38] rounded-lg border border-white/5 overflow-hidden transition-all duration-300 hover:border-white/10"
                            :class="expandedMarkets.has(marketName) ? 'shadow-lg ring-1 ring-white/5' : 'shadow-sm'">

                            <div @click="toggleMarket(marketName)"
                                class="flex items-center justify-between p-3.5 cursor-pointer bg-white/[0.02] hover:bg-white/[0.05] transition-colors select-none">
                                
                                <div class="flex items-center gap-2.5">
                                    <div class="bg-blue-500/10 p-1.5 rounded text-blue-400">
                                        <BarChart3 v-if="marketName.includes('Gols')" class="w-4 h-4" />
                                        <Trophy v-else class="w-4 h-4" />
                                    </div>
                                    <h3 class="text-white font-bold text-xs uppercase tracking-wide">{{ marketName }}</h3>
                                    
                                    <span v-if="getSelectedCountForMarket(marketName) > 0" 
                                          class="ml-1 bg-blue-500 text-white text-[10px] font-black px-1.5 py-0.5 rounded shadow-sm min-w-[20px] text-center animate-bounce-in">
                                        {{ getSelectedCountForMarket(marketName) }}
                                    </span>
                                </div>

                                <component :is="expandedMarkets.has(marketName) ? ChevronDown : ChevronRight" 
                                    class="w-4 h-4 text-slate-500 transition-transform duration-300" />
                            </div>

                            <div v-show="expandedMarkets.has(marketName)" class="p-3 border-t border-white/5 bg-[#0f212e]/50">
                                <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-2">
                                    <button v-for="odd in odds" :key="odd.id || odd.outcomeName" 
                                        @click="handleSelection(odd, marketName)"
                                        :disabled="!odd.price || odd.price <= 1.0"
                                        class="relative group flex flex-col justify-between p-2.5 rounded border transition-all duration-200 h-[54px]"
                                        :class="isSelected(odd, marketName) 
                                            ? 'bg-white text-black border-transparent shadow-[0_0_15px_rgba(255,255,255,0.2)] scale-[1.02]' 
                                            : (!odd.price || odd.price <= 1.0) 
                                                ? 'bg-[#1a2c38] opacity-50 cursor-not-allowed border-transparent'
                                                : 'bg-[#1a2c38] border-transparent hover:bg-white/5 hover:border-white/10 text-slate-300'">
                                        
                                        <div class="flex justify-between w-full mb-0.5">
                                            <span class="text-[10px] font-bold uppercase truncate w-[80%] text-left transition-colors"
                                                :class="isSelected(odd, marketName) ? 'text-black' : 'text-slate-400 group-hover:text-white'">
                                                {{ formatOutcomeName(odd.outcomeName || odd.OutcomeName) }}
                                            </span>
                                            <span v-if="odd.point" class="text-[10px] font-bold" :class="isSelected(odd, marketName) ? 'text-black' : 'text-yellow-500'">
                                                {{ odd.point > 0 ? '+' : '' }}{{ odd.point }}
                                            </span>
                                        </div>

                                        <div class="flex items-center justify-end w-full">
                                            <Lock v-if="!odd.price || odd.price <= 1.0" class="w-3 h-3 text-slate-500" />
                                            <span v-else class="text-sm font-black text-right w-full leading-none transition-transform group-hover:-translate-y-0.5"
                                                :class="isSelected(odd, marketName) ? 'text-black' : 'text-white'">
                                                {{ Number(odd.price).toFixed(2) }}
                                            </span>
                                        </div>
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

.animate-spin-slow { animation: spin 3s linear infinite; }
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }

.animate-bounce-in { animation: bounceIn 0.3s cubic-bezier(0.68, -0.55, 0.265, 1.55); }
@keyframes bounceIn { 0% { transform: scale(0); } 100% { transform: scale(1); } }

.animate-fade-in { animation: fadeIn 0.5s ease-out; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
</style>