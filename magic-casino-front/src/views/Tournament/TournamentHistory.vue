<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { 
    Trophy, 
    Clock, 
    Medal,
    Search,
    Filter,
    ChevronRight,
    PlayCircle,
    // ✅ Novos ícones para o filtro
    LayoutList,
    Zap,
    CheckCircle2
} from 'lucide-vue-next';
import tournamentService from '../../services/Tournament/TournamentService';
import PageLoader from '../../components/PageLoader.vue';
import { usePageLoader } from '../../composables/usePageLoader';

const router = useRouter();
const { isLoading, loadingProgress, startLoader, finishLoader } = usePageLoader();

const tournaments = ref<any[]>([]);
const filterStatus = ref<'ALL' | 'ACTIVE' | 'FINISHED'>('ALL');
const searchQuery = ref('');

// --- CARREGAR DADOS ---
const loadHistory = async () => {
    startLoader();
    try {
        const stored = localStorage.getItem('user') || localStorage.getItem('user_data');
        let userId = '';
        if (stored) {
            const u = JSON.parse(stored);
            userId = u.cpf || u.id || u.code || '';
        }

        if (userId) {
            const res = await tournamentService.listTournaments(userId);
            if (res.data) {
                tournaments.value = res.data.filter((t: any) => t.isJoined);
            }
        }
    } catch (e) {
        console.error("Erro ao carregar histórico:", e);
    } finally {
        finishLoader();
    }
};

onMounted(() => {
    loadHistory();
});

