<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ArrowLeft, Clock, ChevronDown, ChevronRight, Trophy, BarChart3, Medal, Lock } from 'lucide-vue-next';
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

        // 1. Validação
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

// ✅ COMPUTED CORRIGIDO (Tipagem Segura)
const groupedMarkets = computed<Record<string, any[]>>(() => {
    const evt = event.value;
    if (!evt) return {};

    // Garante array
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
    
    // ✅ CORREÇÃO: Verifica se 'g' existe antes de atribuir
    priority.forEach(p => { 
        const g = groups[p];
        if (g) sorted[p] = g; 
    });
    
    Object.keys(groups).sort().forEach(k => { 
        const g = groups[k];
        if (!priority.includes(k) && g) sorted[k] = g; 
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

    // Cast 'as any' para permitir 8 argumentos
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
            marketName: marketNameGrouped
        }
    );
};

// --- FORMATADORES ---
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
const formatDate = (d: string) => new Date(d).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
const formatTime = (d: string) => new Date(d).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });

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
                        <h2 class="text-white text-sm font-bold uppercase tracking-wide truncate max-w-[200px] md:max-w-none">
                            {{ event?.league || 'Detalhes do Jogo' }}
                        </h2>
                    </div>
                </div>

                <div v-if="event" class="space-y-6 animate-fade-in">

                    <div class="relative rounded-xl p-6 border border-yellow-500/10 shadow-2xl overflow-hidden group min-h-[160px] flex items-center bg-[#1a2c38]">
                        <div class="absolute inset-0 z-0">
                            <img src="/images/backgrouns-sport/backgrounds1.png" alt="Stadium" class="w-full h-full object-cover opacity-20 transition-transform duration-[20s] group-hover:scale-110" />
                        </div>
                        <div class="absolute inset-0 z-0 bg-gradient-to-t from-[#0f212e] via-[#0f212e]/90 to-transparent"></div>

                        <div class="relative z-10 flex items-center justify-between gap-4 w-full">
                            <div class="flex-1 flex flex-col items-center gap-3 text-center">
                                <TeamLogo :teamName="event.homeTeam" :remoteUrl="event.homeTeamLogo" size="w-16 h-16 md:w-20 md:h-20" class="drop-shadow-lg transition-transform group-hover:scale-105" />
                                <h1 class="text-white font-black text-sm md:text-lg leading-tight line-clamp-2 drop-shadow-md">{{ event.homeTeam }}</h1>
                            </div>

                            <div class="flex flex-col items-center justify-center min-w-[100px]">
                                <div class="bg-yellow-500/10 backdrop-blur-md px-4 py-1.5 rounded-full border border-yellow-500/20 mb-3 shadow-lg">
                                    <span class="text-yellow-500 font-black text-xs tracking-wider">VS</span>
                                </div>
                                <div class="flex flex-col items-center gap-1">
                                    <div class="flex items-center gap-1.5 text-white/90 font-bold text-xs bg-black/30 px-2 py-1 rounded shadow-sm border border-white/5">
                                        <Clock class="w-3 h-3 text-yellow-500" />
                                        <span>{{ formatTime(event.commenceTime) }}</span>
                                    </div>
                                    <span class="text-xs text-gray-400 font-bold uppercase tracking-wide">{{ formatDate(event.commenceTime) }}</span>
                                </div>
                            </div>

                            <div class="flex-1 flex flex-col items-center gap-3 text-center">
                                <TeamLogo :teamName="event.awayTeam" :remoteUrl="event.awayTeamLogo" size="w-16 h-16 md:w-20 md:h-20" class="drop-shadow-lg transition-transform group-hover:scale-105" />
                                <h1 class="text-white font-black text-sm md:text-lg leading-tight line-clamp-2 drop-shadow-md">{{ event.awayTeam }}</h1>
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
                                    <div class="bg-yellow-500/10 p-1.5 rounded text-yellow-500">
                                        <BarChart3 v-if="marketName.includes('Gols')" class="w-4 h-4" />
                                        <Trophy v-else class="w-4 h-4" />
                                    </div>
                                    <h3 class="text-white font-bold text-xs uppercase tracking-wide">{{ marketName }}</h3>
                                    
                                    <span v-if="getSelectedCountForMarket(marketName) > 0" 
                                          class="ml-1 bg-yellow-500 text-black text-[10px] font-black px-1.5 py-0.5 rounded shadow-sm min-w-[20px] text-center animate-bounce-in">
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

.animate-bounce-in { animation: bounceIn 0.3s cubic-bezier(0.68, -0.55, 0.265, 1.55); }
@keyframes bounceIn { 0% { transform: scale(0); } 100% { transform: scale(1); } }

.animate-fade-in { animation: fadeIn 0.5s ease-out; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
</style>