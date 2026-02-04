<script setup lang="ts">
import { useRouter } from 'vue-router';
import { CalendarDays } from 'lucide-vue-next';

const props = defineProps<{
  tournamentId: number;
  activeMode: 'prematch' | 'live';
}>();

const router = useRouter();

const goTo = (mode: 'prematch' | 'live') => {
  if (mode === props.activeMode) return;
  const routeName = mode === 'live' ? `/tournament/${props.tournamentId}/live` : `/tournament/${props.tournamentId}/play`;
  router.push(routeName);
};
</script>

<template>
  <div class="sticky top-0 z-30 bg-[#0f172a]/95 backdrop-blur-md border-b border-white/5 py-3 shadow-lg">
    <div class="max-w-[1200px] mx-auto px-2 md:px-4 flex flex-col md:flex-row items-center justify-between gap-3 md:gap-0">
      
      <div class="flex items-center gap-2 w-full md:w-auto overflow-x-auto custom-scrollbar pb-1 md:pb-0">
        <slot name="actions"></slot>
      </div>

      <div class="flex items-center gap-4 w-full md:w-auto justify-end">
        
        <div class="flex items-center bg-[#020617] p-1 rounded-full border border-white/10 shadow-inner">
          
          <button 
            @click="goTo('live')"
            class="relative flex items-center justify-center gap-2 px-4 py-1.5 rounded-full transition-all duration-300 text-[10px] md:text-xs font-bold uppercase tracking-wider border"
            :class="activeMode === 'live' 
              ? 'bg-red-500/10 border-red-500/50 text-red-400 shadow-[0_0_15px_rgba(248,113,113,0.3)]' 
              : 'bg-transparent border-transparent text-slate-500 hover:text-slate-300 hover:bg-white/5'"
          >
            <div class="relative flex h-2 w-2">
               <span v-if="activeMode === 'live'" class="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75"></span>
               <span class="relative inline-flex rounded-full h-2 w-2" :class="activeMode === 'live' ? 'bg-red-500 shadow-[0_0_8px_#ef4444]' : 'bg-slate-600'"></span>
            </div>
            Ao Vivo
          </button>

          <button 
            @click="goTo('prematch')"
            class="relative flex items-center justify-center gap-2 px-4 py-1.5 rounded-full transition-all duration-300 text-[10px] md:text-xs font-bold uppercase tracking-wider border"
            :class="activeMode === 'prematch' 
              ? 'bg-blue-500/10 border-blue-500/50 text-blue-400 shadow-[0_0_15px_rgba(96,165,250,0.3)]' 
              : 'bg-transparent border-transparent text-slate-500 hover:text-slate-300 hover:bg-white/5'"
          >
            <CalendarDays class="w-3.5 h-3.5" :class="activeMode === 'prematch' ? 'drop-shadow-[0_0_5px_rgba(96,165,250,0.8)]' : ''" />
            Pré-Jogo
          </button>

        </div>

        <div class="w-px h-8 bg-gradient-to-b from-transparent via-white/10 to-transparent hidden md:block"></div>

        <div class="overflow-x-auto custom-scrollbar flex-1 md:flex-none flex justify-end">
            <slot></slot>
        </div>

      </div>

    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; height: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 4px; }
</style>