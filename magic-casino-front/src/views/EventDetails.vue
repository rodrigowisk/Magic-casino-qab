<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ArrowLeft, Clock, ChevronDown, ChevronRight, Trophy, BarChart3 } from 'lucide-vue-next';
import SportsService from '../services/SportsService';
import TeamLogo from '../components/TeamLogo.vue';
import { useBetStore, type BetType } from '../stores/useBetStore';

// --- HELPERS E INTERFACES ---
const asString = (v: string | number | null | undefined | string[]): string => {
    if (Array.isArray(v)) return v[0] || '';
    return String(v ?? '').trim();
};

interface SportEvent {
    externalId?: string | number;
    id?: string | number;
    homeTeam: string;
    awayTeam: string;
    commenceTime: string;
    league?: string;
    odds: Array<{
        id?: string | number;
        marketName: string;
        outcomeName: string | number;
        price: number | string;
        point?: number;
    }>;
}

const route = useRoute();
const router = useRouter();
const betStore = useBetStore();

const event = ref<SportEvent | null>(null);
const loading = ref(true);
const expandedMarkets = ref<Set<string>>(new Set());

// --- LÓGICA DE MERCADOS ---
const toggleMarket = (marketName: string) => {
    if (expandedMarkets.value.has(marketName)) {
        expandedMarkets.value.delete(marketName);
    } else {
        expandedMarkets.value.add(marketName);
    }
};

const translateMarket = (key: string) => {
    const k = asString(key).toLowerCase().trim();
    if (['1x2', 'full time result', 'match winner', 'money line', 'resultado final'].includes(k)) return 'Resultado Final';
    if (k.includes('double chance')) return 'Dupla Hipótese';
    if (k.includes('goals over under') || k.includes('total goals')) return 'Gols Mais/Menos';
    if (k.includes('handicap')) return 'Handicap';
    if (k.includes('both teams to score') || k === 'btts') return 'Ambos Marcam';
    if (k.includes('correct score')) return 'Placar Correto';
    if (k.includes('half time')) return 'Intervalo';
    return key;
};

const isMarket1x2 = (marketName: string) => {
    const k = translateMarket(marketName).toLowerCase();
    if (k === 'resultado final') return true;
    return k.includes('winner') || k.includes('result') || k.includes('1x2');
};

const fetchDetails = async () => {
    try {
        const eventId = asString(route.params.id);
        if (!eventId) return;
        const data = await SportsService.getEventDetails(eventId);
        if (data) event.value = data as SportEvent;
    } catch (err) {
        console.error('Erro ao carregar detalhes', err);
    } finally {
        loading.value = false;
    }
};

// --- COMPUTED: AGRUPAMENTO INTELIGENTE ---
const groupedMarkets = computed<Record<string, any[]>>(() => {
    const evt = event.value; 
    if (!evt || !evt.odds) return {};

    const groups: Record<string, any[]> = {};

    evt.odds.forEach((odd) => {
        const name = translateMarket(odd.marketName);
        if (!groups[name]) groups[name] = [];
        groups[name].push(odd);
    });

    const priority = ['Resultado Final', 'Gols Mais/Menos', 'Ambos Marcam', 'Dupla Hipótese'];
    const sortedGroups: Record<string, any[]> = {};
    
    priority.forEach(p => {
        if (groups[p]) sortedGroups[p] = groups[p];
    });
    
    Object.keys(groups).sort().forEach(k => {
        if (!priority.includes(k)) sortedGroups[k] = groups[k];
    });

    return sortedGroups;
});

watch(groupedMarkets, (newVal) => {
    const important = ['Resultado Final', 'Gols Mais/Menos', 'Ambos Marcam'];
    Object.keys(newVal).forEach(k => {
        if (important.includes(k) || expandedMarkets.value.size < 3) expandedMarkets.value.add(k);
    });
});

// --- HELPERS DE SELEÇÃO E FORMATAÇÃO ---

const getBetTypeForSlip = (odd: any, marketName: string): string => {
    if (isMarket1x2(marketName) && event.value) {
        const outcome = asString(odd.outcomeName).toLowerCase();
        const home = asString(event.value.homeTeam).toLowerCase();
        const away = asString(event.value.awayTeam).toLowerCase();
        
        if (outcome === '1' || outcome === home) return '1';
        if (outcome === '2' || outcome === away) return '2';
        if (['x', 'draw', 'empate'].includes(outcome)) return 'X';
        
        if (outcome.includes(home)) return '1';
        if (outcome.includes(away)) return '2';
    }
    return translateMarket(marketName); 
};

// Gera um ID que representa a aposta única
const getSelectionId = (gameId: string, odd: any): string => {
    const type = getBetTypeForSlip(odd, odd.marketName);
    
    // Se for 1x2, usamos o GameID para garantir unicidade no store
    if (['1', '2', 'X'].includes(type)) return gameId;
    
    // Para outros mercados, usamos um ID composto
    return `${gameId}_${asString(odd.marketName)}_${asString(odd.outcomeName)}`;
};

