<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import { 
    Trophy, 
    Clock, 
    Medal,
    Search,
    Filter,
    ChevronRight,
    Play, // 👈 Ícone de Play importado
    LayoutList,
    Zap,
    CheckCircle2,
    ArrowLeft,
    Coins
} from 'lucide-vue-next';
import tournamentService from '../../services/Tournament/TournamentService';
import PageLoader from '../../components/PageLoader.vue';
import { usePageLoader } from '../../composables/usePageLoader';
import { useAuthStore } from '../../stores/useAuthStore'; 

// Importa o Modal que criamos
import TournamentDetailsModal from '../../views/Tournament/TournamentDetailsModal.vue';

const router = useRouter();
const authStore = useAuthStore();
const { isLoading, loadingProgress, startLoader, finishLoader } = usePageLoader();

const tournaments = ref<any[]>([]);
const filterStatus = ref<'ALL' | 'ACTIVE' | 'FINISHED'>('ALL');
const searchQuery = ref('');

// --- ESTADOS DO MODAL ---
const showDetailsModal = ref(false);
const selectedTournament = ref<any>(null);

const getUserIdSafe = () => {
    let u = authStore.user;
    if (!u) {
        const stored = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
        if (stored) {
            try { u = JSON.parse(stored); } catch (e) {}
        }
    }
    if (!u) return '';
    const rawId = u.cpf || u.Cpf || u.id || u.Id || u.userId || u.UserId || u.code || u.Code || '';
    return String(rawId).replace(/\D/g, ''); 
};

const isItemFinished = (t: any) => {
    if (t.isFinished === true) return true;
    if (String(t.isFinished).toLowerCase() === 'true') return true;
    const statusText = String(t.status || '').toUpperCase();
    if (statusText === 'FINISHED' || statusText === 'FINALIZADO') return true;
    return false;
};

// --- CARREGAR DADOS ---
const loadHistory = async () => {
    const userId = getUserIdSafe();
    if (!userId) return;

    startLoader();
    try {
        const res = await tournamentService.getHistory(userId);
        
        console.log("💰 DADOS DO HISTÓRICO ATUALIZADOS:", res.data);

        if (res.data && Array.isArray(res.data)) {
            tournaments.value = res.data.map((t: any) => {
                
                // 1. Pega o Dinheiro Real
                const money = t.realPrize ?? t.RealPrize ?? 0;

                return {
                    ...t,
                    id: t.id || t.Id,
                    name: t.name || t.Name || 'Torneio',
                    isFinished: t.isFinished ?? t.IsFinished ?? t.isFinish ?? t.IsFinish ?? false,
                    endDate: t.endDate || t.EndDate,
                    entryFee: t.entryFee ?? t.EntryFee ?? 0,
                    
                    // Mapeia dinheiro real
                    moneyPrize: money,
                    
                    rank: t.rank ?? t.Rank ?? 0,
                    status: t.status || t.Status || ''
                };
            });
        }
    } catch (e) {
        console.error("❌ Falha ao carregar histórico:", e);
    } finally {
        finishLoader();
    }
};

onMounted(() => {
    loadHistory();
});

watch(() => authStore.user, (newUser) => {
    if (newUser && tournaments.value.length === 0) {
        loadHistory();
    }
}, { deep: true });

// --- COMPUTEDS ---
const filteredList = computed(() => {
    let list = tournaments.value;

    if (filterStatus.value === 'ACTIVE') {
        list = list.filter(t => !isItemFinished(t));
    } else if (filterStatus.value === 'FINISHED') {
        list = list.filter(t => isItemFinished(t));
    }

    if (searchQuery.value) {
        const query = searchQuery.value.toLowerCase();
        list = list.filter(t => t.name.toLowerCase().includes(query));
    }

    return list.sort((a, b) => {
        const dateA = new Date(a.endDate || 0).getTime();
        const dateB = new Date(b.endDate || 0).getTime();
        return dateB - dateA; 
    });
});

// --- HELPERS VISUAIS ---
const formatCurrency = (val: number) => {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(val);
};

