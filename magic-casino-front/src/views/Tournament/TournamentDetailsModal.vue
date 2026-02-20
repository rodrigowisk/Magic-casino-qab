<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { 
    ChevronLeft, Ticket, Trophy, Info, Calendar, 
    Users, ShieldCheck, Gamepad2, 
    Medal, CheckCircle2 
} from 'lucide-vue-next';

// IMPORTA AS PÁGINAS EXISTENTES PARA RENDERIZAR AQUI DENTRO
import TournamentMyBets from '../../views/Tournament/TournamentMyBets.vue';
import TournamentRanking from '../../views/Tournament/TournamentRanking.vue';

// IMPORTA O HEADER (Ajuste o caminho se necessário, baseado na sua estrutura de pastas)
import Header from '../../components/Header.vue';

const props = defineProps<{
    show: boolean;
    tournament: any;
}>();

const emit = defineEmits(['update:show']);

// Abas disponíveis
const activeTab = ref<'prize' | 'bets' | 'ranking' | 'info'>('bets');

const close = () => {
    emit('update:show', false);
};

// Aba padrão ao abrir
watch(() => props.show, (isOpen) => {
    if (isOpen && props.tournament) {
        if (props.tournament.isFinished || String(props.tournament.status).toUpperCase() === 'FINISHED') {
            activeTab.value = 'prize'; 
        } else {
            activeTab.value = 'bets';
        }
    }
});

// --- Helpers de Exibição ---
const formattedDateStart = computed(() => {
    if (!props.tournament?.startDate) return '--/--';
    return new Date(props.tournament.startDate).toLocaleString('pt-BR');
});

const formattedDateEnd = computed(() => {
    if (!props.tournament?.endDate) return '--/--';
    return new Date(props.tournament.endDate).toLocaleString('pt-BR');
});

