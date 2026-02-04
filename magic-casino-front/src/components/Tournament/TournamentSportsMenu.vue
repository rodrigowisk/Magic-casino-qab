<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { CalendarDays } from 'lucide-vue-next';
import FiltroDatas from '../FiltroDatas.vue'; 

// --- PROPS ---
const props = withDefaults(defineProps<{
  games?: any[]; 
  activeMode: 'prematch' | 'live';
  selectedSport: string;    
  selectedDate: string;
}>(), {
  games: () => []
});

// --- EMITS ---
const emit = defineEmits<{
  (e: 'update:activeMode', val: 'prematch' | 'live'): void;
  (e: 'update:selectedSport', val: string): void;
  (e: 'update:selectedDate', val: string): void;
}>();

// --- CONFIGURAÇÃO DE ÍCONES ---
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

const normalizeKey = (apiKey: string): string => {
    if (!apiKey) return 'all';
    const k = String(apiKey).toLowerCase();
    if (k.includes('soccer') || k.includes('futebol')) return 'soccer';
    if (k.includes('basket')) return 'basketball';
    if (k.includes('tennis')) return 'tennis';
    if (k.includes('volley')) return 'volleyball';
    if (k.includes('hockey')) return 'ice-hockey';
    return 'all'; 
};

// --- LÓGICA INTELIGENTE: FILTRO & CONTAGEM ---
const availableSports = computed(() => {
    const counts: Record<string, number> = { all: 0 };
    const sportsFound = new Set<string>();
    
    const rawGames = props.games || []; 
    const safeGames = Array.isArray(rawGames) ? rawGames : [];

    safeGames.forEach((g: any) => {
        if (!g) return;
        const k1 = g.sportKey;
        const k2 = g.Sport;
        const k3 = g.sport;
        const k4 = g.SportKey;
        const sportRaw = k1 || k2 || k3 || k4 || 'soccer';
        const key = normalizeKey(String(sportRaw));
        
        sportsFound.add(key);
        
        const currentCount = counts[key] ?? 0;
        counts[key] = currentCount + 1;
        
        const currentAll = counts['all'] ?? 0;
        counts['all'] = currentAll + 1;
    });

    let result = ALL_SPORTS_CONFIG
        .filter(s => s.key === 'all' || sportsFound.has(s.key))
        .map(s => ({
            ...s,
            count: counts[s.key] ?? 0
        }));

    if (sportsFound.size === 1) {
        result = result.filter(s => s.key !== 'all');
    }

    return result;
});

watch(availableSports, (newList) => {
    if (newList.length === 1 && newList[0]) {
        const uniqueSport = newList[0].key;
        if (props.selectedSport !== uniqueSport) {
            emit('update:selectedSport', uniqueSport);
        }
    }
}, { immediate: true });

// --- INTERAÇÃO DE DRAG ---
const scrollContainer = ref<HTMLElement | null>(null);
let isDown = false;
let startX = 0;
let scrollLeft = 0;
let isDragging = false;

