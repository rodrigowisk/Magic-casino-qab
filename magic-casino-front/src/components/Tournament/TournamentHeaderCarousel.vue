<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { ChevronLeft, ChevronRight, Trophy, Ticket, Coins, Medal, Info } from 'lucide-vue-next';

export interface Tournament {
  id: number;
  name: string;
  endDate: string;
  isJoined?: boolean;
  userRank?: number;
  prizePool?: number;
  entryFee?: number;
  participants?: number;
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
  (e: 'open-details', id: number): void;
}>();

// --- DETECÇÃO MOBILE ---
const isMobile = ref(false);

const checkScreenSize = () => {
    if (typeof window !== 'undefined') {
        isMobile.value = window.innerWidth < 768;
    }
};

onMounted(() => {
    checkScreenSize();
    window.addEventListener('resize', checkScreenSize);
});

onUnmounted(() => {
    window.removeEventListener('resize', checkScreenSize);
});

// --- LÓGICA DO CARROSSEL ---
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

  // Range reduzido no mobile para evitar repetição visual
  const range = isMobile.value ? 1 : 2;

  for (let i = -range; i <= range; i++) {
    const idx = getCircularIndex(activeIndex.value, i, len);
    const tournament = props.tournaments[idx];
    if (tournament) {
        list.push({ ...tournament, offset: i, isCenter: i === 0 });
    }
  }
  return list;
});

const getCardStyle = (item: any) => {
    const absOffset = Math.abs(item.offset);
    
    let scale = 1;
    if (absOffset === 1) scale = isMobile.value ? 0.8 : 0.85; 
    if (absOffset === 2) scale = isMobile.value ? 0.7 : 0.75; 

    let translateX = 0;
    
    if (isMobile.value) {
        // MOBILE: Ajuste de espaçamento para card de 215px
        if (absOffset === 1) translateX = 120 * Math.sign(item.offset); 
        if (absOffset === 2) translateX = 210 * Math.sign(item.offset); 
    } else {
        // DESKTOP: Ajuste padrão
        if (absOffset === 1) translateX = 135 * Math.sign(item.offset); 
        if (absOffset === 2) translateX = 235 * Math.sign(item.offset); 
    }

    const zIndex = 30 - absOffset * 10;
    const opacity = absOffset === 2 ? 0.5 : (absOffset === 1 ? 0.85 : 1);
    const brightness = 1 - (absOffset * (isMobile.value ? 0.3 : 0.15)); 

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
  const offset = direction === 'next' ? -1 : 1; 
  const newIndex = getCircularIndex(activeIndex.value, offset, len);
  const newTournament = props.tournaments[newIndex];
  if (newTournament) emit('select', newTournament.id);
};

const selectItem = (item: any) => {
  if (item.isCenter) return;
  emit('select', item.id);
};
</script>

