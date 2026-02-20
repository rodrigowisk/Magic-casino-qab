<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { ChevronRight, ChevronLeft } from 'lucide-vue-next';
import TournamentCard from './TournamentCard.vue';

// --- PROPS ---
const props = defineProps<{
    title: string;
    tournaments: any[];
    processingId: number | null;
    viewAllType?: string;
}>();

// --- EMITS ---
const emit = defineEmits(['join', 'enter', 'share', 'favorite']);

// Verifica se é o carrossel de Destaques
const isFeatured = computed(() => props.viewAllType === 'featured');

// =======================================================================
// LÓGICA 1: CARROSSEL DE DESTAQUES (SCROLL CONTÍNUO / MARQUEE)
// =======================================================================

// Pega TODOS os torneios enviados para o componente (sem cortar)
const baseMarqueeList = computed(() => {
    let list = [...props.tournaments];
    if (list.length === 0) return [];
    
    // Se tiver poucos destaques, a gente repete eles até ter pelo menos 8 cards para preencher a tela
    while (list.length < 8) {
        list = [...list, ...props.tournaments];
    }
    return list;
});

// A mágica do Loop Infinito: Criamos exatamente duas metades iguais.
const marqueeTournaments = computed(() => {
    const base = baseMarqueeList.value;
    if (base.length === 0) return [];
    
    return [
        ...base.map((t, idx) => ({ ...t, tempKey: `set1_${t.id}_${idx}` })),
        ...base.map((t, idx) => ({ ...t, tempKey: `set2_${t.id}_${idx}` }))
    ];
});

// 👇 A MATEMÁTICA DA VELOCIDADE PERFEITA 👇
// Calcula 4 segundos para cada card na lista base. 
// Ex: 10 cards = 40s. 100 cards = 400s. A velocidade visual fica sempre igual!
const marqueeDuration = computed(() => {
    const time = baseMarqueeList.value.length * 4;
    return `${time}s`;
});

// =======================================================================
// LÓGICA 2: CARROSSEL NORMAL (COM SETAS E SWIPE)
// =======================================================================
const carouselIndex = ref(0); 
const itemsPerPage = ref(6);

const visibleTournaments = computed(() => {
    const list = props.tournaments; 
    const len = list.length;
    if (len === 0) return [];
    
    if (len <= itemsPerPage.value) return list.map((t, i) => ({ ...t, tempKey: `norm_${t.id}_${i}` }));

    const result = [];
    for (let i = 0; i < itemsPerPage.value + 2; i++) { 
        const index = (carouselIndex.value + i) % len;
        if (list[index]) {
            result.push({ ...list[index], tempKey: `${list[index].id}_${i}_${carouselIndex.value}` }); 
        }
    }
    return result;
});

const navigateCarousel = (direction: 'prev' | 'next') => {
    const len = props.tournaments.length;
    if (len === 0) return;
    
    if (direction === 'next') {
        carouselIndex.value = (carouselIndex.value + 1) % len;
    } else {
        carouselIndex.value = (carouselIndex.value - 1 + len) % len;
    }
};

const updateItemsPerPage = () => {
    const w = window.innerWidth;
    if (w < 640) itemsPerPage.value = 2;
    else if (w < 768) itemsPerPage.value = 3;
    else if (w < 1280) itemsPerPage.value = 4;
    else itemsPerPage.value = 6;
};

// --- LÓGICA DE SWIPE (Apenas pro Normal) ---
const touchStartX = ref(0);
const touchEndX = ref(0);

const handleTouchStart = (e: TouchEvent) => {
    if (isFeatured.value) return; 
    if (!e.changedTouches || e.changedTouches.length === 0) return;
    
    const touch = e.changedTouches[0];
    if (touch) {
        touchStartX.value = touch.screenX;
    }
};

const handleTouchEnd = (e: TouchEvent) => {
    if (isFeatured.value) return; 
    if (!e.changedTouches || e.changedTouches.length === 0) return;

    const touch = e.changedTouches[0];
    if (touch) {
        touchEndX.value = touch.screenX;
        const diff = touchStartX.value - touchEndX.value;
        
        if (diff > 50) navigateCarousel('next');
        else if (diff < -50) navigateCarousel('prev');
    }
};

onMounted(() => {
    updateItemsPerPage();
    window.addEventListener('resize', updateItemsPerPage);
});

onUnmounted(() => {
    window.removeEventListener('resize', updateItemsPerPage);
});
</script>