const formatCurrency = (val: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(val || 0);

const getRankColor = (rank: number) => {
    if (rank === 1) return 'text-yellow-400';
    if (rank === 2) return 'text-slate-300';
    if (rank === 3) return 'text-orange-400';
    return 'text-blue-400';
};

// Lógica de cálculo da Distribuição dos Prêmios
const prizeDistribution = computed(() => {
    const t = props.tournament;
    if (!t) return [];
    
    const pool = t.prizePool || 0;
    const rule = String(t.prizeRuleId || '').toUpperCase().trim();

    if (rule === 'PREMIO_1' || rule === 'TOP3' || rule === '1') {
        return [
            { rank: '1º LUGAR', percent: '50%', value: pool * 0.50 },
            { rank: '2º LUGAR', percent: '30%', value: pool * 0.30 },
            { rank: '3º LUGAR', percent: '20%', value: pool * 0.20 },
        ];
    }
    if (rule === 'PREMIO_2' || rule === 'TOP5' || rule === '2') {
        return [
            { rank: '1º LUGAR', percent: '45%', value: pool * 0.45 },
            { rank: '2º LUGAR', percent: '25%', value: pool * 0.25 },
            { rank: '3º LUGAR', percent: '15%', value: pool * 0.15 },
            { rank: '4º LUGAR', percent: '10%', value: pool * 0.10 },
            { rank: '5º LUGAR', percent: '5%', value: pool * 0.05 },
        ];
    }
    if (rule === 'WINNER_TAKES_ALL') {
        return [{ rank: '1º LUGAR', percent: '100%', value: pool }];
    }
    if (rule === 'PREMIO_3' || rule === 'DOUBLE_UP' || rule === '3') {
        const entry = t.entryFee || 0;
        const target = entry * 2;
        const winners = target > 0 ? Math.floor(pool / target) : 0;
        
        if (winners > 0 && entry > 0) {
            return [{ rank: `TOP ${winners} RECEBEM`, percent: '2x ENTRADA', value: target }];
        }
        return [{ rank: 'VENCEDORES', percent: 'DOBRA A ENTRADA', value: target }];
    }

    if (pool > 0) {
        return [{ rank: 'PREMIAÇÃO TOTAL', percent: '100%', value: pool }];
    }
    return [];
});
</script>

<template>
    <Teleport to="body">
        <transition name="slide-up">
            <div v-if="show" class="fixed inset-0 z-[999999] bg-[#0f172a] flex flex-col justify-start items-stretch m-0 p-0 h-[100dvh] w-screen font-sans overflow-hidden">
                
                <div class="flex-none w-full z-[60]">
                    <Header />
                </div>

                <div class="flex-none w-full bg-[#020617] border-b border-slate-800 shadow-[0_10px_30px_rgba(0,0,0,0.5)] z-50">
                    
                    <div class="flex items-center gap-4 p-4 border-b border-slate-800/80 max-w-4xl mx-auto">
                        <button @click="close" class="p-2.5 bg-slate-800 rounded-full hover:bg-slate-700 transition text-slate-400 hover:text-white shrink-0">
                            <ChevronLeft class="w-5 h-5 md:w-6 md:h-6" />
                        </button>
                        <div class="flex flex-col min-w-0 flex-1">
                            <h2 class="text-base md:text-xl font-black text-white tracking-wide uppercase leading-tight truncate">
                                {{ tournament?.name }}
                            </h2>
                            <span class="text-[10px] md:text-xs text-slate-500 font-mono tracking-widest uppercase">
                                ID: #{{ tournament?.id }}
                            </span>
                        </div>
                    </div>

                    <div class="px-4 py-3 bg-[#090e1a]">
                        <div class="bg-[#1e293b] p-1.5 rounded-xl flex items-center justify-between w-full max-w-md mx-auto border border-slate-700/50 shadow-inner gap-1">
                            
                            <button v-if="tournament?.isFinished" @click="activeTab = 'prize'" 
                                    class="flex-1 flex justify-center items-center py-2.5 rounded-lg transition-all duration-300"
                                    :class="activeTab === 'prize' ? 'bg-[#10b981] shadow-[0_0_15px_rgba(16,185,129,0.3)] text-white' : 'text-slate-500 hover:text-slate-300'">
                                <Medal class="w-5 h-5" />
                            </button>

                            <button @click="activeTab = 'bets'" 
                                    class="flex-1 flex justify-center items-center py-2.5 rounded-lg transition-all duration-300"
                                    :class="activeTab === 'bets' ? 'bg-[#3b82f6] shadow-[0_0_15px_rgba(59,130,246,0.3)] text-white' : 'text-slate-500 hover:text-slate-300'">
                                <Ticket class="w-5 h-5" />
                            </button>

                            <button @click="activeTab = 'ranking'" 
                                    class="flex-1 flex justify-center items-center py-2.5 rounded-lg transition-all duration-300"
                                    :class="activeTab === 'ranking' ? 'bg-[#f59e0b] shadow-[0_0_15px_rgba(245,158,11,0.3)] text-white' : 'text-slate-500 hover:text-slate-300'">
                                <Trophy class="w-5 h-5" />
                            </button>

                            <button @click="activeTab = 'info'" 
                                    class="flex-1 flex justify-center items-center py-2.5 rounded-lg transition-all duration-300"
                                    :class="activeTab === 'info' ? 'bg-[#8b5cf6] shadow-[0_0_15px_rgba(139,92,246,0.3)] text-white' : 'text-slate-500 hover:text-slate-300'">
                                <Info class="w-5 h-5" />
                            </button>
                        </div>
                    </div>
                </div>

                <div class="flex-1 w-full relative bg-[#0f172a] overflow-y-auto custom-scrollbar">
                    <div class="max-w-4xl mx-auto h-full w-full relative pb-20">

                        <div v-if="activeTab === 'prize'" class="h-full p-4 flex flex-col items-center justify-start pt-10">
                            <div class="relative w-full max-w-sm bg-[#020617] border border-emerald-500/30 rounded-xl p-8 shadow-2xl overflow-hidden text-center mt-4">
                                <div class="absolute inset-0 opacity-10 blur-2xl transition-colors duration-500" 
                                     :class="tournament?.rank === 1 ? 'bg-yellow-500' : 'bg-emerald-500'"></div>

                                <div class="relative z-10 mb-6">
                                    <div class="inline-flex items-center gap-1.5 bg-emerald-500/20 border border-emerald-500/30 px-3 py-1 rounded-full mb-6">
                                        <CheckCircle2 class="w-3.5 h-3.5 text-emerald-400" />
                                        <span class="text-[10px] font-bold text-emerald-400 uppercase tracking-wider">Torneio Finalizado</span>
                                    </div>
                                    <div class="flex flex-col items-center">
                                        <Medal class="w-12 h-12 mb-2" :class="getRankColor(tournament?.rank)" />
                                        <span class="text-[10px] text-slate-500 uppercase font-bold mb-1">Sua Posição</span>
                                        <span class="text-5xl font-black text-white tracking-tighter leading-none"
                                              :class="tournament?.rank === 1 ? 'text-yellow-400 drop-shadow-md' : ''">
                                            {{ (!tournament?.rank || tournament?.rank < 1) ? '--' : `#${tournament?.rank}` }}
                                        </span>
                                    </div>
                                </div>
                                <div class="relative z-10 border-t border-white/10 pt-6">
                                    <template v-if="Number(tournament?.moneyPrize) > 0">
                                        <p class="text-emerald-400 font-bold text-xs uppercase mb-3 tracking-widest">Você Ganhou</p>
                                        <div class="bg-emerald-500/10 border border-emerald-500/20 rounded-xl py-4 px-4 inline-block min-w-[220px]">
                                            <span class="text-3xl font-black text-white tracking-tight drop-shadow-md">
                                                {{ formatCurrency(Number(tournament?.moneyPrize)) }}
                                            </span>
                                        </div>
                                    </template>
                                    <template v-else>
                                        <div class="bg-slate-800/30 border border-slate-700/50 rounded-xl p-4">
                                            <p class="text-xs text-slate-400 font-medium leading-relaxed">
                                                Não houve premiação para sua posição neste torneio.<br>Mais sorte na próxima!
                                            </p>
                                        </div>
                                    </template>
                                </div>
                            </div>
                        </div>

                        <div v-if="activeTab === 'bets'" class="h-full">
                            <TournamentMyBets :tournamentIdProp="tournament?.id" :isEmbedded="true" />
                        </div>
                        <div v-if="activeTab === 'ranking'" class="h-full">
                            <TournamentRanking :tournamentIdProp="tournament?.id" :isEmbedded="true" />
                        </div>

                        <div v-if="activeTab === 'info'" class="h-full p-4 md:p-6">
                            
                            <div class="grid grid-cols-2 md:grid-cols-4 gap-3 md:gap-4 mb-4">
                                <div class="bg-[#1e293b] p-4 rounded-xl border border-slate-700/50 flex flex-col">
                                    <div class="flex items-center gap-2 mb-1">
                                        <Trophy class="w-4 h-4 text-yellow-500" />
                                        <span class="text-[10px] text-slate-400 uppercase font-bold tracking-widest">Premiação</span>
                                    </div>
                                    <span class="text-lg md:text-xl font-black text-white mt-1">{{ formatCurrency(tournament?.prizePool) }}</span>
                                </div>

                                <div class="bg-[#1e293b] p-4 rounded-xl border border-slate-700/50 flex flex-col">
                                    <div class="flex items-center gap-2 mb-1">
                                        <Ticket class="w-4 h-4 text-blue-400" />
                                        <span class="text-[10px] text-slate-400 uppercase font-bold tracking-widest">Entrada</span>
                                    </div>
                                    <span class="text-lg md:text-xl font-black text-white mt-1">{{ tournament?.entryFee == 0 ? 'GRÁTIS' : formatCurrency(tournament?.entryFee) }}</span>
                                </div>

                                <div class="bg-[#1e293b] p-4 rounded-xl border border-slate-700/50 flex flex-col">
                                    <div class="flex items-center gap-2 mb-1">
                                        <Users class="w-4 h-4 text-emerald-400" />
                                        <span class="text-[10px] text-slate-400 uppercase font-bold tracking-widest">Jogadores</span>
                                    </div>
                                    <span class="text-lg md:text-xl font-black text-white mt-1">
                                        {{ tournament?.participantsCount || 0 }} 
                                        <span v-if="tournament?.maxParticipants > 0" class="text-slate-500 text-sm font-normal">/ {{ tournament?.maxParticipants }}</span>
                                    </span>
                                </div>

                                <div class="bg-[#1e293b] p-4 rounded-xl border border-slate-700/50 flex flex-col">
                                    <div class="flex items-center gap-2 mb-1">
                                        <Gamepad2 class="w-4 h-4 text-purple-400" />
                                        <span class="text-[10px] text-slate-400 uppercase font-bold tracking-widest">Modalidade</span>
                                    </div>
                                    <span class="text-lg md:text-xl font-black text-white mt-1 capitalize">{{ tournament?.sport || 'Geral' }}</span>
                                </div>
                            </div>

                            <div class="grid grid-cols-1 md:grid-cols-3 gap-3 md:gap-4 mb-6">
                                <div class="bg-[#1e293b] p-4 rounded-xl border border-slate-700/50 flex flex-col justify-center">
                                    <div class="flex items-center gap-2 mb-2 text-slate-400">
                                        <Calendar class="w-4 h-4" />
                                        <span class="text-[10px] uppercase font-bold tracking-widest">Início</span>
                                    </div>
                                    <span class="text-sm font-bold text-white">{{ formattedDateStart }}</span>
                                </div>
                                <div class="bg-[#1e293b] p-4 rounded-xl border border-slate-700/50 flex flex-col justify-center">
                                    <div class="flex items-center gap-2 mb-2 text-slate-400">
                                        <Calendar class="w-4 h-4" />
                                        <span class="text-[10px] uppercase font-bold tracking-widest">Término</span>
                                    </div>
                                    <span class="text-sm font-bold text-white">{{ formattedDateEnd }}</span>
                                </div>
                                <div class="bg-[#1e293b] p-4 rounded-xl border border-slate-700/50 flex flex-col justify-center">
                                    <div class="flex items-center gap-2 mb-2 text-slate-400">
                                        <ShieldCheck class="w-4 h-4" />
                                        <span class="text-[10px] uppercase font-bold tracking-widest">Regras</span>
                                    </div>
                                    <span class="text-sm font-bold text-white">Padrão da Plataforma</span>
                                </div>
                            </div>

                            <div class="bg-[#1e293b] rounded-xl border border-slate-700/50 overflow-hidden">
                                <div class="p-4 bg-slate-800/50 border-b border-slate-700/50 flex items-center gap-2">
                                    <span class="text-xs font-bold text-slate-300 uppercase tracking-wider">% Distribuição da Premiação</span>
                                </div>
                                
                                <div class="p-4 md:p-6">
                                    <div v-if="prizeDistribution.length > 0" class="flex flex-wrap gap-4">
                                        <div v-for="(prize, idx) in prizeDistribution" :key="idx" 
                                             class="flex-1 min-w-[140px] bg-[#0f172a] p-4 rounded-lg border border-slate-700/50 flex items-center justify-between transition-transform hover:-translate-y-1">
                                            <div class="flex flex-col">
                                                <span class="text-[10px] text-slate-400 uppercase font-bold tracking-wider mb-1">{{ prize.rank }}</span>
                                                <span class="text-lg font-black" :class="idx === 0 ? 'text-yellow-400' : (idx === 1 ? 'text-slate-300' : (idx === 2 ? 'text-orange-400' : 'text-blue-400'))">
                                                    {{ prize.percent }}
                                                </span>
                                            </div>
                                            <div class="flex flex-col items-end">
                                                <span class="text-[10px] text-slate-500 uppercase font-bold tracking-wider mb-1">Valor</span>
                                                <span class="text-sm font-bold text-white">{{ formatCurrency(prize.value) }}</span>
                                            </div>
                                        </div>
                                    </div>
                                    <div v-else class="text-sm text-slate-400">
                                        A distribuição de prêmios será definida de acordo com as regras do torneio.
                                    </div>
                                    <p class="text-[9px] text-slate-500 mt-4 italic">* A premiação pode variar se baseada no número de participantes (Pool Dinâmico).</p>
                                </div>
                            </div>

                        </div>
                    </div>
                </div>
            </div>
        </transition>
    </Teleport>
</template>

<style scoped>
.slide-up-enter-active, .slide-up-leave-active {
    transition: transform 0.3s cubic-bezier(0.16, 1, 0.3, 1), opacity 0.3s ease;
}
.slide-up-enter-from, .slide-up-leave-to {
    transform: translateY(100%);
    opacity: 0;
}

.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 4px; }
</style>