const formatDate = (dateStr: string) => {
    if (!dateStr) return '--/--';
    const d = new Date(dateStr);
    return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute:'2-digit' });
};

const getRankColor = (rank: number) => {
    if (rank === 1) return 'text-yellow-400 drop-shadow-md';
    if (rank === 2) return 'text-gray-300';
    if (rank === 3) return 'text-orange-400';
    return 'text-slate-400';
};

const getBorderClass = (item: any) => {
    const finished = isItemFinished(item);
    if (!finished) return 'border-blue-500 border-l-4'; 
    if ((item.moneyPrize || 0) > 0) return 'border-emerald-500 border-l-4'; 
    return 'border-red-500 border-l-4'; 
};

const getStatusBadgeClass = (item: any) => {
    const finished = isItemFinished(item);
    if (!finished) return 'bg-blue-500/10 text-blue-400 border-blue-500/20';
    if ((item.moneyPrize || 0) > 0) return 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20';
    return 'bg-slate-700/30 text-slate-400 border-slate-600/30';
};

const getReturnTextClass = (item: any) => {
    const finished = isItemFinished(item);
    if (!finished) return 'text-white';
    if ((item.moneyPrize || 0) > 0) return 'text-[#00ffb9] drop-shadow-[0_0_5px_rgba(0,255,185,0.3)]';
    return 'text-slate-500 decoration-1';
};

// --- NAVEGAÇÃO E ABRIR MODAL ---
const openTournamentDetails = (tournament: any) => {
    selectedTournament.value = tournament;
    showDetailsModal.value = true;
};

// 👇 Função restaurada para entrar direto no torneio
const navigateToTournament = (id: number) => {
    router.push(`/tournament/${id}/play`);
};

const goBack = () => {
    router.back();
};
</script>

