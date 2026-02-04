<script setup lang="ts">
import { computed } from 'vue';
import { ChevronLeft, ChevronRight, Trophy, Ticket, Coins, Medal, Clock } from 'lucide-vue-next';

export interface Tournament {
  id: number;
  name: string;
  endDate: string;
  isJoined?: boolean;
  userRank?: number;
}

const props = defineProps<{
  tournaments: Tournament[];
  activeTournamentId: number;
  fantasyBalance: number;
  userRank: number;
}>();

const emit = defineEmits<{
  (e: 'select', tournamentId: number): void;
  (e: 'open-history'): void;
  (e: 'open-ranking'): void;
}>();

// --- LÓGICA ---
const getCircularIndex = (baseIndex: number, offset: number, length: number) => {
  if (length === 0) return 0;
  return (((baseIndex + offset) % length) + length) % length;
};

const activeIndex = computed(() => {
    const idx = props.tournaments.findIndex(t => String(t.id) === String(props.activeTournamentId));
    return idx === -1 ? 0 : idx;
});

const visibleItems = computed(() => {
  const list = [];
  const len = props.tournaments.length;
  if (len === 0) return [];

  // Renderiza 5 itens (central + 2 esquerda + 2 direita)
  for (let i = -2; i <= 2; i++) {
    const idx = getCircularIndex(activeIndex.value, i, len);
    const tournament = props.tournaments[idx];
    if (tournament) {
        list.push({ ...tournament, offset: i, isCenter: i === 0 });
    }
  }
  return list;
});

// Estilo Dinâmico para os Cards
const getCardStyle = (item: any) => {
    const absOffset = Math.abs(item.offset);
    
    // Escala: Principal (1) > Vizinhos (0.85) > Pontas (0.7)
    let scale = 1;
    if (absOffset === 1) scale = 0.85;
    if (absOffset === 2) scale = 0.70;

    // Translação: Espalhamento dos cards
    let translateX = 0;
    if (absOffset === 1) translateX = 105 * Math.sign(item.offset);
    if (absOffset === 2) translateX = 195 * Math.sign(item.offset);

    // Z-Index e Filtros
    const zIndex = 30 - absOffset * 10;
    const opacity = absOffset === 2 ? 0.6 : (absOffset === 1 ? 0.85 : 1);
    const brightness = 1 - (absOffset * 0.15);

    return {
        transform: `translateX(${translateX}%) scale(${scale}) translateZ(${-absOffset * 50}px)`,
        zIndex: zIndex,
        opacity: opacity,
        filter: item.isCenter ? 'none' : `brightness(${brightness})`
    };
};

const navigate = (direction: 'prev' | 'next') => {
  const len = props.tournaments.length;
  if (len <= 1) return; 
  
  // ✅ LÓGICA INVERTIDA CONFORME PEDIDO:
  // Next (Seta Direita) -> Offset -1 (Traz o da esquerda pro centro)
  // Prev (Seta Esquerda) -> Offset +1 (Traz o da direita pro centro)
  const offset = direction === 'next' ? -1 : 1;
  
  const newIndex = getCircularIndex(activeIndex.value, offset, len);
  const newTournament = props.tournaments[newIndex];
  if (newTournament) emit('select', newTournament.id);
};

const selectItem = (item: any) => {
  if (item.isCenter) return;
  emit('select', item.id);
};

const getDaysRemaining = (dateStr?: string) => {
  if (!dateStr) return '...';
  const diff = new Date(dateStr).getTime() - new Date().getTime();
  const days = Math.ceil(diff / (1000 * 60 * 60 * 24));
  return days > 0 ? `${days}d` : 'Fim';
};
</script>

