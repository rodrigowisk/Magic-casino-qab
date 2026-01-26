<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import SportsService from '../services/SportsService';

// --- PROPS ---
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

// Estado de carregamento
const loadingAdmin = ref(true); 
const loadingApi = ref(true);   

// Sets de controle
const adminVisibleSports = ref<Set<string>>(new Set()); 
const apiActiveSports = ref<Set<string>>(new Set());    

// 1️⃣ LISTA MESTRA (Mapeamento de ícones e nomes)
const ALL_SPORTS_CONFIG = [
  { key: 'all',               name: 'Todos',          file: 'all-sports.svg' },
  { key: 'soccer',            name: 'Futebol',        file: 'soccer.svg' },
  { key: 'basketball',        name: 'Basquete',       file: 'basquet.svg' },
  { key: 'tennis',            name: 'Tênis',          file: 'tenis.svg' },
  { key: 'volleyball',        name: 'Vôlei',          file: 'volley.svg' },
  { key: 'ice-hockey',        name: 'Hóquei',         file: 'hoquei.svg' },
  { key: 'baseball',          name: 'Beisebol',       file: 'baseball.svg' },
  { key: 'american-football', name: 'Fut. Americano', file: 'fut-america.svg' },
  { key: 'mma',               name: 'MMA',            file: 'mma.svg' },
  { key: 'boxing',            name: 'Boxe',           file: 'boxing.svg' },
  { key: 'darts',             name: 'Dardos',         file: 'dards.svg' },
];

// --- HELPER DE NORMALIZAÇÃO ---
const normalizeKey = (apiKey: string): string => {
    const k = apiKey.toLowerCase();
    if (k.includes('esport') || k.includes('virtual')) return '';
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

// --- COMPUTED: LISTA FINAL ---
const orderedSports = computed(() => {
    if (loadingAdmin.value && !props.isLive) return [];

    const list = ALL_SPORTS_CONFIG.map(sport => {
        let isVisibleInAdmin = false;
        let hasActiveGames = false;
        let count = 0;

        if (props.isLive) {
            // Lógica Live: Sempre mostra "Todos", os outros dependem de ter jogos
            if (sport.key === 'all') {
                isVisibleInAdmin = true;
                hasActiveGames = true; 
                count = props.liveCounts ? Object.values(props.liveCounts).reduce((a, b) => a + b, 0) : 0;
            } else {
                isVisibleInAdmin = true; 
                hasActiveGames = props.liveSports?.has(sport.key) || false;
                count = props.liveCounts?.[sport.key] || 0;
            }
        } else {
            // Lógica Pré-Jogo
            if (sport.key === 'all') {
                isVisibleInAdmin = false; 
            } else {
                isVisibleInAdmin = adminVisibleSports.value.has(sport.key);
                hasActiveGames = !loadingApi.value && apiActiveSports.value.has(sport.key);
            }
        }
        
        return {
            ...sport,
            isVisible: isVisibleInAdmin,
            isActive: hasActiveGames,
            count,
            iconPath: `/images/icons/${sport.file}` 
        };
    });

    let finalList = list.filter(s => {
        if (props.isLive) return s.isActive; 
        return s.isVisible;                  
    });

    return finalList.sort((a, b) => {
        if (a.key === 'all') return -1;
        if (b.key === 'all') return 1;
        if (props.isLive) return b.count - a.count;
        if (a.isActive && !b.isActive) return -1;
        if (!a.isActive && b.isActive) return 1;
        if (a.key === 'soccer') return -1;
        if (b.key === 'soccer') return 1;
        return a.name.localeCompare(b.name);
    });
});

const checkIsSelected = (sportKey: string) => {
    if (props.isLive) return props.selectedSport === sportKey;
    return route.params.id === sportKey;
};

// --- INTERAÇÃO (Drag & Drop) ---
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
    if (sport.isActive) {
        if (props.isLive) {
            emit('select', sport.key);
        } else {
            router.push({ name: 'sport-events', params: { id: sport.key } });
        }
    }
};