const startDrag = (e: MouseEvent) => {
  isDown = true; isDragging = false;
  if (!scrollContainer.value) return;
  startX = e.pageX - scrollContainer.value.offsetLeft;
  scrollLeft = scrollContainer.value.scrollLeft;
};
const stopDrag = () => {
  isDown = false;
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

const selectSport = (key: string) => {
    if (isDragging) return;
    emit('update:selectedSport', key);
};
</script>

<template>
  <div class="w-full bg-[#0f172a]/50 backdrop-blur border-b border-white/5 relative z-30">
    <div class="max-w-[1200px] mx-auto px-2 md:px-4 h-[72px] grid grid-cols-[1fr_auto_1fr] items-center gap-4">

        <div class="min-w-0 h-full flex items-center justify-start relative overflow-visible">
            <div 
                ref="scrollContainer"
                class="flex items-center gap-4 overflow-x-auto no-scrollbar scroll-smooth cursor-grab active:cursor-grabbing h-full px-2 mask-fade-right w-full py-2"
                @mousedown="startDrag"
                @mouseleave="stopDrag"
                @mouseup="stopDrag"
                @mousemove="moveDrag"
            >
                <div 
                    v-for="sport in availableSports" 
                    :key="sport.key"
                    @click="selectSport(sport.key)"
                    class="group flex flex-col items-center justify-center min-w-[50px] h-[50px] rounded-lg transition-all duration-300 relative flex-shrink-0 cursor-pointer"
                    :class="[
                        { 'pointer-events-none': isDragging },
                        selectedSport === sport.key 
                            ? 'scale-110 z-10'  // Aumenta e traz para frente
                            : 'hover:bg-white/5 opacity-60 hover:opacity-100'
                    ]"
                >
                    <div class="relative">
                        <img 
                            :src="`/images/icons/${sport.file}`" 
                            :alt="sport.name"
                            class="w-6 h-6 object-contain transition-all duration-300"
                            :class="selectedSport === sport.key 
                                ? 'brightness-125 drop-shadow-[0_0_10px_rgba(0,255,127,0.5)]' 
                                : 'grayscale group-hover:grayscale-0'"
                            @error="(e) => (e.target as HTMLImageElement).src = '/images/icons/all-sports.svg'"
                        />
                        <span 
                            v-if="sport.count > 0"
                            class="absolute -top-2 -right-3 min-w-[14px] h-[14px] flex items-center justify-center bg-[#1e293b] text-[8px] font-bold rounded-full border border-white/10 shadow-sm transition-colors"
                            :class="selectedSport === sport.key ? 'text-[#00FF7F] border-[#00FF7F]/30' : 'text-slate-400 group-hover:text-white'"
                        >
                            {{ sport.count }}
                        </span>
                    </div>
                    
                    <span class="text-[9px] font-bold uppercase mt-1 transition-colors whitespace-nowrap"
                          :class="selectedSport === sport.key ? 'text-[#00FF7F] drop-shadow-sm' : 'text-gray-500 group-hover:text-gray-300'">
                        {{ sport.name }}
                    </span>

                    <div v-if="selectedSport === sport.key" class="absolute -bottom-1 w-3/4 h-[3px] bg-[#00FF7F] rounded-full shadow-[0_0_8px_rgba(0,255,127,0.6)]"></div>
                </div>
            </div>
        </div>

        <div class="flex justify-center">
            <div class="flex items-center bg-[#020617] p-1 rounded-full border border-white/10 shadow-inner">
              <button 
                @click="emit('update:activeMode', 'live')"
                class="relative flex items-center justify-center gap-2 px-4 py-1.5 rounded-full transition-all duration-300 text-[10px] font-bold uppercase tracking-wider border"
                :class="activeMode === 'live' 
                  ? 'bg-red-500/10 border-red-500/50 text-red-400 shadow-[0_0_10px_rgba(248,113,113,0.3)]' 
                  : 'bg-transparent border-transparent text-slate-500 hover:text-slate-300 hover:bg-white/5'"
              >
                <div class="relative flex h-2 w-2">
                   <span v-if="activeMode === 'live'" class="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75"></span>
                   <span class="relative inline-flex rounded-full h-2 w-2" :class="activeMode === 'live' ? 'bg-red-500' : 'bg-slate-600'"></span>
                </div>
                Ao Vivo
              </button>

              <button 
                @click="emit('update:activeMode', 'prematch')"
                class="relative flex items-center justify-center gap-2 px-4 py-1.5 rounded-full transition-all duration-300 text-[10px] font-bold uppercase tracking-wider border"
                :class="activeMode === 'prematch' 
                  ? 'bg-blue-500/10 border-blue-500/50 text-blue-400 shadow-[0_0_10px_rgba(96,165,250,0.3)]' 
                  : 'bg-transparent border-transparent text-slate-500 hover:text-slate-300 hover:bg-white/5'"
              >
                <CalendarDays class="w-3.5 h-3.5" :class="activeMode === 'prematch' ? 'drop-shadow-[0_0_5px_rgba(96,165,250,0.8)]' : ''" />
                Pré-Jogo
              </button>
            </div>
        </div>

        <div class="flex justify-end min-w-0">
            <FiltroDatas 
                :games="props.games as any[]" 
                :model-value="selectedDate"
                @update:model-value="(val) => emit('update:selectedDate', val)"
            />
        </div>

    </div>
  </div>
</template>

<style scoped>
.no-scrollbar::-webkit-scrollbar { display: none; }
.no-scrollbar { -ms-overflow-style: none; scrollbar-width: none; }

/* ✅ MÁSCARA MENOS AGRESSIVA */
.mask-fade-right {
    -webkit-mask-image: linear-gradient(to right, black 90%, transparent 100%);
    mask-image: linear-gradient(to right, black 90%, transparent 100%);
}
</style>