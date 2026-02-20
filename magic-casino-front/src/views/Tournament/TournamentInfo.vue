<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { 
    Trophy, Users, Banknote, Calendar, ShieldCheck, 
    Gamepad2, Info, Clock, Percent, ChevronLeft 
} from 'lucide-vue-next';
import tournamentService from '../../services/Tournament/TournamentService';
import { useAuthStore } from '../../stores/useAuthStore';

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

const tournamentId = ref(Number(route.params.id));
const tournament = ref<any>(null);
const isLoading = ref(true);

// --- LÓGICA DE DADOS ---
const getUserId = () => {
    if (authStore.user) return authStore.user.id || authStore.user.code;
    const stored = localStorage.getItem('user');
    return stored ? JSON.parse(stored).id : '';
};

const loadData = async () => {
    isLoading.value = true;
    try {
        const userId = getUserId();
        const res = await tournamentService.getTournament(tournamentId.value, userId);
        tournament.value = res.data;
    } catch (error) {
        console.error("Erro ao carregar info:", error);
    } finally {
        isLoading.value = false;
    }
};

const goBack = () => {
    // Tenta voltar para a tela de jogo do torneio
    router.push({ name: 'TournamentPlay', params: { id: tournamentId.value } });
};

// --- COMPUTEDS AUXILIARES ---
const formattedStartDate = computed(() => {
  if (!tournament.value?.startDate) return '--/--/-- --:--';
  return new Date(tournament.value.startDate).toLocaleString('pt-BR', {
      day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit'
  });
});

const formattedEndDate = computed(() => {
  if (!tournament.value?.endDate) return '--/--/-- --:--';
  return new Date(tournament.value.endDate).toLocaleString('pt-BR', {
      day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit'
  });
});

const statusLabel = computed(() => {
  if (tournament.value?.isFinished) return 'Finalizado';
  if (tournament.value?.isActive) return 'Em Andamento';
  return 'Aguardando';
});

const statusColor = computed(() => {
  if (tournament.value?.isFinished) return 'text-red-500 bg-red-500/10 border-red-500/20';
  if (tournament.value?.isActive) return 'text-emerald-500 bg-emerald-500/10 border-emerald-500/20';
  return 'text-blue-500 bg-blue-500/10 border-blue-500/20';
});

const formatCurrency = (val: number) => {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(val || 0);
};

// Simulação de Estrutura de Premiação (Caso o backend não envie)
const prizeDistribution = computed(() => {
    if (tournament.value?.prizeDistribution) return tournament.value.prizeDistribution;
    
    // Fallback padrão
    return [
        { position: '1º Lugar', percent: '50%', value: (tournament.value?.prizePool || 0) * 0.5 },
        { position: '2º Lugar', percent: '30%', value: (tournament.value?.prizePool || 0) * 0.3 },
        { position: '3º Lugar', percent: '20%', value: (tournament.value?.prizePool || 0) * 0.2 },
    ];
});

watch(() => route.params.id, (newId) => {
    tournamentId.value = Number(newId);
    loadData();
});

onMounted(() => {
    loadData();
});
</script>