const shouldShowMenu = computed(() => {
  // Ajuste aqui: Se isLive for true via prop, sempre mostra.
  if (props.isLive) return true; 

  const isEventDetails = route.name === 'event-details' || route.path.includes('/event/');
  // Removemos a verificação do route.path.includes('/live') aqui, pois se o componente
  // for inserido manualmente na página Live, ele deve aparecer.
  return !isEventDetails;
});

// --- API FETCHING ---
onMounted(async () => {
    if (props.isLive) {
        loadingAdmin.value = false;
        loadingApi.value = false;
        return;
    }

    try {
        const adminData = await SportsService.getAdminConfig(); 
        const enabledSet = new Set<string>();
        const list = Array.isArray(adminData) ? adminData : [];

        list.forEach((item: any) => {
            const isEnabled = item.isActive === true || item.IsActive === true;
            const sportKey = item.key || item.Key;
            if (isEnabled && sportKey) {
                const normalized = normalizeKey(sportKey);
                if (normalized) enabledSet.add(normalized);
            }
        });
        adminVisibleSports.value = enabledSet;
    } catch (err) {
        adminVisibleSports.value = new Set(['soccer']); 
    } finally {
        loadingAdmin.value = false;
    }

    try {
        const data = await SportsService.getActiveSports(); 
        const availableKeys = new Set<string>();
        data.forEach((item: any) => {
            const hasGames = item.count && Number(item.count) > 0;
            if (hasGames) {
                const normalized = normalizeKey(item.key || item.name);
                if (normalized) availableKeys.add(normalized);
            }
        });
        apiActiveSports.value = availableKeys;
    } catch (error) { 
        console.error("Erro active sports:", error); 
    } finally { 
        loadingApi.value = false;
    }
});
</script>

<template>
  <div v-if="shouldShowMenu" class="w-full sticky top-0 z-40 select-none overflow-hidden bg-[#0f212e] border-b border-white/5 shadow-md h-[72px]">
    
    <div 
        ref="scrollContainer"
        class="flex items-center justify-start gap-2 px-4 h-full overflow-x-auto no-scrollbar scroll-smooth cursor-grab active:cursor-grabbing"
        @mousedown="startDrag"
        @mouseleave="stopDrag"
        @mouseup="stopDrag"
        @mousemove="moveDrag"
    >
      
      <div v-if="loadingAdmin && !isLive" class="flex gap-3">
         <div v-for="i in 5" :key="i" class="w-[60px] h-[50px] bg-white/5 animate-pulse rounded-md"></div>
      </div>

      <div 
        v-else
        v-for="sport in orderedSports" 
        :key="sport.key"
        @click="handleSportClick(sport)"
        class="group flex flex-col items-center justify-center min-w-[68px] h-[60px] relative transition-all duration-200 hover:bg-white/5 rounded-lg"
        :class="[
            { 'pointer-events-none': isDragging },
            sport.isActive ? 'cursor-pointer' : 'cursor-not-allowed opacity-50'
        ]" 
      >
        <div class="relative flex items-center justify-center mb-1 transition-all duration-300">
          
             <img 
                :src="sport.iconPath" 
                :alt="sport.name"
                class="w-6 h-6 object-contain transition-all duration-300 transform"
                :class="[
                    checkIsSelected(sport.key)
                        ? 'scale-110 brightness-110 drop-shadow-[0_0_8px_rgba(0,255,127,0.6)]' 
                        : (sport.isActive)
                            ? 'opacity-70 group-hover:scale-110 group-hover:opacity-100'
                            : 'grayscale opacity-30 contrast-50'
                ]"
                @error="(e) => (e.target as HTMLImageElement).src = '/images/icons/trophy.svg'"
             />

             <span 
                v-if="isLive && sport.count > 0"
                class="absolute -top-2 -right-2 bg-red-600 text-white text-[9px] font-bold px-1.5 py-0.5 rounded-full shadow-sm min-w-[16px] text-center border border-[#0f212e]"
            >
                {{ sport.count }}
            </span>

        </div>

        <span 
          class="text-[10px] font-bold uppercase tracking-wide transition-colors duration-300 whitespace-nowrap mt-1"
          :class="[
            checkIsSelected(sport.key)
              ? 'text-[#00FF7F]' 
              : 'text-gray-400 group-hover:text-white'
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