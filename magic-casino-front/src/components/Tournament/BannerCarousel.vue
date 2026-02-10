<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { ChevronLeft, ChevronRight } from 'lucide-vue-next';

// --- 1. CARREGAMENTO AUTOMÁTICO DAS IMAGENS ---
const bannerModules = import.meta.glob('/src/assets/banners/*.{png,jpg,jpeg,svg,webp}', { eager: true });

const banners = ref<string[]>([]);
for (const path in bannerModules) {
    const mod = bannerModules[path] as any;
    banners.value.push(mod.default);
}

// --- 2. ESTADOS E LÓGICA ---
const currentIndex = ref(0);
const itemsToShow = ref(1); 
const windowWidth = ref(window.innerWidth);
let autoPlayInterval: any = null;

// LÓGICA RESPONSIVA ATUALIZADA
const updateLayout = () => {
    windowWidth.value = window.innerWidth;
    if (window.innerWidth < 640) {
        itemsToShow.value = 1; // Mobile: 1 por vez
    } else if (window.innerWidth < 1024) {
        itemsToShow.value = 2; // Tablet: 2 por vez
    } else {
        itemsToShow.value = 4; // Desktop: 4 por vez
    }
};

// --- 3. LÓGICA DE ROTAÇÃO INFINITA ---
const visibleBanners = computed(() => {
    if (banners.value.length === 0) return [];
    
    const list = [];
    for (let i = 0; i < itemsToShow.value; i++) {
        const index = (currentIndex.value + i) % banners.value.length;
        list.push(banners.value[index]);
    }
    return list;
});

const nextSlide = () => {
    currentIndex.value = (currentIndex.value + 1) % banners.value.length;
};

const prevSlide = () => {
    currentIndex.value = (currentIndex.value - 1 + banners.value.length) % banners.value.length;
};

// --- 4. CICLO DE VIDA ---
onMounted(() => {
    updateLayout();
    window.addEventListener('resize', updateLayout);
    autoPlayInterval = setInterval(nextSlide, 5000);
});

onUnmounted(() => {
    window.removeEventListener('resize', updateLayout);
    if (autoPlayInterval) clearInterval(autoPlayInterval);
});
</script>

<template>
  <div v-if="banners.length > 0" class="relative w-full mb-1 group/banner">
    
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 overflow-hidden">
        
        <transition-group name="fade-slide">
            <div 
                v-for="(bannerSrc, idx) in visibleBanners" 
                :key="`${currentIndex}-${idx}`"
                class="relative rounded-lg overflow-hidden shadow-lg border border-white/10 bg-[#1f1f1f] aspect-banner cursor-pointer hover:border-white/30 transition-colors"
            >
                <img 
                    :src="bannerSrc" 
                    alt="Promo Banner" 
                    class="w-full h-full object-cover transform hover:scale-105 transition-transform duration-700"
                />
                
                <div class="absolute inset-0 bg-gradient-to-t from-black/80 via-transparent to-white/5 pointer-events-none"></div>
            </div>
        </transition-group>

    </div>

    <button @click.stop="prevSlide" class="absolute left-2 top-1/2 -translate-y-1/2 bg-black/60 hover:bg-red-600 text-white p-2 rounded-full backdrop-blur-sm transition-all opacity-0 group-hover/banner:opacity-100 z-20 shadow-xl border border-white/10">
        <ChevronLeft class="w-5 h-5" />
    </button>
    <button @click.stop="nextSlide" class="absolute right-2 top-1/2 -translate-y-1/2 bg-black/60 hover:bg-red-600 text-white p-2 rounded-full backdrop-blur-sm transition-all opacity-0 group-hover/banner:opacity-100 z-20 shadow-xl border border-white/10">
        <ChevronRight class="w-5 h-5" />
    </button>

    <div class="md:hidden absolute bottom-3 left-1/2 -translate-x-1/2 flex gap-1.5 z-20">
        <span 
            v-for="(_, idx) in banners" 
            :key="idx"
            class="h-1.5 rounded-full transition-all duration-300 shadow-sm"
            :class="currentIndex === idx ? 'bg-red-600 w-4' : 'bg-white/30 w-1.5'"
        ></span>
    </div>

  </div>

  <div v-else class="h-32 w-full flex items-center justify-center border border-dashed border-gray-700 rounded text-gray-500 text-xs mb-6">
    Adicione imagens em /assets/banners
  </div>
</template>

<style scoped>
.aspect-banner {
    height: 140px; 
}

@media (min-width: 1024px) {
    .aspect-banner {
        height: 130px; 
    }
}

/* --- CORREÇÃO DO LAYOUT (IMPEDE DUPLICAÇÃO DE LINHA) --- */

.fade-slide-enter-active {
  transition: all 0.5s ease;
}

/* O Segredo: Remove imediatamente o banner antigo do fluxo do grid */
.fade-slide-leave-active {
  display: none; 
}

.fade-slide-enter-from {
  opacity: 0;
  transform: translateX(10px);
}

.fade-slide-leave-to {
  opacity: 0;
}
</style>