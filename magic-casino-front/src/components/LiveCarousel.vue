<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { Trophy, ChevronRight, Loader2, Play, CircleDot } from 'lucide-vue-next';
import SportsService from '../services/SportsService';

const router = useRouter();
const liveGames = ref<any[]>([]);
const loading = ref(true);

// Imagens de Fundo
const sportImages = [
  'https://images.unsplash.com/photo-1529900748604-07564a03e7a6?w=400&q=80',
  'https://images.unsplash.com/photo-1574629810360-7efbbe195018?w=400&q=80',
  'https://images.unsplash.com/photo-1560272564-c83b66b1ad12?w=400&q=80',
  'https://images.unsplash.com/photo-1518605348435-239902d4bb40?w=400&q=80',
  'https://images.unsplash.com/photo-1551958219-acbc608c6377?w=400&q=80',
  'https://images.unsplash.com/photo-1517466787929-bc90951d0974?w=400&q=80',
];

const getRandomImage = (index: number) => sportImages[index % sportImages.length];

// ✅ FUNÇÃO DE CORREÇÃO AUTOMÁTICA
const handleImageError = (e: Event) => {
  const target = e.target as HTMLImageElement;
  // Substitui por uma textura escura neutra se falhar
  target.src = 'https://images.unsplash.com/photo-1614632537423-1e6c2e7e0aab?q=80&w=600&auto=format&fit=crop';
};

const fetchLiveGames = async () => {
  try {
    const events = await SportsService.getEvents('soccer', 1, 50);
    if (events && Array.isArray(events)) {
      const now = new Date();
      liveGames.value = events.filter((e: any) => {
        const startDate = new Date(e.commenceTime);
        return startDate <= now && startDate > new Date(now.getTime() - 130 * 60000); 
      }).slice(0, 10);

      if (liveGames.value.length === 0) liveGames.value = events.slice(0, 10);
    }
  } catch (error) {
    console.error("Erro ao carregar jogos ao vivo", error);
  } finally {
    loading.value = false;
  }
};

const getEventId = (item: any) => item.externalId || item.ExternalId || item.id || item.Id || item.eventId || item.gameId;

const goToEvent = (game: any) => {
  const id = getEventId(game);
  if(id) router.push(`/event/${id}`);
};

const getGameTime = (startDateStr: string) => {
  const start = new Date(startDateStr);
  const now = new Date();
  const diff = Math.floor((now.getTime() - start.getTime()) / 60000);
  if (diff < 0) return 'Live'; 
  if (diff > 90) return '90+'; 
  return `${diff}'`; 
};

onMounted(fetchLiveGames);
</script>

<template>
  <div class="mt-4 relative">
    <div class="flex items-center justify-between mb-2 px-1">
      <div class="flex items-center gap-2">
        <div class="relative flex h-2.5 w-2.5">
          <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75"></span>
          <span class="relative inline-flex rounded-full h-2.5 w-2.5 bg-red-500"></span>
        </div>
        <h3 class="text-white font-bold text-sm md:text-base uppercase tracking-wide border-l-4 border-red-500 pl-2">
          Ao Vivo Agora
        </h3>
      </div>
      <button class="text-[10px] text-gray-400 hover:text-white transition-colors uppercase font-bold flex items-center gap-1">
        Ver Todos <ChevronRight class="w-3 h-3" />
      </button>
    </div>

    <div v-if="loading" class="flex justify-center py-4">
      <Loader2 class="w-6 h-6 text-stake-blue animate-spin" />
    </div>

    <div v-else-if="liveGames.length === 0" class="text-center py-6 text-gray-500 bg-[#1e293b]/50 rounded-lg border border-white/5">
      <p class="text-xs">Nenhum jogo ao vivo.</p>
    </div>

    <div v-else class="flex gap-2 overflow-x-auto pb-2 custom-scrollbar snap-x px-1">
      <div 
        v-for="(game, index) in liveGames" 
        :key="getEventId(game)"
        @click="goToEvent(game)"
        class="min-w-[110px] md:min-w-[140px] aspect-square rounded-lg overflow-hidden cursor-pointer group snap-center relative shadow-md border border-white/10 bg-[#1e293b]"
      >
        <img 
          :src="getRandomImage(index)" 
          class="w-full h-full object-cover transition-transform duration-500 group-hover:scale-110 opacity-50 group-hover:opacity-40" 
          alt="Game Bg"
          @error="handleImageError"
        />

        <div class="absolute inset-0 flex flex-col items-center justify-center p-2 text-center z-10">
          <div class="absolute top-1 right-1 flex items-center gap-0.5 bg-red-600/90 backdrop-blur text-white text-[8px] font-black px-1 py-0.5 rounded shadow-sm animate-pulse">
            <CircleDot class="w-1.5 h-1.5 fill-current" />
            <span>{{ getGameTime(game.commenceTime) }}</span>
          </div>
          <span class="text-[8px] font-bold text-white/80 uppercase mb-1 tracking-widest bg-black/40 px-1.5 py-0.5 rounded backdrop-blur-sm truncate max-w-full">
            {{ game.league || 'Liga' }}
          </span>
          <div class="flex flex-col items-center gap-0.5 w-full">
            <div class="flex items-center justify-between w-full gap-1">
              <h4 class="text-white font-black text-[10px] leading-tight drop-shadow-md truncate text-right flex-1">{{ game.homeTeam }}</h4>
              <span class="text-yellow-400 font-mono font-bold text-[10px] bg-black/50 px-1 rounded backdrop-blur-sm">{{ Math.floor(Math.random() * 3) }}</span>
            </div>
            <span class="text-[8px] text-gray-300 font-bold uppercase">VS</span>
            <div class="flex items-center justify-between w-full gap-1">
              <span class="text-yellow-400 font-mono font-bold text-[10px] bg-black/50 px-1 rounded backdrop-blur-sm">{{ Math.floor(Math.random() * 2) }}</span>
              <h4 class="text-white font-black text-[10px] leading-tight drop-shadow-md truncate text-left flex-1">{{ game.awayTeam }}</h4>
            </div>
          </div>
          <div class="mt-1 opacity-0 group-hover:opacity-100 transition-opacity duration-300">
            <button class="bg-stake-blue text-white p-1 rounded-full hover:scale-110 transition-transform shadow-md">
              <Play class="w-3 h-3 fill-current" />
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { height: 3px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 3px; }
</style>