const isSelected = (odd: any): boolean => {
    if (!event.value) return false;
    const gameId = asString(route.params.id);
    const uniqueId = getSelectionId(gameId, odd);
    return betStore.selections.some(s => s.id === uniqueId);
};

// 🔥 CORREÇÃO PRINCIPAL AQUI 🔥
const handleSelection = (odd: any) => {
    if (!event.value) return;
    
    const gameId = asString(route.params.id);
    const marketNameTranslated = translateMarket(odd.marketName);
    const betTypeString = getBetTypeForSlip(odd, odd.marketName);
    const uniqueId = getSelectionId(gameId, odd);

    // 1. Se já está selecionado (clicou no mesmo), remove e sai
    if (isSelected(odd)) {
        betStore.removeSelection(uniqueId);
        return;
    }

    // 2. Limpeza Inteligente: Remove outras seleções do MESMO mercado para este jogo
    // Ex: Se eu selecionei "Handicap +1.5" e clico em "Handicap -1.5", remove o anterior.
    // Ex: Se eu selecionei "Over 2.5" e clico em "Under 2.5", remove o anterior.
    
    const existingSelectionSameMarket = betStore.selections.find(s => {
        // Verifica se é do mesmo jogo
        const isSameGame = s.id === gameId || String(s.id).startsWith(gameId + '_');
        if (!isSameGame) return false;

        // Se for mercado 1x2, o tipo será '1', 'X' ou '2'.
        if (['1', '2', 'X'].includes(betTypeString)) {
             return ['1', '2', 'X'].includes(s.type);
        }

        // Para outros mercados, verificamos se o nome do mercado bate
        // O `s.marketName` no store guarda o nome traduzido ou tipo
        return s.marketName === marketNameTranslated;
    });

    if (existingSelectionSameMarket) {
        betStore.removeSelection(existingSelectionSameMarket.id);
    }

    // 3. Define nome da seleção
    let selectionName = asString(odd.outcomeName);
    if (betTypeString === '1') selectionName = event.value.homeTeam;
    else if (betTypeString === '2') selectionName = event.value.awayTeam;
    else if (betTypeString === 'X') selectionName = 'Empate';

    // 4. Adiciona a nova seleção
    betStore.addOrReplaceSelection(
        uniqueId,
        event.value.homeTeam,
        event.value.awayTeam,
        selectionName,
        Number(odd.price),
        betTypeString as BetType,
        event.value.commenceTime,
        marketNameTranslated // Passamos o nome do mercado para controle futuro
    );
};

const formatDate = (d: string) => {
    if (!d) return '--/--';
    const date = new Date(d);
    return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
};

const formatTime = (d: string) => d ? new Date(d).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) : '--:--';

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

onMounted(fetchDetails);
</script>

