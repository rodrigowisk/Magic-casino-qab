<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import SportsService from '../services/SportsService';

// --- PROPS PARA O MODO LIVE (OPCIONAIS) ---
const props = defineProps<{
  isLive?: boolean;            
  liveSports?: Set<string>;    
  liveCounts?: Record<string, number>; 
  selectedSport?: string;      
}>();

const emit = defineEmits(['select']);

// --- CONFIGURAÇÃO ---
const router = useRouter();
const route = useRoute();
const loading = ref(true);
const activeSportsKeys = ref<Set<string>>(new Set());

// 1️⃣ LISTA MESTRA DE ESPORTES (Sem E-Sports/Virtual)
const ALL_SPORTS_CONFIG = [
  { key: 'all',               name: 'Todos',           file: 'all-sports.svg' }, // Exclusivo Live
  { key: 'soccer',            name: 'Futebol',         file: 'soccer.svg' },
  { key: 'basketball',        name: 'Basquete',        file: 'basquet.svg' },
  { key: 'tennis',            name: 'Tênis',           file: 'tenis.svg' },
  { key: 'volleyball',        name: 'Vôlei',           file: 'volley.svg' },
  { key: 'ice-hockey',        name: 'Hóquei',          file: 'hoquei.svg' },
  { key: 'baseball',          name: 'Beisebol',        file: 'baseball.svg' },
  { key: 'american-football', name: 'Fut. Americano',  file: 'fut-america.svg' },
  { key: 'mma',               name: 'MMA',             file: 'mma.svg' },
  { key: 'boxing',            name: 'Boxe',            file: 'boxing.svg' },
  { key: 'darts',             name: 'Dardos',          file: 'dards.svg' },
];

// --- HELPER DE NORMALIZAÇÃO ---
const normalizeKey = (apiKey: string): string => {
    const k = apiKey.toLowerCase();
    
    // Filtros de exclusão
    if (k.includes('esport') || k.includes('e-sport') || k.includes('virtual')) return '';

    if (k.includes('soccer') || k.includes('futebol')) return 'soccer';
    if (k.includes('basket')) return 'basketball';
    if (k.includes('tennis') && !k.includes('table')) return 'tennis';
    if (k.includes('volley')) return 'volleyball';
    if (k.includes('hockey') || k.includes('ice')) return 'ice-hockey';
    if (k.includes('base')) return 'baseball';
    if (k.includes('darts')) return 'darts';
    if (k.includes('football') || k.includes('americano')) return 'american-football';
    if (k.includes('mma') || k.includes('ufc')) return 'mma';
    if (k.includes('boxing') || k.includes('boxe')) return 'boxing';
    
    return k; 
};

// --- COMPUTED: LISTA FINAL ORDENADA ---
const orderedSports = computed(() => {
    const list = ALL_SPORTS_CONFIG.map(sport => {
        let isActive = false;
        let count = 0;

        if (props.isLive) {
            // MODO LIVE
            if (sport.key === 'all') {
                isActive = true; 
                count = props.liveCounts ? Object.values(props.liveCounts).reduce((a, b) => a + b, 0) : 0;
            } else {
                isActive = props.liveSports?.has(sport.key) || false;
                count = props.liveCounts?.[sport.key] || 0;
            }
        } else {
            // MODO PRÉ-JOGO
            if (sport.key === 'all') {
                isActive = false; // 'Todos' nunca ativo no pré-jogo
            } else {
                isActive = !loading.value && activeSportsKeys.value.has(sport.key);
            }
        }
        
        return {
            ...sport,
            isActive,
            count,
            iconPath: `/images/icons/${sport.file}` 
        };
    });

    // Se estiver carregando (apenas pré-jogo), mostra lista vazia para não piscar
    if (loading.value && !props.isLive) return [];

    // Filtra: 
    // Live -> Mostra APENAS ativos.
    // Pré-jogo -> Mostra TODOS (exceto 'all'), inativos ficam cinza.
    let finalList = list;
    if (props.isLive) {
        finalList = list.filter(s => s.isActive);
    } else {
        finalList = list.filter(s => s.key !== 'all');
    }

    // Ordenação
    return finalList.sort((a, b) => {
        if (a.key === 'all') return -1;
        if (b.key === 'all') return 1;

        if (props.isLive) {
            return b.count - a.count;
        }

        // Pré-jogo: Ativos primeiro
        if (a.isActive && !b.isActive) return -1;
        if (!a.isActive && b.isActive) return 1;

        // Futebol prioridade
        if (a.key === 'soccer') return -1;
        if (b.key === 'soccer') return 1;

        return a.name.localeCompare(b.name);
    });
});

const checkIsSelected = (sportKey: string) => {
    if (props.isLive) {
        return props.selectedSport === sportKey;
    }
    return route.params.id === sportKey;
};

