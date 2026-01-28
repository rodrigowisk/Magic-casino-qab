<script setup lang="ts">
// ✅ CORREÇÃO: Removido 'ref' que não estava sendo usado
import { Play } from 'lucide-vue-next';

// Imagens REAIS e mais estáveis (Links diretos)
const highlightedGames = [
  { id: 1, home: 'Flamengo', away: 'Vasco', image: 'https://images.unsplash.com/photo-1551958219-acbc608c6377?w=400&q=80', league: 'Carioca', time: '16:00' },
  { id: 2, home: 'Lakers', away: 'Celtics', image: 'https://images.unsplash.com/photo-1518063319789-7217e6706b04?w=400&q=80', league: 'NBA', time: '20:00' },
  { id: 3, home: 'Real Madrid', away: 'Barça', image: 'https://images.unsplash.com/photo-1577223625816-7546f13df25d?w=400&q=80', league: 'La Liga', time: '17:00' },
  { id: 4, home: 'Chiefs', away: '49ers', image: 'https://images.unsplash.com/photo-1566577739112-5180d4bf9390?w=400&q=80', league: 'NFL', time: '22:00' },
  { id: 5, home: 'Djokovic', away: 'Sinner', image: 'https://images.unsplash.com/photo-1622163642998-1ea14b60c57e?w=400&q=80', league: 'Aus. Open', time: '05:00' },
  { id: 6, home: 'Man City', away: 'Arsenal', image: 'https://images.unsplash.com/photo-1574629810360-7efbbe195018?w=400&q=80', league: 'Premier', time: '12:30' },
  { id: 7, home: 'Boca', away: 'River', image: 'https://images.unsplash.com/photo-1518605348435-239902d4bb40?w=400&q=80', league: 'Argentino', time: '19:00' },
  { id: 8, home: 'Warriors', away: 'Suns', image: 'https://images.unsplash.com/photo-1546519638-68e109498ffc?w=400&q=80', league: 'NBA', time: '23:30' },
];

const emit = defineEmits(['select-game']);

// Função de Erro: Se a imagem falhar, coloca um fundo escuro neutro
const handleImageError = (e: Event) => {
  const target = e.target as HTMLImageElement;
  target.src = 'https://images.unsplash.com/photo-1614632537423-1e6c2e7e0aab?q=80&w=600&auto=format&fit=crop';
};
</script>

<template>
  <div class="mt-4">
    <div class="flex items-center justify-between mb-2 px-1">
      <h3 class="text-white font-bold text-sm md:text-base uppercase tracking-wide border-l-4 border-stake-blue pl-2">
        Destaques de Hoje
      </h3>
      <button class="text-[10px] text-gray-400 hover:text-white transition-colors uppercase font-bold">Ver Todos</button>
    </div>

    <div class="flex gap-2 overflow-x-auto pb-2 custom-scrollbar snap-x px-1">
      
      <div 
        v-for="game in highlightedGames" 
        :key="game.id"
        @click="emit('select-game', game.id)"
        class="min-w-[110px] md:min-w-[140px] aspect-square rounded-lg overflow-hidden cursor-pointer group snap-center relative shadow-md border border-white/5 bg-[#1e293b]"
      >
        <img 
          :src="game.image" 
          class="w-full h-full object-cover transition-transform duration-500 group-hover:scale-110 opacity-60 group-hover:opacity-40" 
          alt="Game Bg"
          @error="handleImageError"
        />

        <div class="absolute inset-0 flex flex-col items-center justify-center p-1 text-center z-10">
          
          <span class="text-[8px] font-bold text-stake-blue uppercase mb-0.5 tracking-widest bg-black/60 px-1.5 py-0.5 rounded backdrop-blur-sm truncate max-w-full">
            {{ game.league }}
          </span>

          <div class="flex flex-col items-center leading-none my-0.5 w-full px-1">
            <h4 class="text-white font-black text-[10px] md:text-xs drop-shadow-md truncate w-full">
              {{ game.home }}
            </h4>
            <span class="text-[8px] text-gray-300 font-bold my-0.5">VS</span>
            <h4 class="text-white font-black text-[10px] md:text-xs drop-shadow-md truncate w-full">
              {{ game.away }}
            </h4>
          </div>

          <div class="mt-1 opacity-0 group-hover:opacity-100 transition-opacity duration-300">
            <button class="bg-white text-black p-1 rounded-full hover:scale-110 transition-transform">
              <Play class="w-3 h-3 fill-current" />
            </button>
          </div>
        </div>

        <div class="absolute top-1 right-1 bg-black/70 backdrop-blur text-white text-[8px] font-bold px-1.5 py-0.5 rounded">
          {{ game.time }}
        </div>
      </div>

    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar {
  height: 3px; 
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: transparent; 
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #334155; 
  border-radius: 3px;
}
</style>