<template>
    <div class="space-y-6 pb-24 pt-4 px-3 md:px-0 max-w-5xl mx-auto">
        
        <div class="flex items-center gap-3 pb-3 border-b border-white/5">
            <button @click="router.back()"
                class="bg-white/5 hover:bg-white/10 p-2 rounded-full text-white transition-all active:scale-95 group">
                <ArrowLeft class="w-4 h-4 group-hover:-translate-x-0.5 transition-transform" />
            </button>
            <div class="flex flex-col">
                <span class="text-[10px] text-stake-text font-bold uppercase tracking-widest">Evento</span>
                <h2 class="text-white text-sm font-bold uppercase tracking-wide truncate max-w-[200px] md:max-w-none">
                    {{ event?.league || 'Carregando...' }}
                </h2>
            </div>
        </div>

        <div v-if="loading" class="space-y-4 animate-pulse">
            <div class="h-32 bg-white/5 rounded-xl"></div>
            <div class="h-12 bg-white/5 rounded-lg w-1/3"></div>
            <div class="grid grid-cols-3 gap-3"><div class="h-10 bg-white/5 rounded"></div><div class="h-10 bg-white/5 rounded"></div><div class="h-10 bg-white/5 rounded"></div></div>
        </div>

        <div v-else-if="event" class="space-y-6">

            <div class="relative rounded-xl p-6 border border-white/5 shadow-2xl overflow-hidden group min-h-[160px] flex items-center">
                
                <div class="absolute inset-0 z-0">
                    <img src="/images/backgrouns-sport/backgrounds1.png" 
                         alt="Stadium Background" 
                         class="w-full h-full object-cover transition-transform duration-[10s] group-hover:scale-110" />
                </div>

                <div class="absolute inset-0 z-0 bg-gradient-to-t from-[#0f212e] via-[#0f212e]/80 to-[#1a2c38]/70 backdrop-blur-[1px]"></div>

                <div class="relative z-10 flex items-center justify-between gap-4 w-full">
                    
                    <div class="flex-1 flex flex-col items-center gap-3 text-center">
                        <div class="relative">
                            <TeamLogo :teamName="event.homeTeam" size="w-16 h-16 md:w-20 md:h-20" class="drop-shadow-[0_4px_6px_rgba(0,0,0,0.5)] transition-transform group-hover:scale-105 duration-500" />
                        </div>
                        <h1 class="text-white font-black text-sm md:text-lg leading-tight line-clamp-2 drop-shadow-md">
                            {{ event.homeTeam }}
                        </h1>
                    </div>

                    <div class="flex flex-col items-center justify-center min-w-[100px]">
                        <div class="bg-black/40 backdrop-blur-md px-4 py-1.5 rounded-full border border-white/10 mb-3 shadow-lg">
                            <span class="text-stake-blue font-black text-xs tracking-wider">VS</span>
                        </div>
                        <div class="flex flex-col items-center gap-1">
                            <div class="flex items-center gap-1.5 text-white/90 font-bold text-xs bg-black/30 px-2 py-1 rounded shadow-sm border border-white/5">
                                <Clock class="w-3 h-3 text-stake-blue" />
                                <span>{{ formatTime(event.commenceTime) }}</span>
                            </div>
                            <span class="text-xs text-gray-300 font-bold uppercase tracking-wide drop-shadow-sm">
                                {{ formatDate(event.commenceTime) }}
                            </span>
                        </div>
                    </div>

                    <div class="flex-1 flex flex-col items-center gap-3 text-center">
                        <div class="relative">
                            <TeamLogo :teamName="event.awayTeam" size="w-16 h-16 md:w-20 md:h-20" class="drop-shadow-[0_4px_6px_rgba(0,0,0,0.5)] transition-transform group-hover:scale-105 duration-500" />
                        </div>
                        <h1 class="text-white font-black text-sm md:text-lg leading-tight line-clamp-2 drop-shadow-md">
                            {{ event.awayTeam }}
                        </h1>
                    </div>
                </div>
            </div>

            <div class="space-y-3">
                <div v-for="(odds, marketName) in groupedMarkets" :key="marketName" 
                    class="bg-[#1a2c38] rounded-lg border border-white/5 overflow-hidden transition-all duration-300 hover:border-white/10"
                    :class="expandedMarkets.has(marketName) ? 'shadow-lg' : 'shadow-sm'">

                    <div @click="toggleMarket(marketName)"
                        class="flex items-center justify-between p-3.5 cursor-pointer bg-white/[0.02] hover:bg-white/[0.05] transition-colors select-none">
                        <div class="flex items-center gap-2.5">
                            <div class="bg-stake-blue/10 p-1.5 rounded text-stake-blue">
                                <BarChart3 v-if="marketName.includes('Gols')" class="w-4 h-4" />
                                <Trophy v-else class="w-4 h-4" />
                            </div>
                            <h3 class="text-white font-bold text-xs uppercase tracking-wide">{{ marketName }}</h3>
                        </div>
                        <div class="flex items-center gap-3">
                            <span v-if="!expandedMarkets.has(marketName)" class="text-[10px] text-stake-text font-medium bg-black/20 px-2 py-0.5 rounded-full">
                                {{ odds.length }} opções
                            </span>
                            <component :is="expandedMarkets.has(marketName) ? ChevronDown : ChevronRight" 
                                class="w-4 h-4 text-stake-text transition-transform duration-300" />
                        </div>
                    </div>

                    <div v-show="expandedMarkets.has(marketName)" class="p-3 border-t border-white/5 bg-[#0f212e]/50">
                        <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-2">
                            <button v-for="odd in odds" :key="odd.id || odd.outcomeName" 
                                @click="handleSelection(odd)"
                                class="relative group flex flex-col justify-between p-2.5 rounded border transition-all duration-200 h-[52px]"
                                :class="isSelected(odd) 
                                    ? 'bg-stake-blue/10 border-stake-blue shadow-[0_0_10px_rgba(0,146,255,0.2)]' 
                                    : 'bg-[#1a2c38] border-transparent hover:bg-white/5 hover:border-white/10'">
                                
                                <span class="text-[10px] font-bold uppercase truncate w-full text-left transition-colors mb-0.5"
                                    :class="isSelected(odd) ? 'text-stake-blue' : 'text-stake-text group-hover:text-white'">
                                    {{ formatOutcomeName(odd.outcomeName) }}
                                    <span v-if="odd.point" class="text-white ml-1">{{ odd.point > 0 ? '+' : '' }}{{ odd.point }}</span>
                                </span>

                                <span class="text-sm font-black text-right w-full leading-none transition-transform group-hover:-translate-y-0.5"
                                    :class="isSelected(odd) ? 'text-white' : 'text-white'">
                                    {{ Number(odd.price).toFixed(2) }}
                                </span>

                                <div v-if="isSelected(odd)" class="absolute top-0 right-0 w-2 h-2">
                                    <span class="absolute inline-flex h-full w-full rounded-bl-md bg-stake-blue opacity-100"></span>
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
/* Animação suave para o acordeão */
.v-enter-active,
.v-leave-active {
  transition: opacity 0.2s ease;
}
.v-enter-from,
.v-leave-to {
  opacity: 0;
}
</style>