<template>
    <div class="relative h-full flex flex-col bg-[#0f172a] text-slate-300 overflow-hidden loader-scope z-40">
        
        <PageLoader 
            :is-loading="isLoading" 
            :progress="loadingProgress"
            loading-text="Carregando Histórico..."
        />

        <div class="flex-shrink-0 h-[180px] bg-gradient-to-br from-[#050505] to-[#0f172a] border-b border-white/10 px-4 flex flex-col justify-center z-50 relative shadow-md">
            <div class="max-w-4xl mx-auto w-full flex flex-col gap-4">
                
                <div class="flex items-center justify-between">
                    <div class="flex items-center gap-4">
                        <button 
                            @click="goBack"
                            class="w-12 h-12 rounded-2xl border border-white/10 bg-white/5 flex items-center justify-center hover:bg-white/10 hover:border-white/20 transition-all shrink-0"
                        >
                            <ArrowLeft class="w-5 h-5 text-slate-300" />
                        </button>
                        
                        <div>
                            <h2 class="text-2xl font-bold text-white tracking-tight leading-none flex items-center gap-3">
                                <Trophy class="w-6 h-6 text-yellow-500" />
                                Meus Torneios
                            </h2>
                            <p class="text-xs text-slate-400 font-medium mt-1 hidden md:block">
                                Histórico completo e competições ativas.
                            </p>
                        </div>
                    </div>
                    
                    <div class="relative w-32 md:w-56 group hidden md:block">
                        <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500 group-focus-within:text-blue-400 transition-colors" />
                        <input 
                            v-model="searchQuery" 
                            type="text" 
                            placeholder="Buscar..." 
                            class="w-full bg-white/5 border border-white/10 rounded-xl pl-10 pr-4 py-2 text-xs text-white focus:border-blue-500/50 focus:bg-white/10 outline-none transition-all placeholder:text-slate-500"
                        />
                    </div>
                </div>

                <div class="flex p-1 bg-[#0b1121]/50 rounded-xl border border-white/10 relative shadow-inner gap-1 backdrop-blur-sm">
                    <button 
                        @click="filterStatus = 'ALL'"
                        class="flex-1 py-2 rounded-lg text-[10px] font-bold uppercase tracking-widest transition-all duration-300 flex items-center justify-center gap-2 relative overflow-hidden group"
                        :class="filterStatus === 'ALL' 
                            ? 'text-white shadow-[0_0_20px_rgba(37,99,235,0.15)] border border-blue-500/30' 
                            : 'text-slate-500 hover:text-slate-300 hover:bg-white/5 border border-transparent'"
                    >
                        <div v-if="filterStatus === 'ALL'" class="absolute inset-0 bg-gradient-to-br from-blue-600 to-indigo-700 opacity-100 transition-opacity"></div>
                        <LayoutList class="w-3.5 h-3.5 relative z-10" :class="filterStatus === 'ALL' ? 'text-white' : 'text-slate-600 group-hover:text-slate-400'" />
                        <span class="relative z-10">Todos</span>
                    </button>

                    <button 
                        @click="filterStatus = 'ACTIVE'"
                        class="flex-1 py-2 rounded-lg text-[10px] font-bold uppercase tracking-widest transition-all duration-300 flex items-center justify-center gap-2 relative overflow-hidden group"
                        :class="filterStatus === 'ACTIVE' 
                            ? 'text-white shadow-[0_0_20px_rgba(34,197,94,0.15)] border border-green-500/30' 
                            : 'text-slate-500 hover:text-slate-300 hover:bg-white/5 border border-transparent'"
                    >
                        <div v-if="filterStatus === 'ACTIVE'" class="absolute inset-0 bg-gradient-to-br from-green-600 to-emerald-700 opacity-100 transition-opacity"></div>
                        <Zap class="w-3.5 h-3.5 relative z-10" :class="filterStatus === 'ACTIVE' ? 'text-white fill-white' : 'text-slate-600 group-hover:text-slate-400'" />
                        <span class="relative z-10">Ativos</span>
                    </button>

                    <button 
                        @click="filterStatus = 'FINISHED'"
                        class="flex-1 py-2 rounded-lg text-[10px] font-bold uppercase tracking-widest transition-all duration-300 flex items-center justify-center gap-2 relative overflow-hidden group"
                        :class="filterStatus === 'FINISHED' 
                            ? 'text-white shadow-[0_0_20px_rgba(100,116,139,0.15)] border border-slate-500/30' 
                            : 'text-slate-500 hover:text-slate-300 hover:bg-white/5 border border-transparent'"
                    >
                        <div v-if="filterStatus === 'FINISHED'" class="absolute inset-0 bg-gradient-to-br from-slate-600 to-slate-700 opacity-100 transition-opacity"></div>
                        <CheckCircle2 class="w-3.5 h-3.5 relative z-10" :class="filterStatus === 'FINISHED' ? 'text-white' : 'text-slate-600 group-hover:text-slate-400'" />
                        <span class="relative z-10">Finalizados</span>
                    </button>
                </div>

            </div>
        </div>

        <div class="flex-1 overflow-y-auto custom-scrollbar p-3 md:p-4 pb-20 relative z-0">
            
            <div class="md:hidden px-4 mb-4">
                 <div class="relative group">
                    <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500 group-focus-within:text-blue-400 transition-colors" />
                    <input 
                        v-model="searchQuery" 
                        type="text" 
                        placeholder="Buscar torneio..." 
                        class="w-full bg-white/5 border border-white/10 rounded-xl pl-10 pr-4 py-2.5 text-sm text-white focus:border-blue-500/50 focus:bg-white/10 outline-none transition-all placeholder:text-slate-500"
                    />
                </div>
            </div>

            <div v-if="!isLoading && filteredList.length === 0" class="flex flex-col items-center justify-center h-64 opacity-50 border border-dashed border-slate-700 rounded-lg m-2 bg-[#1e293b]/30">
                <Filter class="w-10 h-10 mb-2 text-slate-600" />
                <p class="text-sm text-slate-500">Nenhum torneio encontrado.</p>
                <p class="text-xs text-slate-700 mt-2">Total carregado: {{ tournaments.length }}</p>
            </div>

            <div class="space-y-3 max-w-4xl mx-auto">
                <div 
                    v-for="item in filteredList" 
                    :key="item.id"
                    @click="openTournamentDetails(item)"
                    class="bg-[#1e293b] rounded-lg shadow-lg overflow-hidden transition-all hover:bg-[#263345] cursor-pointer group hover:-translate-y-0.5 hover:shadow-xl duration-300"
                    :class="getBorderClass(item)"
                >
                    <div class="px-4 py-3 flex justify-between items-center border-b border-slate-700/50">
                        <div class="flex flex-col gap-1 flex-1 min-w-0 pr-4">
                            <div class="flex items-center gap-2">
                                <span 
                                    class="text-[9px] font-black uppercase tracking-wider px-2 py-0.5 rounded border"
                                    :class="getStatusBadgeClass(item)"
                                >
                                    {{ isItemFinished(item) ? 'Finalizado' : 'Em Andamento' }}
                                </span>
                                <span class="text-[10px] text-slate-500 font-mono tracking-wide">ID: #{{ item.id }}</span>
                            </div>
                            <h3 class="text-sm font-bold text-white truncate group-hover:text-blue-400 transition-colors">
                                {{ item.name }}
                            </h3>
                            <div class="flex items-center gap-3 mt-1 text-[11px] text-slate-400">
                                <span class="flex items-center gap-1">
                                    <Clock class="w-3 h-3" /> {{ formatDate(item.endDate) }}
                                </span>
                                <span v-if="isItemFinished(item) && item.rank" class="flex items-center gap-1 font-bold" :class="getRankColor(item.rank)">
                                    <Medal class="w-3 h-3" /> #{{ item.rank }}
                                </span>
                            </div>
                        </div>

                        <div class="self-center">
                            <button v-if="!isItemFinished(item)"
                                @click.stop="navigateToTournament(item.id)"
                                class="flex items-center justify-center gap-1.5 px-4 md:px-5 py-2 bg-gradient-to-r from-emerald-500 to-green-600 hover:from-emerald-400 hover:to-green-500 text-white rounded-lg shadow-[0_4px_15px_rgba(16,185,129,0.4)] transition-transform hover:scale-105 active:scale-95"
                            >
                                <Play class="w-4 h-4 fill-current" />
                                <span class="text-[10px] md:text-xs font-black uppercase tracking-widest">Jogar</span>
                            </button>

                            <button v-else 
                                class="p-2 rounded-full bg-slate-800 group-hover:bg-slate-700 transition-all text-slate-500 group-hover:text-white"
                            >
                                <ChevronRight class="w-5 h-5" />
                            </button>
                        </div>
                        </div>

                    <div class="px-4 py-2 bg-[#172231] flex items-center justify-between border-t border-slate-700/50">
                        <div class="flex flex-col">
                            <span class="text-[9px] text-slate-500 uppercase tracking-wide font-bold">Investido</span>
                            <span class="text-xs font-bold text-white tracking-wide">{{ formatCurrency(item.entryFee || 0) }}</span>
                        </div>

                        <div class="text-right">
                            <span class="text-[9px] uppercase tracking-wide block mb-0.5 font-bold"
                                  :class="isItemFinished(item) && (item.moneyPrize || 0) == 0 ? 'text-slate-500' : 'text-slate-400'">
                                {{ isItemFinished(item) ? 'Prêmio Recebido' : 'Prêmio Atual' }}
                            </span>
                            <span class="text-sm font-black tracking-wide flex items-center justify-end gap-1" :class="getReturnTextClass(item)">
                                <Coins v-if="(item.moneyPrize || 0) > 0" class="w-3.5 h-3.5" />
                                {{ (item.moneyPrize || 0) > 0 ? formatCurrency(item.moneyPrize) : (isItemFinished(item) ? 'Sem Prêmio' : '--') }}
                            </span>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <TournamentDetailsModal 
            :show="showDetailsModal" 
            :tournament="selectedTournament" 
            @update:show="showDetailsModal = $event" 
        />

    </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #475569; }

.loader-scope :deep(div[style*="fixed"]),
.loader-scope :deep(.fixed) {
    position: absolute !important;
    z-index: 50 !important;
}
</style>