<template>
  <div class="flex flex-col h-full bg-[#0f172a] text-slate-300 font-sans overflow-hidden">
    
    <div class="shrink-0 flex items-center justify-between px-4 py-3 bg-[#0f172a] border-b border-white/5">
        <div class="flex items-center gap-3">
            <button @click="goBack" class="w-8 h-8 rounded-full bg-[#1e293b] hover:bg-white/10 flex items-center justify-center transition-colors border border-white/5 group">
                <ChevronLeft class="w-4 h-4 text-slate-400 group-hover:text-white" />
            </button>
            
            <div class="flex items-center gap-2">
                <h2 class="text-sm font-black uppercase tracking-widest text-white">
                    Informações do Torneio
                </h2>
            </div>
        </div>
        
        <div v-if="tournament" class="flex items-center gap-2">
            <span class="text-[10px] font-black uppercase px-2 py-0.5 rounded border" :class="statusColor">
                {{ statusLabel }}
            </span>
        </div>
    </div>

    <div class="flex-1 overflow-y-auto custom-scrollbar p-4">
        
        <div v-if="isLoading" class="space-y-4 animate-pulse max-w-5xl mx-auto">
            <div class="h-20 bg-[#1e293b] rounded border border-white/5"></div>
            <div class="h-40 bg-[#1e293b] rounded border border-white/5"></div>
        </div>

        <div v-else-if="tournament" class="max-w-6xl mx-auto space-y-4">

            <div class="flex flex-col gap-1 mb-2 pl-1">
                <h1 class="text-xl md:text-2xl font-black italic text-white uppercase tracking-tight">
                    {{ tournament.name }}
                </h1>
                <span class="text-[10px] font-mono text-slate-500 font-bold">ID: #{{ tournament.id }}</span>
            </div>

            <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
               
               <div class="bg-[#1e293b] border border-white/5 rounded p-3 flex flex-col justify-center relative overflow-hidden group">
                  <div class="flex items-center gap-2 text-yellow-500 mb-1 z-10">
                    <Trophy class="w-3.5 h-3.5" />
                    <span class="text-[10px] font-bold uppercase tracking-wider">Premiação</span>
                  </div>
                  <span class="text-lg md:text-xl font-black text-white z-10 tracking-tight">
                    {{ formatCurrency(tournament.prizePool) }}
                  </span>
                  <div class="absolute -right-4 -top-4 w-16 h-16 bg-yellow-500/10 rounded-full blur-xl group-hover:bg-yellow-500/20 transition-all"></div>
               </div>

               <div class="bg-[#1e293b] border border-white/5 rounded p-3 flex flex-col justify-center">
                  <div class="flex items-center gap-2 text-blue-400 mb-1">
                    <Banknote class="w-3.5 h-3.5" />
                    <span class="text-[10px] font-bold uppercase tracking-wider">Entrada</span>
                  </div>
                  <span class="text-lg md:text-xl font-black text-white tracking-tight">
                    {{ tournament.entryFee === 0 ? 'GRÁTIS' : formatCurrency(tournament.entryFee) }}
                  </span>
               </div>

                <div class="bg-[#1e293b] border border-white/5 rounded p-3 flex flex-col justify-center">
                  <div class="flex items-center gap-2 text-emerald-400 mb-1">
                    <Users class="w-3.5 h-3.5" />
                    <span class="text-[10px] font-bold uppercase tracking-wider">Jogadores</span>
                  </div>
                  <span class="text-lg md:text-xl font-black text-white tracking-tight">
                    {{ tournament.participantsCount || 0 }} <span class="text-slate-500 text-xs font-normal">/ {{ tournament.maxParticipants || '∞' }}</span>
                  </span>
               </div>

                <div class="bg-[#1e293b] border border-white/5 rounded p-3 flex flex-col justify-center">
                  <div class="flex items-center gap-2 text-purple-400 mb-1">
                    <Gamepad2 class="w-3.5 h-3.5" />
                    <span class="text-[10px] font-bold uppercase tracking-wider">Modalidade</span>
                  </div>
                  <span class="text-lg md:text-xl font-black text-white tracking-tight capitalize">
                    {{ tournament.sport || 'Geral' }}
                  </span>
               </div>

            </div>

            <div class="bg-[#1e293b] border border-white/5 rounded flex flex-col md:flex-row divide-y md:divide-y-0 md:divide-x divide-white/5">
                
                <div class="flex-1 p-3 flex items-center gap-3">
                    <div class="w-8 h-8 rounded bg-white/5 flex items-center justify-center text-slate-400 shrink-0">
                        <Calendar class="w-4 h-4" />
                    </div>
                    <div class="flex flex-col">
                        <span class="text-[9px] text-slate-500 uppercase font-bold tracking-wider">Início</span>
                        <span class="text-slate-200 font-bold text-xs">{{ formattedStartDate }}</span>
                    </div>
                </div>

                <div class="flex-1 p-3 flex items-center gap-3">
                    <div class="w-8 h-8 rounded bg-white/5 flex items-center justify-center text-slate-400 shrink-0">
                        <Clock class="w-4 h-4" />
                    </div>
                    <div class="flex flex-col">
                        <span class="text-[9px] text-slate-500 uppercase font-bold tracking-wider">Término</span>
                        <span class="text-slate-200 font-bold text-xs">{{ formattedEndDate }}</span>
                    </div>
                </div>

                <div class="flex-1 p-3 flex items-center gap-3">
                    <div class="w-8 h-8 rounded bg-white/5 flex items-center justify-center text-slate-400 shrink-0">
                        <ShieldCheck class="w-4 h-4" />
                    </div>
                    <div class="flex flex-col">
                        <span class="text-[9px] text-slate-500 uppercase font-bold tracking-wider">Regras</span>
                        <span class="text-slate-200 font-bold text-xs">Padrão da Plataforma</span>
                    </div>
                </div>

            </div>

            <div class="bg-[#1e293b] border border-white/5 rounded p-4">
                <h3 class="text-[11px] font-black uppercase tracking-widest text-slate-400 mb-3 flex items-center gap-2">
                    <Percent class="w-3.5 h-3.5" /> Distribuição da Premiação
                </h3>
                
                <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <div v-for="(prize, index) in prizeDistribution" :key="index" 
                         class="bg-[#0f172a] border border-white/5 rounded p-3 flex items-center justify-between">
                        <div class="flex flex-col">
                            <span class="text-[10px] text-slate-500 font-bold uppercase">{{ prize.position }}</span>
                            <span class="text-sm font-black text-yellow-500">{{ prize.percent }}</span>
                        </div>
                        <div class="text-right">
                             <span class="text-[10px] text-slate-500 font-bold uppercase block">Valor</span>
                             <span class="text-xs text-white font-bold">{{ formatCurrency(prize.value) }}</span>
                        </div>
                    </div>
                </div>
                <p class="text-[9px] text-slate-600 mt-2 italic">* A premiação pode variar se baseada no número de participantes (Pool Dinâmico).</p>
            </div>

            <div v-if="tournament.description" class="p-4 rounded border border-blue-500/20 bg-blue-500/5">
                <h4 class="text-blue-400 font-bold uppercase text-[10px] mb-2 flex items-center gap-2 tracking-wider">
                    <Info class="w-3 h-3" /> Sobre o Evento
                </h4>
                <p class="text-xs text-slate-300 leading-relaxed">
                    {{ tournament.description }}
                </p>
            </div>

        </div>
    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 2px; }
</style>