// --- COMPUTEDS ---
const filteredList = computed(() => {
    let list = tournaments.value;

    if (filterStatus.value === 'ACTIVE') {
        list = list.filter(t => !t.isFinished);
    } else if (filterStatus.value === 'FINISHED') {
        list = list.filter(t => t.isFinished);
    }

    if (searchQuery.value) {
        const query = searchQuery.value.toLowerCase();
        list = list.filter(t => t.name.toLowerCase().includes(query));
    }

    return list.sort((a, b) => {
        if (a.isFinished === b.isFinished) {
            return new Date(b.endDate).getTime() - new Date(a.endDate).getTime();
        }
        return a.isFinished ? 1 : -1;
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
    if (!item.isFinished) return 'border-blue-500 border-l-4'; 
    if ((item.myPrize || 0) > (item.entryFee || 0)) return 'border-green-500 border-l-4'; 
    if ((item.myPrize || 0) > 0) return 'border-yellow-500 border-l-4'; 
    return 'border-red-500 border-l-4'; 
};

const getStatusBadgeClass = (item: any) => {
    if (!item.isFinished) return 'bg-blue-500/10 text-blue-400 border-blue-500/20';
    if ((item.myPrize || 0) > 0) return 'bg-green-500/10 text-green-400 border-green-500/20';
    return 'bg-slate-700/30 text-slate-400 border-slate-600/30';
};

const getReturnTextClass = (item: any) => {
    if (!item.isFinished) return 'text-white';
    if ((item.myPrize || 0) > 0) return 'text-[#00ffb9] drop-shadow-[0_0_5px_rgba(0,255,185,0.3)]';
    return 'text-slate-500 decoration-1';
};

const navigateToTournament = (id: number) => {
    router.push(`/tournament/${id}/play`);
};
</script>

<template>
    <div class="relative h-full flex flex-col bg-[#0f172a] text-slate-300 overflow-hidden loader-scope">
        
        <PageLoader 
            :is-loading="isLoading" 
            :progress="loadingProgress"
            loading-text="Carregando Histórico..."
        />

        <div class="flex-shrink-0 px-4 py-4 border-b border-slate-800 bg-[#141e2e] shadow-lg z-10">
            <div class="flex items-center justify-between mb-4">
                <div>
                    <h2 class="text-lg font-black text-white flex items-center gap-2 uppercase tracking-wide italic">
                        <Trophy class="w-5 h-5 text-yellow-500 fill-yellow-500/20" />
                        <span class="bg-gradient-to-r from-white to-slate-400 bg-clip-text text-transparent">
                            Meus Torneios
                        </span>
                    </h2>
                </div>
                
                <div class="relative w-32 md:w-56 group">
                    <Search class="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-slate-500 group-focus-within:text-blue-400 transition-colors" />
                    <input 
                        v-model="searchQuery" 
                        type="text" 
                        placeholder="Buscar torneio..." 
                        class="w-full bg-[#0f172a] border border-slate-700 rounded-full pl-9 pr-3 py-1.5 text-xs text-white focus:border-blue-500 focus:ring-1 focus:ring-blue-500 outline-none transition-all placeholder:text-slate-600 shadow-inner"
                    />
                </div>
            </div>

            <div class="flex p-1.5 bg-[#0b1121] rounded-xl border border-white/5 relative shadow-inner gap-1">
                <button 
                    @click="filterStatus = 'ALL'"
                    class="flex-1 py-2.5 rounded-lg text-[10px] md:text-xs font-bold uppercase tracking-widest transition-all duration-300 flex items-center justify-center gap-2 relative overflow-hidden group"
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
                    class="flex-1 py-2.5 rounded-lg text-[10px] md:text-xs font-bold uppercase tracking-widest transition-all duration-300 flex items-center justify-center gap-2 relative overflow-hidden group"
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
                    class="flex-1 py-2.5 rounded-lg text-[10px] md:text-xs font-bold uppercase tracking-widest transition-all duration-300 flex items-center justify-center gap-2 relative overflow-hidden group"
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

        <div class="flex-1 overflow-y-auto custom-scrollbar p-3 md:p-4 pb-20 relative">
            
            <div v-if="!isLoading && filteredList.length === 0" class="flex flex-col items-center justify-center h-64 opacity-50 border border-dashed border-slate-700 rounded-lg m-2 bg-[#1e293b]/30">
                <Filter class="w-10 h-10 mb-2 text-slate-600" />
                <p class="text-sm text-slate-500">Nenhum torneio encontrado.</p>
            </div>

            <div class="space-y-3 max-w-4xl mx-auto">
                <div 
                    v-for="item in filteredList" 
                    :key="item.id"
                    @click="navigateToTournament(item.id)"
                    class="bg-[#1e293b] rounded-lg shadow-lg overflow-hidden transition-all hover:bg-[#263345] cursor-pointer group hover:-translate-y-0.5 hover:shadow-xl duration-300"
                    :class="getBorderClass(item)"
                >
                    <div class="px-4 py-3 flex justify-between items-start border-b border-slate-700/50">
                        <div class="flex flex-col gap-1 flex-1 min-w-0 pr-4">
                            <div class="flex items-center gap-2">
                                <span 
                                    class="text-[9px] font-black uppercase tracking-wider px-2 py-0.5 rounded border"
                                    :class="getStatusBadgeClass(item)"
                                >
                                    {{ item.isFinished ? 'Finalizado' : 'Em Andamento' }}
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
                                <span v-if="item.isFinished && item.rank" class="flex items-center gap-1 font-bold" :class="getRankColor(item.rank)">
                                    <Medal class="w-3 h-3" /> #{{ item.rank }}
                                </span>
                            </div>
                        </div>

                        <div class="self-center">
                            <button class="p-2 rounded-full bg-slate-800 group-hover:bg-blue-600 group-hover:text-white transition-all text-slate-500">
                                <PlayCircle v-if="!item.isFinished" class="w-5 h-5" />
                                <ChevronRight v-else class="w-5 h-5" />
                            </button>
                        </div>
                    </div>

                    <div class="px-4 py-2 bg-[#172231] flex items-center justify-between border-t border-slate-700/50">
                        <div class="flex flex-col">
                            <span class="text-[9px] text-slate-500 uppercase tracking-wide font-bold">Investido</span>
                            <span class="text-xs font-bold text-white tracking-wide">{{ formatCurrency(item.entryFee) }}</span>
                        </div>

                        <div class="text-right">
                            <span class="text-[9px] uppercase tracking-wide block mb-0.5 font-bold"
                                  :class="item.isFinished && (item.myPrize || 0) == 0 ? 'text-slate-500' : 'text-slate-400'">
                                {{ item.isFinished ? 'Prêmio Recebido' : 'Prêmio Atual' }}
                            </span>
                            <span class="text-sm font-black tracking-wide" :class="getReturnTextClass(item)">
                                {{ (item.myPrize || 0) > 0 ? formatCurrency(item.myPrize) : (item.isFinished ? 'Sem Prêmio' : '--') }}
                            </span>
                        </div>
                    </div>
                </div>
            </div>
        </div>
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