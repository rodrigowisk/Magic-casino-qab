<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { ChevronLeft, ChevronRight } from 'lucide-vue-next';

// Dados simulados dos Banners com IMAGENS REAIS QUE FUNCIONAM
const slides = [
  {
    id: 1,
    // Estádio de Futebol (Substituindo o arquivo local quebrado)
    image: 'https://images.unsplash.com/photo-1522778119026-d647f0565c6a?q=80&w=1600&auto=format&fit=crop', 
    title: 'BÔNUS DE BOAS-VINDAS',
    subtitle: 'Ganhe 100% até R$ 500 no primeiro depósito',
    buttonText: 'RESGATAR AGORA',
    color: 'text-yellow-400'
  },
  {
    id: 2,
    // Basquete / NBA
    image: 'https://images.unsplash.com/photo-1546519638-68e109498ffc?q=80&w=1600&auto=format&fit=crop',
    title: 'FINAIS DA NBA',
    subtitle: 'Aposte ao vivo com as melhores odds do mercado',
    buttonText: 'VER JOGOS',
    color: 'text-blue-400'
  },
  {
    id: 3,
    // Futebol em ação
    image: 'https://images.unsplash.com/photo-1489944440615-453fc2b6a9a9?q=80&w=1600&auto=format&fit=crop',
    title: 'BRASILEIRÃO SÉRIE A',
    subtitle: 'Cubra todos os lances do campeonato',
    buttonText: 'APOSTAR',
    color: 'text-green-400'
  }
];

const currentSlide = ref(0);
let intervalId: any = null;

const nextSlide = () => {
  currentSlide.value = (currentSlide.value + 1) % slides.length;
};

const prevSlide = () => {
  currentSlide.value = (currentSlide.value - 1 + slides.length) % slides.length;
};

// Auto-play
onMounted(() => {
  intervalId = setInterval(nextSlide, 5000); 
});

onUnmounted(() => {
  clearInterval(intervalId);
});
</script>

<template>
  <div class="relative w-full h-[180px] md:h-[220px] rounded-xl overflow-hidden group shadow-lg border border-white/5">
    
    <div 
      class="absolute inset-0 transition-transform duration-700 ease-in-out flex"
      :style="{ transform: `translateX(-${currentSlide * 100}%)` }"
    >
      <div v-for="slide in slides" :key="slide.id" class="w-full h-full flex-shrink-0 relative">
        <img :src="slide.image" class="w-full h-full object-cover" alt="Banner" />
        
        <div class="absolute inset-0 bg-gradient-to-r from-black/90 via-black/60 to-transparent"></div>

        <div class="absolute inset-0 flex flex-col justify-center px-5 md:px-10 max-w-lg">
          <h2 class="text-xl md:text-3xl font-black uppercase italic tracking-tighter mb-1 drop-shadow-lg leading-tight" :class="slide.color">
            {{ slide.title }}
          </h2>
          <p class="text-white/80 text-[10px] md:text-xs font-medium mb-3 drop-shadow-md max-w-[80%]">
            {{ slide.subtitle }}
          </p>
          <button class="bg-blue-600 hover:bg-blue-500 text-white font-bold text-[10px] md:text-xs px-5 py-2 rounded w-fit transition-all hover:scale-105 shadow-md shadow-blue-900/30">
            {{ slide.buttonText }}
          </button>
        </div>
      </div>
    </div>

    <button @click.stop="prevSlide" class="absolute left-2 top-1/2 -translate-y-1/2 bg-black/30 hover:bg-black/60 text-white p-1 rounded-full opacity-0 group-hover:opacity-100 transition-all">
      <ChevronLeft class="w-4 h-4" />
    </button>
    <button @click.stop="nextSlide" class="absolute right-2 top-1/2 -translate-y-1/2 bg-black/30 hover:bg-black/60 text-white p-1 rounded-full opacity-0 group-hover:opacity-100 transition-all">
      <ChevronRight class="w-4 h-4" />
    </button>

    <div class="absolute bottom-2 left-1/2 -translate-x-1/2 flex gap-1">
      <button 
        v-for="(slide, index) in slides" 
        :key="index"
        @click="currentSlide = index"
        class="w-1 h-1 rounded-full transition-all"
        :class="currentSlide === index ? 'bg-white w-3' : 'bg-white/40 hover:bg-white/60'"
      ></button>
    </div>
  </div>
</template>