<template>
  <div class="flex flex-col w-full relative z-20 pb-2 overflow-hidden min-h-[160px] border-b border-white/5 transition-all duration-300 bg-[#0f172a]">
    
    <div class="absolute inset-0 z-0 pointer-events-none overflow-hidden">
        <div class="absolute inset-0 bg-[#020617]"></div>
        <div class="absolute -top-[150px] left-1/2 -translate-x-1/2 w-[1000px] h-[500px] 
                    bg-[radial-gradient(circle,rgba(59,130,246,0.12)_0%,rgba(79,70,229,0.08)_40%,rgba(2,6,23,0)_70%)] 
                    blur-3xl"></div>
    </div>

    <div class="relative w-full py-4 select-none h-[120px] flex items-center justify-center z-10">
        <div class="w-full max-w-[1050px] relative h-full flex items-center justify-center px-2">
            
            <button v-if="tournaments.length > 1" @click.stop="navigate('prev')" class="hidden md:flex absolute left-0 md:-left-4 top-1/2 -translate-y-1/2 z-50 p-1.5 bg-slate-800/80 hover:bg-blue-600 rounded-full text-white transition-all backdrop-blur-md border border-white/10 hover:scale-110 shadow-lg cursor-pointer items-center justify-center">
              <ChevronLeft class="w-5 h-5" />
            </button>

            <button v-if="tournaments.length > 1" @click.stop="navigate('next')" class="hidden md:flex absolute right-0 md:-right-4 top-1/2 -translate-y-1/2 z-50 p-1.5 bg-slate-800/80 hover:bg-blue-600 rounded-full text-white transition-all backdrop-blur-md border border-white/10 hover:scale-110 shadow-lg cursor-pointer items-center justify-center">
              <ChevronRight class="w-5 h-5" />
            </button>

            <div class="w-full h-full flex items-center justify-center perspective-container relative">
                <div v-if="tournaments.length === 0" class="flex flex-col items-center justify-center text-gray-500 animate-pulse scale-90">
                    <Trophy class="w-8 h-8 mb-2 opacity-50" />
                    <span class="text-[10px] uppercase tracking-widest font-bold">Carregando Torneios...</span>
                </div>

                <div v-else class="flex items-center justify-center w-full h-full relative">
                    <transition-group name="list" tag="div" class="flex items-center justify-center relative w-full h-full">
                      <div v-for="item in visibleItems" :key="`${item.id}_${item.offset}`" @click="selectItem(item)"
                        class="absolute transition-all duration-500 ease-[cubic-bezier(0.2,0.8,0.2,1)] cursor-pointer flex flex-col items-center justify-center"
                        :style="getCardStyle(item)"
                        :class="[
                           /* Removemos overflow-hidden daqui para as setas poderem sair */
                          item.isCenter 
                            ? 'w-[215px] h-[95px] md:w-[300px] md:h-[100px] z-50' 
                            : 'w-[140px] h-[65px] md:w-[200px] md:h-[70px] rounded-xl overflow-hidden bg-[#1e293b] border border-white/20 hover:border-white/40 shadow-lg'
                        ]"
                      >
                          <div v-if="item.isCenter" class="relative w-full h-full">
                              
                              <div class="w-full h-full bg-gradient-to-br from-[#1e293b] to-[#0f172a] border border-blue-500/50 shadow-[0_0_30px_rgba(59,130,246,0.25)] ring-1 ring-blue-400/30 rounded-xl overflow-hidden p-2.5 flex flex-col justify-between">
                                  <div>
                                    <div class="flex items-start justify-between w-full">
                                        <h2 class="text-white font-black italic text-sm uppercase truncate w-[70%] leading-none drop-shadow-md">
                                            {{ item.name }}
                                        </h2>
                                        <div class="bg-blue-600 text-white text-[7px] font-black px-1.5 py-0.5 rounded uppercase tracking-wider">ATIVO</div>
                                    </div>
                                    <span class="text-[8px] font-mono text-slate-400 block mt-0.5">ID: #{{ item.id }}</span>
                                  </div>

                                  <div class="flex items-center justify-center gap-2 mt-auto pt-2 border-t border-white/5">
                                      <div class="flex items-center gap-1">
                                          <button @click.stop="emit('open-history')" class="p-1.5 rounded bg-white/5 border border-white/10 hover:bg-blue-600/20 hover:border-blue-500/50 transition-all">
                                              <Ticket class="w-3 h-3 md:w-3.5 md:h-3.5 text-blue-400" />
                                          </button>
                                          <button @click.stop="emit('open-ranking')" class="p-1.5 rounded bg-white/5 border border-white/10 hover:bg-yellow-600/20 hover:border-yellow-500/50 transition-all">
                                              <Trophy class="w-3 h-3 md:w-3.5 md:h-3.5 text-yellow-400" />
                                          </button>
                                          <button @click.stop="emit('open-details', item.id)" class="p-1.5 rounded bg-white/5 border border-white/10 hover:bg-emerald-600/20 hover:border-emerald-500/50 transition-all">
                                              <Info class="w-3 h-3 md:w-3.5 md:h-3.5 text-emerald-400" />
                                          </button>
                                      </div>

                                      <div class="flex flex-col items-end">
                                          <div class="flex items-center gap-1 px-2 py-0.5 rounded-md bg-emerald-500/10 border border-emerald-500/20">
                                              <Coins class="w-3 h-3 text-emerald-400" />
                                              <span class="text-white font-mono font-bold text-[10px] md:text-[11px]">{{ fantasyBalance.toFixed(2) }}</span>
                                          </div>
                                      </div>
                                  </div>
                              </div>

                              <button v-if="tournaments.length > 1" @click.stop="navigate('prev')" class="flex md:hidden absolute -left-4 bottom-4 z-[60] p-1 bg-slate-800/90 hover:bg-blue-600 rounded-full text-white transition-all border border-white/20 shadow-lg cursor-pointer items-center justify-center">
                                  <ChevronLeft class="w-3.5 h-3.5" />
                              </button>
                              
                              <button v-if="tournaments.length > 1" @click.stop="navigate('next')" class="flex md:hidden absolute -right-4 bottom-4 z-[60] p-1 bg-slate-800/90 hover:bg-blue-600 rounded-full text-white transition-all border border-white/20 shadow-lg cursor-pointer items-center justify-center">
                                  <ChevronRight class="w-3.5 h-3.5" />
                              </button>

                          </div>

                          <div v-else class="w-full h-full p-2.5 flex flex-row items-center gap-2 md:gap-3 relative transition-all">
                              <div class="w-8 h-8 md:w-9 md:h-9 rounded-full bg-slate-900 border border-yellow-500/30 flex items-center justify-center shrink-0 shadow-[0_0_10px_rgba(234,179,8,0.1)]">
                                  <Trophy class="w-3.5 h-3.5 md:w-4 md:h-4 text-yellow-400" />
                              </div>
                              <div class="flex flex-col min-w-0">
                                  <span class="text-[9px] md:text-[10px] font-black text-gray-100 uppercase truncate drop-shadow-md">{{ item.name }}</span>
                                  <span class="text-[7px] md:text-[8px] text-slate-400 font-mono font-bold">ID: #{{ item.id }}</span>
                              </div>
                          </div>
                      </div>
                    </transition-group>
                </div>
            </div>
        </div>
    </div>

    <div v-if="userRank > 0" class="relative z-20 flex justify-center mt-1">
        <div class="text-[8px] md:text-[9px] font-bold text-yellow-600 bg-yellow-500/5 border border-yellow-500/10 px-3 py-0.5 rounded-full flex items-center gap-1 shadow-sm">
            <Medal class="w-2.5 h-2.5 md:w-3 md:h-3" /> Sua Posição: #{{ userRank }}
        </div>
    </div>

  </div>
</template>

<style scoped>
.perspective-container { perspective: 1200px; }
.list-move { transition: transform 0.5s ease; }
</style>