// --- LÓGICA DO CARROSSEL ---
const scrollContainer = ref<HTMLElement | null>(null);
let isDown = false;
let startX = 0;
let scrollLeft = 0;
let isDragging = false;

const startDrag = (e: MouseEvent) => {
  isDown = true; isDragging = false;
  if (!scrollContainer.value) return;
  scrollContainer.value.classList.add('active');
  startX = e.pageX - scrollContainer.value.offsetLeft;
  scrollLeft = scrollContainer.value.scrollLeft;
};
const stopDrag = () => {
  isDown = false;
  if (!scrollContainer.value) return;
  scrollContainer.value.classList.remove('active');
  setTimeout(() => { isDragging = false; }, 0);
};
const moveDrag = (e: MouseEvent) => {
  if (!isDown || !scrollContainer.value) return;
  e.preventDefault();
  const x = e.pageX - scrollContainer.value.offsetLeft;
  const walk = (x - startX) * 2;
  if (Math.abs(walk) > 5) isDragging = true;
  scrollContainer.value.scrollLeft = scrollLeft - walk;
};

const handleSportClick = (sport: any) => {
    if (isDragging) return;
    
    // No modo Live, só clica se estiver ativo (sempre verdade pois filtramos)
    // No modo Pré-jogo, só clica se estiver ativo na API
    if (sport.isActive) {
        if (props.isLive) {
            emit('select', sport.key);
        } else {
            router.push({ name: 'sport-events', params: { id: sport.key } });
        }
    }
};

const shouldShowMenu = computed(() => {
  if (props.isLive) return true; 
  
  const isEventDetails = route.name === 'event-details' || route.path.includes('/event/');
  const isLivePage = route.path.includes('/live');
  return !isEventDetails && !isLivePage;
});

// --- API ---
onMounted(async () => {
    if (props.isLive) {
        loading.value = false;
        return;
    }

    try {
        const data = await SportsService.getActiveSports();
        const availableKeys = new Set<string>();
        data.forEach((item: any) => {
            const normalized = normalizeKey(item.key);
            if (normalized) availableKeys.add(normalized);
        });
        activeSportsKeys.value = availableKeys;
    } catch (error) { 
        console.error("Erro menu:", error); 
    } finally { 
        loading.value = false; 
    }
});
</script>

<template>
  <div v-if="shouldShowMenu" class="w-full mb-1 sticky top-0 z-50 select-none overflow-hidden bg-[#0f212e]/95 backdrop-blur-md border-b border-white/5">
    
    <div 
        ref="scrollContainer"
        class="flex items-center justify-start gap-3 px-2 py-1 overflow-x-auto no-scrollbar scroll-smooth cursor-grab active:cursor-grabbing"
        @mousedown="startDrag"
        @mouseleave="stopDrag"
        @mouseup="stopDrag"
        @mousemove="moveDrag"
    >
      
      <div 
        v-for="sport in orderedSports" 
        :key="sport.key"
        @click="handleSportClick(sport)"
        class="group flex flex-col items-center min-w-[60px] relative transition-all duration-300"
        :class="[
            { 'pointer-events-none': isDragging },
            sport.isActive 
                ? 'cursor-pointer' 
                : 'cursor-not-allowed grayscale opacity-70 hover:opacity-90' 
        ]" 
      >
        <div class="relative flex items-center justify-center mb-1.5 transition-all duration-300">
          
             <img 
                :src="sport.iconPath" 
                :alt="sport.name"
                class="w-7 h-7 object-contain transition-all duration-300"
                :class="[
                    checkIsSelected(sport.key)
                        ? 'scale-125 brightness-110 drop-shadow-[0_0_8px_rgba(0,255,127,0.8)]' 
                        : (sport.isActive)
                            ? 'opacity-90 hover:opacity-100 hover:scale-110 hover:drop-shadow-[0_0_5px_rgba(255,255,255,0.3)]'
                            : ''
                ]"
                @error="(e) => (e.target as HTMLImageElement).src = '/images/icons/trophy.svg'"
             />

             <span 
                v-if="isLive && sport.count > 0"
                class="absolute -top-1.5 -right-1.5 bg-red-600 text-white text-[9px] font-bold px-1 rounded-full shadow-sm animate-pulse"
            >
                {{ sport.count }}
            </span>

        </div>

        <span 
          class="text-[9px] font-bold uppercase tracking-wide transition-colors duration-300 whitespace-nowrap"
          :class="[
            checkIsSelected(sport.key)
              ? 'text-[#00FF7F] drop-shadow-sm' 
              : (sport.isActive) 
                  ? 'text-gray-400 group-hover:text-gray-200'
                  : 'text-gray-600'
          ]"
        >
          {{ sport.name }}
        </span>

      </div>

    </div>
  </div>
</template>

<style scoped>
.active { cursor: grabbing; }
.no-scrollbar::-webkit-scrollbar { display: none; }
.no-scrollbar { -ms-overflow-style: none; scrollbar-width: none; }
</style>