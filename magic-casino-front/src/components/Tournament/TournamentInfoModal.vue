<script setup lang="ts">
import { computed } from 'vue';
import { X, Trophy, Users, Banknote, Calendar, ShieldCheck, Gamepad2 } from 'lucide-vue-next';

// --- PROPS ---
const props = defineProps<{
  show: boolean;
  tournament: any | null; // Recebe o objeto do torneio
}>();

// --- EMITS ---
const emit = defineEmits(['close', 'join']);

// --- COMPUTEDS AUXILIARES ---
const formattedDate = computed(() => {
  if (!props.tournament?.startDate) return 'Aguardando início';
  return new Date(props.tournament.startDate).toLocaleString('pt-BR');
});

const statusLabel = computed(() => {
  if (props.tournament?.isFinished) return 'Finalizado';
  if (props.tournament?.isActive) return 'Em Andamento';
  return 'Aguardando';
});

const statusColor = computed(() => {
  if (props.tournament?.isFinished) return 'bg-red-500/10 text-red-500 border-red-500/20';
  if (props.tournament?.isActive) return 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20';
  return 'bg-blue-500/10 text-blue-500 border-blue-500/20';
});

// Formatação de Moeda
const formatCurrency = (val: number) => {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(val || 0);
};
</script>

<template>
  <Teleport to="body">
    <Transition name="modal-fade">
      <div v-if="show" class="fixed inset-0 z-[9999] flex items-center justify-center p-4">
        
        <div class="absolute inset-0 bg-black/80 backdrop-blur-sm" @click="emit('close')"></div>

        <div class="relative w-full max-w-lg bg-[#0f172a] border border-white/10 rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
          
          <div class="p-5 border-b border-white/5 flex items-start justify-between bg-[#1e293b]/50">
            <div class="flex flex-col gap-1">
               <h2 class="text-xl font-black italic text-white tracking-tight uppercase">
                 {{ tournament?.name || 'Detalhes do Torneio' }}
               </h2>
               <div class="flex items-center gap-2">
                 <span class="text-[10px] font-bold px-2 py-0.5 rounded border" :class="statusColor">
                    {{ statusLabel }}
                 </span>
                 <span class="text-xs text-slate-500">ID: #{{ tournament?.id }}</span>
               </div>
            </div>
            <button @click="emit('close')" class="p-2 bg-white/5 hover:bg-white/10 rounded-lg text-slate-400 hover:text-white transition-colors">
              <X class="w-5 h-5" />
            </button>
          </div>

          <div class="p-6 overflow-y-auto custom-scrollbar space-y-6">
            
            <div class="grid grid-cols-2 gap-4">
               
               <div class="p-4 rounded-lg bg-gradient-to-br from-yellow-500/10 to-transparent border border-yellow-500/20 flex flex-col gap-1">
                  <div class="flex items-center gap-2 text-yellow-500 mb-1">
                    <Trophy class="w-4 h-4" />
                    <span class="text-xs font-bold uppercase tracking-wider">Premiação</span>
                  </div>
                  <span class="text-xl font-black text-white">
                    {{ formatCurrency(tournament?.prizePool) }}
                  </span>
               </div>

               <div class="p-4 rounded-lg bg-white/5 border border-white/10 flex flex-col gap-1">
                  <div class="flex items-center gap-2 text-blue-400 mb-1">
                    <Banknote class="w-4 h-4" />
                    <span class="text-xs font-bold uppercase tracking-wider">Entrada</span>
                  </div>
                  <span class="text-xl font-black text-white">
                    {{ tournament?.entryFee === 0 ? 'GRÁTIS' : formatCurrency(tournament?.entryFee) }}
                  </span>
               </div>

            </div>

            <div class="space-y-3">
               <h3 class="text-sm font-bold text-white uppercase opacity-80 border-b border-white/5 pb-2">Informações Gerais</h3>
               
               <div class="grid grid-cols-1 sm:grid-cols-2 gap-y-4 gap-x-8 text-sm">
                  
                  <div class="flex items-center gap-3">
                     <div class="w-8 h-8 rounded bg-white/5 flex items-center justify-center text-slate-400">
                        <Users class="w-4 h-4" />
                     </div>
                     <div class="flex flex-col">
                        <span class="text-[10px] text-slate-500 uppercase font-bold">Participantes</span>
                        <span class="text-slate-200 font-medium">
                            {{ tournament?.participantsCount || 0 }} / {{ tournament?.maxParticipants || 'Ilimitado' }}
                        </span>
                     </div>
                  </div>

                  <div class="flex items-center gap-3">
                     <div class="w-8 h-8 rounded bg-white/5 flex items-center justify-center text-slate-400">
                        <Gamepad2 class="w-4 h-4" />
                     </div>
                     <div class="flex flex-col">
                        <span class="text-[10px] text-slate-500 uppercase font-bold">Esporte</span>
                        <span class="text-slate-200 font-medium capitalize">{{ tournament?.sport || 'Geral' }}</span>
                     </div>
                  </div>

                  <div class="flex items-center gap-3">
                     <div class="w-8 h-8 rounded bg-white/5 flex items-center justify-center text-slate-400">
                        <Calendar class="w-4 h-4" />
                     </div>
                     <div class="flex flex-col">
                        <span class="text-[10px] text-slate-500 uppercase font-bold">Início</span>
                        <span class="text-slate-200 font-medium">{{ formattedDate }}</span>
                     </div>
                  </div>

                  <div class="flex items-center gap-3">
                     <div class="w-8 h-8 rounded bg-white/5 flex items-center justify-center text-slate-400">
                        <ShieldCheck class="w-4 h-4" />
                     </div>
                     <div class="flex flex-col">
                        <span class="text-[10px] text-slate-500 uppercase font-bold">Regras</span>
                        <span class="text-slate-200 font-medium truncate w-[140px]">Padrão da Plataforma</span>
                     </div>
                  </div>

               </div>
            </div>

            <div v-if="tournament?.description" class="p-3 rounded bg-blue-900/10 border border-blue-500/20 text-xs text-blue-200 leading-relaxed">
               {{ tournament.description }}
            </div>

          </div>

          <div class="p-5 border-t border-white/5 bg-[#0b111e]">
             <button 
                @click="emit('join', tournament?.id)"
                :disabled="tournament?.isJoined"
                class="w-full py-3 rounded-lg font-bold uppercase tracking-widest transition-all shadow-lg flex items-center justify-center gap-2"
                :class="tournament?.isJoined 
                    ? 'bg-slate-700 text-slate-400 cursor-not-allowed' 
                    : 'bg-gradient-to-r from-red-600 to-red-700 hover:from-red-500 hover:to-red-600 text-white shadow-red-900/20'"
             >
                {{ tournament?.isJoined ? 'Você já está participando' : 'Entrar no Torneio' }}
             </button>
          </div>

        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
/* Animação do Modal */
.modal-fade-enter-active,
.modal-fade-leave-active {
  transition: opacity 0.3s ease;
}

.modal-fade-enter-from,
.modal-fade-leave-to {
  opacity: 0;
}

/* Custom Scrollbar */
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 2px; }
</style>