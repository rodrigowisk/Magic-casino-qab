<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ArrowLeft, Clock, ChevronDown, ChevronRight, Trophy, BarChart3, Lock, Medal } from 'lucide-vue-next';
import Swal from 'sweetalert2';

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

// --- SAFE GETTERS ---
const getHomeTeam = () => (event.value && event.value.homeTeam) ? String(event.value.homeTeam) : 'Casa';
const getAwayTeam = () => (event.value && event.value.awayTeam) ? String(event.value.awayTeam) : 'Fora';
const getEventId = () => (event.value && event.value.id) ? String(event.value.id) : '';

// --- CARREGAMENTO ---
const loadCurrentUser = () => {
    try {
        const stored = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
        if (stored) {
            const u = JSON.parse(stored);
            const rawId = u?.cpf || u?.Cpf || u?.code || u?.Code || u?.id || '';
            currentUser.value = String(rawId).replace(/\D/g, '');
        }
    } catch (e) { console.error(e); }
};

const initPage = async () => {
    startLoader();
    try {
        loadCurrentUser();

        // 1. Validação do Torneio
        const tourRes = await TournamentService.getTournament(tournamentId.value, currentUser.value);
        if (!tourRes?.data || tourRes.data.isJoined === false) {
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

        // 2. Buscar Dados do Jogo
        const eventRes = await SportsService.getEventDetails(gameId.value);
        
        if (eventRes) {
            const rawOdds = eventRes.odds || eventRes.Odds;
            const safeOdds = Array.isArray(rawOdds) ? rawOdds : [];

            event.value = {
                ...eventRes,
                id: String(eventRes.id || eventRes.externalId || eventRes.gameId),
                homeTeam: eventRes.homeTeam || eventRes.HomeTeam,
                awayTeam: eventRes.awayTeam || eventRes.AwayTeam,
                league: eventRes.league || eventRes.League,
                commenceTime: eventRes.commenceTime || eventRes.CommenceTime,
                odds: safeOdds
            };
        } else {
            throw new Error("Jogo não encontrado na API.");
        }

    } catch (err) {
        console.error("Erro ao carregar:", err);
        Swal.fire({ toast: true, icon: 'error', title: 'Jogo indisponível no momento.', background: '#1e293b', color: '#fff' });
        router.back();
    } finally {
        finishLoader();
    }
};

// --- LÓGICA DE MERCADOS ---

const marketTranslations: Record<string, string> = {
    'CORNERS': 'Escanteios',
    'ALTERNATIVE_CORNERS': 'Escanteios Alternativos',
    'CORNERS_2_WAY': 'Escanteios (2 Opções)',
    'CARDS': 'Cartões',
    'TOTAL DE CARTÕES': 'Total de Cartões',
    'ASIAN_HANDICAP': 'Handicap Asiático',
    'GOAL_LINE': 'Total de Gols (Linha)',
    'ALTERNATIVE_GOAL_LINE': 'Gols Alternativos',
    '1ST_HALF_GOAL_LINE': 'Gols - 1º Tempo',
    '1ST_HALF_ASIAN_HANDICAP': 'Handicap Asiático - 1º Tempo',
};

const groupedMarkets = computed<Record<string, any[]>>(() => {
    // Cast para any para flexibilidade total
    const evt = event.value as any; 
    if (!evt) return {};

    const raw = evt?.odds || evt?.Odds; 
    const oddsList: any[] = Array.isArray(raw) ? raw : [];

    // "NUCLEAR OPTION": Tipagem 'any' para o acumulador de grupos
    // Isso evita o erro TS2532 (Object possibly undefined) ao acessar groups[name]
    const groups: any = {};

    oddsList.forEach((odd: any) => {
        if (!odd) return;
        
        let name = odd?.marketName || odd?.MarketName || 'Outros';
        
        if (marketTranslations[name.toUpperCase()]) {
            name = marketTranslations[name.toUpperCase()];
        } else {
            name = name.replace(/_/g, ' ');
        }

        if (!groups[name]) {
            groups[name] = [];
        }
        
        // Acesso seguro garantido pelo 'any' e pela inicialização acima
        groups[name].push(odd);
    });

    // Ordenação
    Object.keys(groups).forEach(key => {
        const list = groups[key];
        
        // Verificação dupla de segurança
        if (!list || !Array.isArray(list)) return;

        list.sort((a: any, b: any) => {
            const hA = a?.handicap ?? a?.Handicap ?? 0;
            const hB = b?.handicap ?? b?.Handicap ?? 0;
            const handA = parseFloat(String(hA));
            const handB = parseFloat(String(hB));
            
            if (handA !== handB) return handA - handB; 
            
            const outA = String(a?.outcomeName || a?.OutcomeName || '').toLowerCase();
            const outB = String(b?.outcomeName || b?.OutcomeName || '').toLowerCase();
            return outA.localeCompare(outB);
        });
    });

    const priority = [
        'Resultado Final', 
        'Vencedor da Partida',
        'Ambos Marcam', 
        'Total de Gols', 
        'Escanteios',
        'Total de Cartões',
        'Dupla Hipótese', 
        'Handicap Asiático'
    ];
    
    // O resultado final ainda retorna o tipo correto para o template
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
    const important = ['Resultado Final', 'Vencedor da Partida', 'Total de Gols', 'Escanteios', 'Ambos Marcam'];
    Object.keys(newVal).forEach(k => {
        if (important.includes(k) || expandedMarkets.value.size < 2) expandedMarkets.value.add(k);
    });
}, { immediate: true });

const toggleMarket = (name: string) => {
    if (expandedMarkets.value.has(name)) expandedMarkets.value.delete(name);
    else expandedMarkets.value.add(name);
};

const getGridClass = (odds: any[]) => {
    const hasOverUnder = odds.some(o => String(o?.outcomeName || o?.OutcomeName).toLowerCase().includes('over'));
    if (hasOverUnder) return 'grid-cols-2';
    
    if (odds.length === 2) return 'grid-cols-2';
    if (odds.length === 3) return 'grid-cols-3';
    return 'grid-cols-2 md:grid-cols-4'; 
};

// --- AÇÕES DE APOSTA ---

const getSelectionUniqueId = (gId: string, market: string, outcome: string): string => {
    return `${gId}_${market}_${asString(outcome)}`.replace(/\s+/g, '');
};

const isSelected = (odd: any, marketNameGrouped: string): boolean => {
    const evId = getEventId();
    if (!evId || !odd) return false;
    const uniqueId = getSelectionUniqueId(evId, marketNameGrouped, odd?.outcomeName || odd?.OutcomeName);
    return betStore.selections.some(s => s?.id === uniqueId);
};

const getSelectedCountForMarket = (marketNameGrouped: string) => {
    const evId = getEventId();
    if (!evId) return 0;
    return betStore.selections.filter(s => {
        return s?.id && s.id.startsWith(`${evId}_`) && s.id.includes(marketNameGrouped.replace(/\s+/g, ''));
    }).length;
};

const getBetType = (odd: any, marketGrouped: string): string => {
    if (!odd) return 'O';
    if (marketGrouped === 'Resultado Final' || marketGrouped === 'Vencedor da Partida') {
        const out = asString(odd?.outcomeName || odd?.OutcomeName).toLowerCase();
        if (out === '1') return '1';
        if (out === '2') return '2';
        if (['x', 'draw', 'empate'].includes(out)) return 'X';
    }
    return 'O'; 
};

const handleSelection = (odd: any, marketNameGrouped: string) => {
    if (!event.value || !odd) return; 
    
    const price = Number(odd?.price || odd?.Price);
    if (!price || price <= 1.0) return;

    const evId = getEventId();
    const outcomeStr = String(odd?.outcomeName || odd?.OutcomeName);
    const uniqueId = getSelectionUniqueId(evId, marketNameGrouped, outcomeStr);
    
    const alreadySelected = betStore.selections.find(s => s?.id === uniqueId);

    if (alreadySelected) {
        betStore.removeSelection(uniqueId);
        return;
    }

    const existingInMarket = betStore.selections.find(s => {
        return s?.id && s.id.startsWith(`${evId}_`) && s.id.includes(marketNameGrouped.replace(/\s+/g, ''));
    });
    
    if (existingInMarket && existingInMarket.id) {
        betStore.removeSelection(existingInMarket.id);
    }

    const betType = getBetType(odd, marketNameGrouped);
    const selName = formatOutcomeName(odd, marketNameGrouped);

    (betStore as any).addOrReplaceSelection(
        uniqueId,
        getHomeTeam(),
        getAwayTeam(),
        selName,
        price,
        betType as BetType,
        event.value.commenceTime || '',
        {
            isTournament: true,
            tournamentId: tournamentId.value,
            marketName: marketNameGrouped,
            gameId: getEventId() // ✅ O ID real do jogo agora vai para a Store
        }
    );
};

// --- FORMATADORES ---

const formatOutcomeName = (odd: any, marketName: string) => {
    if (!odd) return '';
    let str = asString(odd?.outcomeName || odd?.OutcomeName).trim();
    const hTeam = getHomeTeam();
    const aTeam = getAwayTeam();
    
    if (marketName === 'Resultado Final' || marketName === 'Vencedor da Partida') {
        if (str === '1' || str.toLowerCase() === 'home') return hTeam;
        if (str === '2' || str.toLowerCase() === 'away') return aTeam;
        if (str.toLowerCase() === 'x' || str.toLowerCase() === 'draw') return 'Empate';
    }

    if (str === '1X') return `${hTeam} ou Empate`;
    if (str === 'X2' || str === '2X') return `${aTeam} ou Empate`;
    if (str === '12') return `${hTeam} ou ${aTeam}`;

    str = str
        .replace(/Home/gi, hTeam)
        .replace(/Away/gi, aTeam)
        .replace(/Draw/gi, 'Empate')
        .replace(/Over/gi, 'Mais de')
        .replace(/Under/gi, 'Menos de')
        .replace(/Exactly/gi, 'Exatamente')
        .replace(/Yes/gi, 'Sim')
        .replace(/No/gi, 'Não');

    const handicap = odd?.handicap ?? odd?.Handicap;
    if (handicap !== undefined && handicap !== null && handicap !== '') {
        if (!str.includes(String(handicap))) {
            if (marketName.toLowerCase().includes('handicap')) {
                const sign = parseFloat(handicap) > 0 ? '+' : '';
                str += ` ${sign}${handicap}`;
            } else {
                str += ` ${handicap}`;
            }
        }
    }

    return str;
};

const formatDate = (d: string) => {
    if (!d) return '';
    return new Date(d).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
};

const formatTime = (d: string) => {
    if (!d) return '';
    return new Date(d).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
};

onMounted(initPage);
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
                        <div class="flex items-center gap-1.5 text-yellow-500">
                            <Medal class="w-3 h-3" />
                            <span class="text-[10px] font-bold uppercase tracking-widest">Torneio #{{ tournamentId }}</span>
                        </div>
                        <h2 class="text-white text-sm font-bold uppercase tracking-wide truncate max-w-[300px] md:max-w-none">
                            {{ event?.league || 'Detalhes do Jogo' }}
                        </h2>
                    </div>
                </div>

                <div v-if="event" class="space-y-6 animate-fade-in">

                    <div class="relative rounded-xl p-6 border border-blue-500/20 shadow-2xl overflow-hidden group min-h-[160px] flex items-center bg-[#1a2c38]">
                        <div class="absolute inset-0 z-0">
                            <img src="/images/backgrouns-sport/backgrounds1.png" alt="Stadium" class="w-full h-full object-cover opacity-20 transition-transform duration-[20s] group-hover:scale-110" />
                        </div>
                        <div class="absolute inset-0 z-0 bg-gradient-to-t from-[#0f212e] via-[#0f212e]/90 to-transparent"></div>

                        <div class="relative z-10 flex items-center justify-between gap-4 w-full">
                            <div class="flex-1 flex flex-col items-center gap-3 text-center">
                                <TeamLogo :teamName="getHomeTeam()" :remoteUrl="event?.homeTeamLogo" size="w-16 h-16 md:w-20 md:h-20" class="drop-shadow-lg transition-transform group-hover:scale-105" />
                                <h1 class="text-white font-black text-sm md:text-lg leading-tight line-clamp-2 drop-shadow-md">{{ getHomeTeam() }}</h1>
                            </div>

                            <div class="flex flex-col items-center justify-center min-w-[100px]">
                                <div class="bg-blue-500/10 backdrop-blur-md px-4 py-1.5 rounded-full border border-blue-500/20 mb-3 shadow-lg">
                                    <span class="text-blue-400 font-black text-xs tracking-wider">VS</span>
                                </div>
                                <div class="flex flex-col items-center gap-1">
                                    <div class="flex items-center gap-1.5 text-white/90 font-bold text-xs bg-black/30 px-3 py-1.5 rounded shadow-sm border border-white/5">
                                        <Clock class="w-3 h-3 text-blue-400" />
                                        <span>{{ formatTime(event?.commenceTime) }}</span>
                                    </div>
                                    <span class="text-[10px] text-gray-400 font-bold uppercase tracking-wide">{{ formatDate(event?.commenceTime) }}</span>
                                </div>
                            </div>

                            <div class="flex-1 flex flex-col items-center gap-3 text-center">
                                <TeamLogo :teamName="getAwayTeam()" :remoteUrl="event?.awayTeamLogo" size="w-16 h-16 md:w-20 md:h-20" class="drop-shadow-lg transition-transform group-hover:scale-105" />
                                <h1 class="text-white font-black text-sm md:text-lg leading-tight line-clamp-2 drop-shadow-md">{{ getAwayTeam() }}</h1>
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
                                        <BarChart3 v-if="marketName.includes('Gols') || marketName.includes('Escanteios') || marketName.includes('Cartões')" class="w-4 h-4" />
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
                                <div class="grid gap-2" :class="getGridClass(odds)">
                                    
                                    <button v-for="odd in odds" :key="odd?.id || odd?.outcomeName" 
                                        @click="handleSelection(odd, marketName)"
                                        :disabled="!odd?.price || odd?.price <= 1.0"
                                        class="relative group flex flex-col justify-between p-2.5 rounded border transition-all duration-200 min-h-[54px]"
                                        :class="isSelected(odd, marketName) 
                                            ? 'bg-blue-600 border-blue-500 shadow-md scale-[1.02]' 
                                            : (!odd?.price || odd?.price <= 1.0) 
                                                ? 'bg-[#1a2c38] opacity-50 cursor-not-allowed border-transparent'
                                                : 'bg-[#1a2c38] border-white/5 hover:bg-white/5 hover:border-white/10 text-slate-300'">
                                    
                                        <div class="flex justify-between items-center w-full mb-1">
                                            <span class="text-[11px] font-medium leading-tight text-left transition-colors"
                                                :class="isSelected(odd, marketName) ? 'text-white' : 'text-slate-400 group-hover:text-white'">
                                                {{ formatOutcomeName(odd, marketName) }}
                                            </span>
                                        </div>

                                        <div class="flex items-center justify-between w-full mt-auto">
                                            <Lock v-if="!odd?.price || odd?.price <= 1.0" class="w-3 h-3 text-slate-500" />
                                            <span v-else class="text-sm font-black text-right w-full leading-none transition-transform group-hover:-translate-y-0.5"
                                                :class="isSelected(odd, marketName) ? 'text-white' : 'text-blue-400'">
                                                {{ Number(odd?.price).toFixed(2) }}
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

.animate-bounce-in { animation: bounceIn 0.3s cubic-bezier(0.68, -0.55, 0.265, 1.55); }
@keyframes bounceIn { 0% { transform: scale(0); } 100% { transform: scale(1); } }

.animate-fade-in { animation: fadeIn 0.5s ease-out; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
</style>