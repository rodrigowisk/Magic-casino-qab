<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';
import { ChevronLeft, ChevronRight, Trophy, Ticket, Medal, Info, Play } from 'lucide-vue-next';

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
  (e: 'open-details', id: number): void;
}>();

const router = useRouter();

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
    if (absOffset === 1) scale = isMobile.value ? 0.85 : 0.80; 
    if (absOffset === 2) scale = isMobile.value ? 0.75 : 0.70; 

    let translateX = 0;
    
    if (isMobile.value) {
        if (absOffset === 1) translateX = 115 * Math.sign(item.offset); 
        if (absOffset === 2) translateX = 200 * Math.sign(item.offset); 
    } else {
        if (absOffset === 1) translateX = 140 * Math.sign(item.offset); 
        if (absOffset === 2) translateX = 240 * Math.sign(item.offset); 
    }

    const zIndex = 30 - absOffset * 10;
    const opacity = absOffset === 2 ? 0.4 : (absOffset === 1 ? 0.7 : 1);
    const brightness = 1 - (absOffset * 0.3); 

    return {
        transform: `translateX(${translateX}%) scale(${scale}) translateZ(${-absOffset * 50}px)`, 
        zIndex: zIndex,
        opacity: opacity,
        filter: item.isCenter ? 'none' : `brightness(${brightness}) grayscale(${absOffset * 50}%)`
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
  <div class="flex flex-col w-full relative z-20 pb-2 overflow-hidden min-h-[180px] border-b border-white/5 transition-all duration-300 bg-[#0f172a]">
    
    <div class="absolute inset-0 z-0 pointer-events-none overflow-hidden">
        <div class="absolute inset-0 bg-[#020617]"></div>
        <div class="absolute -top-[120px] left-1/2 -translate-x-1/2 w-[800px] h-[400px] 
                    bg-[radial-gradient(circle,rgba(37,99,235,0.15)_0%,rgba(15,23,42,0)_60%)] 
                    blur-3xl"></div>
    </div>

    <div class="relative w-full py-4 select-none h-[160px] flex items-center justify-center z-10">
        <div class="w-full max-w-[1050px] relative h-full flex items-center justify-center px-2">
            
            <button v-if="tournaments.length > 1" @click.stop="navigate('prev')" class="hidden md:flex absolute left-0 md:-left-4 top-1/2 -translate-y-1/2 z-50 p-2 bg-slate-800/50 hover:bg-blue-600 rounded-full text-white transition-all backdrop-blur-md border border-white/5 hover:border-blue-400 shadow-lg cursor-pointer items-center justify-center group">
              <ChevronLeft class="w-5 h-5 text-slate-400 group-hover:text-white" />
            </button>

            <button v-if="tournaments.length > 1" @click.stop="navigate('next')" class="hidden md:flex absolute right-0 md:-right-4 top-1/2 -translate-y-1/2 z-50 p-2 bg-slate-800/50 hover:bg-blue-600 rounded-full text-white transition-all backdrop-blur-md border border-white/5 hover:border-blue-400 shadow-lg cursor-pointer items-center justify-center group">
              <ChevronRight class="w-5 h-5 text-slate-400 group-hover:text-white" />
            </button>

            <div class="w-full h-full flex items-center justify-center perspective-container relative">
                
                <div v-if="tournaments.length === 0" class="flex flex-col items-center justify-center text-gray-500 animate-pulse scale-90">
                    <Trophy class="w-8 h-8 mb-2 opacity-50" />
                    <span class="text-[10px] uppercase tracking-widest font-bold">Carregando...</span>
                </div>

                <div v-else class="flex items-center justify-center w-full h-full relative">
                    <transition-group name="list" tag="div" class="flex items-center justify-center relative w-full h-full">
                      <div v-for="item in visibleItems" :key="`${item.id}_${item.offset}`" @click="selectItem(item)"
                        class="absolute transition-all duration-500 ease-[cubic-bezier(0.23,1,0.32,1)] cursor-pointer flex flex-col items-center justify-center"
                        :style="getCardStyle(item)"
                        :class="[
                          item.isCenter 
                            ? 'w-[280px] h-[135px] md:w-[340px] md:h-[145px] z-50' 
                            : 'w-[150px] h-[80px] md:w-[220px] md:h-[90px] rounded-xl overflow-hidden shadow-lg'
                        ]"
                      >
                          <div v-if="item.isCenter" class="relative w-full h-full group/maincard rounded-2xl overflow-hidden">
                              <div class="absolute inset-0 bg-gradient-to-br from-[#1e293b] via-[#0f172a] to-[#020617]"></div>
                              <div class="absolute inset-0 bg-gradient-to-br from-white/5 to-transparent opacity-50 pointer-events-none"></div>
                              
                              <div class="absolute inset-0 border border-white/10 rounded-2xl z-20 pointer-events-none"></div>

                              <div class="absolute top-0 right-0 w-[120px] h-[120px] bg-blue-500/20 blur-[50px] rounded-full pointer-events-none -mr-8 -mt-8 mix-blend-screen opacity-60"></div>
                              <div class="absolute bottom-0 left-0 w-[100px] h-[100px] bg-indigo-500/10 blur-[40px] rounded-full pointer-events-none -ml-6 -mb-6 opacity-50"></div>

                              <div class="relative h-full w-full flex flex-col justify-between p-3.5 z-30">
                                  
                                  <div class="w-full flex flex-col items-center justify-center relative -mt-1">
                                      <span class="text-[9px] font-mono font-bold text-slate-500/80 tracking-widest mb-0.5">
                                          #{{ item.id }}
                                      </span>
                                      <h2 class="text-transparent bg-clip-text bg-gradient-to-r from-white via-slate-200 to-slate-400 font-black italic text-lg md:text-xl uppercase tracking-wide truncate drop-shadow-sm text-center max-w-[95%] leading-none pb-1">
                                          {{ item.name }}
                                      </h2>
                                  </div>

                                  <div class="w-full flex justify-center">
                                      <div class="relative w-full max-w-[210px] bg-[#020617]/60 backdrop-blur-sm border border-white/10 rounded-lg px-4 h-[42px] shadow-[inset_0_2px_4px_rgba(0,0,0,0.5)] flex flex-col items-center justify-center transition-all group-hover/maincard:border-blue-500/20 group-hover/maincard:bg-[#020617]/80 overflow-hidden">
                                          <span class="absolute top-0 pt-[2px] w-full text-center text-[7px] font-bold text-slate-500 uppercase tracking-[0.25em] group-hover/maincard:text-blue-300/60 transition-colors">
                                              Saldo Torneio
                                          </span>
                                          <div class="flex items-baseline gap-1.5 mt-4">
                                              <span class="text-emerald-400 text-sm drop-shadow-[0_0_3px_rgba(52,211,153,0.5)]">●</span>
                                              <span class="text-white font-mono font-bold text-xl tracking-tight leading-none drop-shadow-md">
                                                  {{ fantasyBalance.toFixed(2) }}
                                              </span>
                                          </div>
                                      </div>
                                  </div>

                                  <div class="w-full grid grid-cols-4 gap-2">
                                      <button @click.stop="emit('open-history')" class="h-8 md:h-9 flex items-center justify-center rounded-md bg-white/5 border border-white/5 hover:bg-white/10 hover:border-white/20 transition-all active:scale-95" title="Histórico">
                                          <Ticket class="w-4 h-4 text-slate-400 group-hover:text-blue-300 transition-colors" />
                                      </button>
                                      <button @click.stop="router.push(`/tournament/${item.id}/ranking`)" class="h-8 md:h-9 flex items-center justify-center rounded-md bg-white/5 border border-white/5 hover:bg-white/10 hover:border-white/20 transition-all active:scale-95" title="Ranking">
                                          <Trophy class="w-4 h-4 text-slate-400 group-hover:text-yellow-400 transition-colors" />
                                      </button>
                                      <button @click.stop="emit('open-details', item.id)" class="h-8 md:h-9 flex items-center justify-center rounded-md bg-white/5 border border-white/5 hover:bg-white/10 hover:border-white/20 transition-all active:scale-95" title="Detalhes">
                                          <Info class="w-4 h-4 text-slate-400 group-hover:text-emerald-300 transition-colors" />
                                      </button>
                                      <button @click.stop="router.push(`/tournament/${item.id}/play`)" 
                                              class="h-8 md:h-9 flex items-center justify-center rounded-md bg-gradient-to-r from-blue-600 to-blue-500 border border-blue-400/30 shadow-lg hover:to-blue-400 transition-all active:scale-95 relative overflow-hidden" title="Jogar">
                                          <Play class="w-4 h-4 text-white fill-current relative z-10" />
                                      </button>
                                  </div>
                              </div>
                          </div>
                          
                          <div v-else class="w-full h-full relative group/sidecard overflow-hidden">
                              <div class="absolute inset-0 bg-gradient-to-br from-[#1e293b] via-[#0f172a] to-[#020617]"></div>
                              
                              <div class="absolute inset-0 border border-white/10 rounded-xl z-20 pointer-events-none group-hover/sidecard:border-white/20 transition-colors"></div>

                              <div class="relative z-30 w-full h-full flex flex-row items-center gap-3 p-3 opacity-50 group-hover/sidecard:opacity-100 transition-opacity duration-300">
                                  
                                  <div class="w-8 h-8 md:w-10 md:h-10 rounded-full bg-[#020617]/50 border border-white/5 flex items-center justify-center shrink-0 shadow-[inset_0_2px_4px_rgba(0,0,0,0.5)]">
                                      <Trophy class="w-3.5 h-3.5 md:w-4 md:h-4 text-slate-500 group-hover/sidecard:text-yellow-500 transition-colors duration-300" />
                                  </div>

                                  <div class="flex flex-col min-w-0">
                                      <span class="text-[10px] md:text-xs font-black italic uppercase text-slate-300 group-hover/sidecard:text-white truncate transition-colors duration-300 tracking-wide">
                                          {{ item.name }}
                                      </span>
                                      <span class="text-[7px] md:text-[8px] font-mono font-bold text-slate-600 group-hover/sidecard:text-slate-500 transition-colors">
                                          ID: #{{ item.id }}
                                      </span>
                                  </div>

                              </div>
                          </div>
                          </div>
                    </transition-group>
                </div>
            </div>
        </div>
    </div>

    <div v-if="userRank > 0" class="relative z-20 flex justify-center mt-1">
        <div class="text-[9px] md:text-[10px] font-bold text-yellow-500/90 bg-[#0f172a] border border-yellow-500/20 px-4 py-1 rounded-full flex items-center gap-1.5 shadow-sm backdrop-blur-md">
            <Medal class="w-3 h-3" /> 
            <span>Sua Posição: <span class="text-white">#{{ userRank }}</span></span>
        </div>
    </div>

  </div>
</template>

<style scoped>
.perspective-container { perspective: 1000px; }
.list-move { transition: transform 0.5s cubic-bezier(0.25, 1, 0.5, 1); }
</style>