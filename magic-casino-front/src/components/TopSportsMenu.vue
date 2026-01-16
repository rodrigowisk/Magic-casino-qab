<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { Trophy, Clock } from 'lucide-vue-next'; 
import SportsService from '../services/SportsService';

const router = useRouter();
const route = useRoute();
const loading = ref(true);
const sports = ref<any[]>([]);

// ✅ ATUALIZADO: Oculta menu em Detalhes E na página Ao Vivo (/live)
const shouldShowMenu = computed(() => {
  const isEventDetails = route.name === 'event-details' || route.path.includes('/event/');
  const isLivePage = route.path.includes('/live'); // Detecta a página ao vivo
  
  return !isEventDetails && !isLivePage;
});

const goToSport = (key: string) => {
    router.push({ name: 'sport-events', params: { id: key } });
};

const goToHistory = () => {
    router.push('/minhas-apostas');
};

const mapSportVisuals = (key: string) => {
    const k = key.toLowerCase();
    if (k.includes('soccer') || k.includes('futebol')) return { name: 'Futebol', color: 'from-green-500 to-green-700', icon: '⚽' };
    if (k.includes('basket')) return { name: 'Basquete', color: 'from-orange-500 to-red-600', icon: '🏀' };
    if (k.includes('tennis')) return { name: 'Tênis', color: 'from-yellow-500 to-orange-500', icon: '🎾' };
    if (k.includes('boxing')) return { name: 'Boxe', color: 'from-red-700 to-red-900', icon: '🥊' };
    if (k.includes('mma') || k.includes('ufc')) return { name: 'MMA', color: 'from-slate-700 to-red-900', icon: '🥋' };
    if (k.includes('esports') || k.includes('lol')) return { name: 'E-Sports', color: 'from-purple-600 to-blue-900', icon: '🎮' };
    if (k.includes('hockey')) return { name: 'Hóquei', color: 'from-blue-400 to-blue-600', icon: '🏒' };
    if (k.includes('american')) return { name: 'Fut. Americano', color: 'from-amber-700 to-amber-900', icon: '🏈' };
    if (k.includes('cricket')) return { name: 'Cricket', color: 'from-slate-600 to-slate-800', icon: '🏏' };
    return { name: key, color: 'from-gray-500 to-gray-700', icon: '🏆' };
};

onMounted(async () => {
    try {
        const data = await SportsService.getActiveSports();
        const grouped = data.reduce((acc: any, item: any) => {
            const visual = mapSportVisuals(item.key);
            const sportName = visual.name;
            if (!acc[sportName]) {
                acc[sportName] = { name: sportName, realKey: item.key, count: 0, color: visual.color, icon: visual.icon };
            }
            acc[sportName].count += item.count;
            return acc;
        }, {});
        sports.value = Object.values(grouped);
    } catch (error) {
        console.error("Erro ao carregar top sports:", error);
    } finally {
        loading.value = false;
    }
});
</script>

<template>
  <div v-if="shouldShowMenu" class="mb-6">
    <div class="flex items-center justify-between mb-4 pr-1">
      <h2 class="text-white font-bold flex items-center gap-2 text-xl">
          <Trophy class="w-6 h-6 text-yellow-500"/> Top Esportes
      </h2>

      <div class="flex items-center gap-2">
        <button 
          @click="goToHistory"
          class="flex items-center gap-2 px-3 py-1.5 bg-gray-700/60 hover:bg-gray-700 rounded-lg text-xs font-bold text-gray-200 transition-all border border-white/5 active:scale-95 group"
        >
          <Clock class="w-3.5 h-3.5 text-yellow-500 group-hover:animate-pulse" />
          <span>MINHAS APOSTAS</span>
        </button>
      </div>
    </div>

    <div v-if="loading" class="text-stake-text animate-pulse text-sm">Carregando menu...</div>

    <div v-else class="flex gap-3 overflow-x-auto pb-4 custom-scrollbar px-1">
      <div 
        v-for="sport in sports" 
        :key="sport.name" 
        @click="goToSport(sport.realKey)" 
        class="min-w-[160px] h-24 rounded-lg relative overflow-hidden cursor-pointer transition-all duration-300 group shadow-lg flex-shrink-0 border border-transparent"
        :class="route.params.id === sport.realKey 
            ? 'ring-2 ring-white scale-105 brightness-110 z-10 shadow-blue-500/20' 
            : 'hover:brightness-110 opacity-80 hover:opacity-100 hover:-translate-y-1'"
      >
        <div :class="`absolute inset-0 bg-gradient-to-br ${sport.color}`"></div>
        <span class="absolute bottom-2 left-3 text-white font-bold uppercase italic text-sm z-10 leading-tight drop-shadow-md">{{ sport.name }}</span>
        <span class="absolute top-2 left-2 bg-black/30 text-white text-[10px] font-bold px-2 py-0.5 rounded backdrop-blur-sm">{{ sport.count }} jogos</span>
        <span class="absolute top-1 right-2 text-3xl opacity-20 transition-all duration-300" :class="route.params.id === sport.realKey ? 'scale-110 opacity-40' : 'group-hover:scale-110 group-hover:opacity-40'">{{ sport.icon }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { height: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: rgba(255, 255, 255, 0.05); border-radius: 10px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: rgba(255, 255, 255, 0.2); border-radius: 10px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: rgba(255, 255, 255, 0.4); }
</style>