<template>
    <div class="w-full relative py-2 overflow-hidden">
        
        <div class="mb-2 flex items-center justify-between px-2 w-full">
            <div class="flex items-center gap-2">
                <h2 class="text-sm md:text-base font-bold text-white tracking-wide border-l-4 border-blue-600 pl-2">{{ title }}</h2>
            </div>

            <router-link 
                v-if="viewAllType" 
                :to="{ name: 'TournamentList', params: { type: viewAllType } }"
                class="flex items-center gap-1 text-[10px] md:text-xs font-bold text-blue-400 hover:text-white transition-colors uppercase tracking-widest opacity-90 hover:opacity-100 py-1 px-2 rounded hover:bg-white/5 z-10"
            >
                Ver Todos
                <ChevronRight class="w-3 h-3" />
            </router-link>
        </div>

        <div v-if="tournaments.length === 0" class="h-[260px] md:h-[330px] w-full flex items-center justify-center text-xs text-gray-500 border border-dashed border-gray-800 rounded-lg mt-1 bg-white/5 mx-2">
            <p>Nenhum torneio disponível nesta categoria.</p>
        </div>

        <template v-else>
            
            <div v-if="isFeatured" class="marquee-wrapper relative w-[100vw] min-h-[260px] md:min-h-[350px] -ml-3 md:ml-0 overflow-hidden">
                <div class="absolute inset-y-0 left-0 w-8 md:w-16 bg-gradient-to-r from-[#0f172a] to-transparent z-10 pointer-events-none"></div>
                <div class="absolute inset-y-0 right-0 w-8 md:w-16 bg-gradient-to-l from-[#0f172a] to-transparent z-10 pointer-events-none"></div>

                <div class="animate-marquee flex gap-3 px-2 pb-4 pt-1 w-max"
                     :style="{ animationDuration: marqueeDuration }">
                    <div 
                        v-for="t in marqueeTournaments" 
                        :key="t.tempKey"
                        class="shrink-0 w-[45vw] md:w-auto transform transition-transform duration-300"
                    >
                        <TournamentCard 
                            :tournament="t"
                            :processing-id="processingId"
                            @join="emit('join', $event)"
                            @enter="emit('enter', $event)"
                            @share="emit('share', $event)"
                            @favorite="emit('favorite', $event)"
                        />
                    </div>
                </div>
            </div>

            <div v-else class="carousel-container relative group/carousel min-h-[260px] md:min-h-[350px]"
                 @touchstart="handleTouchStart"
                 @touchend="handleTouchEnd">
                
                <button v-if="tournaments.length > itemsPerPage" @click="navigateCarousel('prev')" 
                        class="hidden md:flex absolute -left-3 top-1/2 -translate-y-1/2 z-40 w-10 h-10 bg-black/60 hover:bg-black rounded-full items-center justify-center opacity-0 group-hover/carousel:opacity-100 transition-all duration-300 cursor-pointer shadow-lg border border-white/10 backdrop-blur-sm hover:scale-110">
                    <ChevronLeft class="w-6 h-6 text-white" />
                </button>

                <div class="slider flex gap-3 overflow-hidden px-2 pb-4 pt-1 w-full">
                    <div 
                        v-for="t in visibleTournaments" 
                        :key="t.tempKey"
                        class="shrink-0 w-[40vw] md:w-auto"
                    >
                        <TournamentCard 
                            :tournament="t"
                            :processing-id="processingId"
                            @join="emit('join', $event)"
                            @enter="emit('enter', $event)"
                            @share="emit('share', $event)"
                            @favorite="emit('favorite', $event)"
                        />
                    </div>
                </div>

                <button v-if="tournaments.length > itemsPerPage" @click="navigateCarousel('next')" 
                        class="hidden md:flex absolute -right-3 top-1/2 -translate-y-1/2 z-40 w-10 h-10 bg-black/60 hover:bg-black rounded-full items-center justify-center opacity-0 group-hover/carousel:opacity-100 transition-all duration-300 cursor-pointer shadow-lg border border-white/10 backdrop-blur-sm hover:scale-110">
                    <ChevronRight class="w-6 h-6 text-white" />
                </button>
            </div>
        </template>
    </div>
</template>

<style scoped>
.carousel-container { 
    position: relative; 
    max-width: 100vw; 
    overflow: hidden; 
}

.slider { 
    display: flex; 
    will-change: transform; 
}

/* ======================================= */
/* ESTILOS DA ANIMAÇÃO DE VÍDEO (MARQUEE)  */
/* ======================================= */
.marquee-wrapper {
    max-width: 100vw;
}

.animate-marquee {
    /* O tempo "40s" fixo foi removido daqui e passado para o JavaScript! */
    animation-name: marquee;
    animation-timing-function: linear;
    animation-iteration-count: infinite;
    will-change: transform;
}

.animate-marquee:hover {
    animation-play-state: paused;
}

@keyframes marquee {
    0% {
        transform: translateX(0%);
    }
    100% {
        transform: translateX(-50%);
    }
}
</style>