<template>
  <div class="flex flex-col w-full bg-[#0f172a] relative z-20 border-b border-white/5 pb-2 transition-all duration-300">
    
    <div class="absolute top-0 left-1/2 -translate-x-1/2 w-[500px] h-[200px] bg-blue-600/5 blur-[80px] rounded-full pointer-events-none"></div>

    <div class="relative w-full py-2 select-none overflow-hidden h-[100px] flex items-center mt-2">
        
        <button v-if="tournaments.length > 1" @click.stop="navigate('prev')" class="absolute left-1 z-40 p-1.5 bg-slate-800/60 hover:bg-blue-600 rounded-full text-white transition-all backdrop-blur-md border border-white/10 hover:scale-110 shadow-lg">
          <ChevronLeft class="w-4 h-4" />
        </button>

        <button v-if="tournaments.length > 1" @click.stop="navigate('next')" class="absolute right-1 z-40 p-1.5 bg-slate-800/60 hover:bg-blue-600 rounded-full text-white transition-all backdrop-blur-md border border-white/10 hover:scale-110 shadow-lg">
          <ChevronRight class="w-4 h-4" />
        </button>

        <div class="max-w-[1200px] mx-auto flex items-center justify-center w-full h-full perspective-container relative z-10">
            
            <div v-if="tournaments.length === 0" class="flex flex-col items-center justify-center text-gray-600 animate-pulse scale-75">
                <Trophy class="w-6 h-6 mb-1" />
                <span class="text-[9px] uppercase tracking-widest font-bold">Carregando...</span>
            </div>

            <div v-else class="flex items-center justify-center w-full h-full relative">
                <transition-group name="list" tag="div" class="flex items-center justify-center relative w-full h-full">
                  <div v-for="item in visibleItems" :key="`${item.id}_${item.offset}`" @click="selectItem(item)"
                    class="absolute transition-all duration-500 ease-[cubic-bezier(0.2,0.8,0.2,1)] cursor-pointer flex flex-col items-center justify-center rounded-lg overflow-hidden border"
                    :style="getCardStyle(item)"
                    :class="[
                      item.isCenter 
                        ? 'w-[260px] h-[75px] bg-gradient-to-br from-[#1e293b] to-[#0f172a] border-blue-500/50 shadow-[0_0_20px_rgba(59,130,246,0.25)] ring-1 ring-blue-400/30' 
                        : 'w-[220px] h-[75px] bg-[#1e293b]/70 backdrop-blur-sm border-white/5 hover:border-white/20 hover:bg-[#1e293b] shadow-md'
                    ]"
                  >
                      <div v-if="item.isCenter" class="w-full h-full p-2.5 flex flex-col justify-between relative">
                          <div class="absolute top-0 right-0">
                              <div class="bg-blue-600 text-white text-[7px] font-black px-2 py-0.5 rounded-bl-lg shadow-sm uppercase tracking-wider">
                                  ATIVO
                              </div>
                          </div>
                          
                          <div class="absolute inset-0 bg-gradient-to-r from-transparent via-white/5 to-transparent -skew-x-12 translate-x-[-150%] group-hover:translate-x-[150%] transition-transform duration-1000 ease-in-out"></div>

                          <div class="flex flex-col items-start w-full pr-10">
                              <h2 class="text-white font-black italic text-sm uppercase tracking-wide truncate w-full leading-tight drop-shadow-md">
                                  {{ item.name }}
                              </h2>
                              <span class="text-blue-400 text-[8px] font-bold tracking-widest uppercase flex items-center gap-1 mt-0.5">
                                  <Trophy class="w-2.5 h-2.5" /> Principal
                              </span>
                          </div>

                          <div class="flex items-center justify-between w-full border-t border-white/10 pt-1 mt-0.5">
                              <div class="flex items-center gap-1 text-[9px] text-slate-300 font-medium">
                                  <Clock class="w-2.5 h-2.5 text-blue-400" />
                                  <span>{{ getDaysRemaining(item.endDate) }}</span>
                              </div>
                              <span class="text-[9px] font-mono text-slate-500">ID: {{ item.id }}</span>
                          </div>
                      </div>

                      <div v-else class="w-full h-full p-2.5 flex flex-row items-center gap-2.5 relative">
                          <div class="w-8 h-8 rounded-full bg-gradient-to-b from-gray-700 to-gray-900 border border-white/10 flex items-center justify-center shrink-0 shadow-inner">
                              <Trophy class="w-4 h-4 text-gray-300" />
                          </div>
                          <div class="flex flex-col min-w-0 justify-center">
                              <span class="text-[10px] font-bold text-gray-200 uppercase truncate w-full leading-tight">
                                  {{ item.name }}
                              </span>
                              <div class="flex items-center gap-2 mt-0.5">
                                  <span class="text-[8px] text-gray-500 font-mono">ID: {{ item.id }}</span>
                                  <span v-if="item.isJoined" class="flex items-center gap-0.5 text-[7px] text-green-400 bg-green-900/20 px-1 rounded border border-green-500/20">
                                      INSCRITO
                                  </span>
                              </div>
                          </div>
                      </div>
                  </div>
                </transition-group>
            </div>
        </div>
    </div>

    <div class="flex justify-center items-center gap-8 mt-1">
        
        <button @click="emit('open-history')" class="group flex flex-col items-center gap-1 cursor-pointer active:scale-95 transition-all w-16">
            <div class="p-1.5 rounded-lg border border-white/10 bg-white/5 group-hover:bg-white/10 group-hover:border-blue-500/30 transition-colors shadow-sm">
                <Ticket class="w-3.5 h-3.5 text-slate-400 group-hover:text-blue-400 transition-colors" />
            </div>
            <span class="text-[8px] font-bold uppercase tracking-wider text-slate-500 group-hover:text-blue-300">Apostas</span>
        </button>

        <div class="flex flex-col items-center justify-center">
            <div class="flex items-center gap-1.5 px-4 py-1.5 rounded-full border border-emerald-500/30 bg-emerald-500/10 shadow-[0_0_15px_rgba(16,185,129,0.15)] backdrop-blur-sm group hover:border-emerald-400/50 transition-colors">
                <Coins class="w-3.5 h-3.5 text-emerald-400 group-hover:text-emerald-300 transition-colors" />
                <span class="text-white font-mono font-bold text-sm tracking-tight drop-shadow-sm">{{ fantasyBalance.toFixed(2) }}</span>
            </div>
            <span class="text-[7px] font-black uppercase text-emerald-600/70 mt-0.5 tracking-[0.15em]">Seu Saldo</span>
        </div>

        <button @click="emit('open-ranking')" class="group flex flex-col items-center gap-1 cursor-pointer active:scale-95 transition-all relative w-16">
            <div class="p-1.5 rounded-lg border border-white/10 bg-white/5 group-hover:bg-white/10 group-hover:border-yellow-500/30 transition-colors shadow-sm relative">
                <Trophy class="w-3.5 h-3.5 text-slate-400 group-hover:text-yellow-400 transition-colors" />
                <span v-if="userRank > 0" class="absolute -top-0.5 -right-0.5 w-1.5 h-1.5 bg-red-500 rounded-full animate-pulse border border-[#0f172a]"></span>
            </div>
            <span class="text-[8px] font-bold uppercase tracking-wider text-slate-500 group-hover:text-yellow-300">Ranking</span>
        </button>

    </div>

    <div v-if="userRank > 0" class="flex justify-center mt-1">
        <div class="text-[8px] font-bold text-yellow-600 bg-yellow-500/5 border border-yellow-500/10 px-2 py-0.5 rounded-full flex items-center gap-1 shadow-sm">
            <Medal class="w-2.5 h-2.5" /> #{{ userRank }}
        </div>
    </div>

  </div>
</template>

<style scoped>
.perspective-container {
    perspective: 900px;
}
.list-move {
  transition: transform 0.5s ease;
}
</style>