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
const loadingAdmin = ref(true); // Carregando configuração do Admin
const loadingApi = ref(true);   // Carregando disponibilidade de jogos

// Sets de controle
const adminVisibleSports = ref<Set<string>>(new Set()); // Esportes ON no admin
const apiActiveSports = ref<Set<string>>(new Set());    // Esportes com jogos na API (> 0)

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
  // Adicione outros mapeamentos conforme necessário
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
    // 1. Se ainda não carregou a config do admin, não mostra nada (ou mostra skeleton)
    if (loadingAdmin.value && !props.isLive) return [];

    const list = ALL_SPORTS_CONFIG.map(sport => {
        let isVisibleInAdmin = false;
        let hasActiveGames = false;
        let count = 0;

        // --- LÓGICA LIVE ---
        if (props.isLive) {
            if (sport.key === 'all') {
                isVisibleInAdmin = true;
                hasActiveGames = true; 
                count = props.liveCounts ? Object.values(props.liveCounts).reduce((a, b) => a + b, 0) : 0;
            } else {
                isVisibleInAdmin = true; 
                hasActiveGames = props.liveSports?.has(sport.key) || false;
                count = props.liveCounts?.[sport.key] || 0;
            }
        } 
        // --- LÓGICA PRÉ-JOGO (REQ DO USUÁRIO) ---
        else {
            if (sport.key === 'all') {
                isVisibleInAdmin = false; 
            } else {
                // REGRA 1: Status Admin (ON/OFF) - Define se o ícone EXISTE na tela
                isVisibleInAdmin = adminVisibleSports.value.has(sport.key);

                // REGRA 2: Status API (Tem jogo > 0?) - Define se tem COR
                hasActiveGames = !loadingApi.value && apiActiveSports.value.has(sport.key);
            }
        }
        
        return {
            ...sport,
            isVisible: isVisibleInAdmin, // Renderiza?
            isActive: hasActiveGames,    // Colorido/Clicável?
            count,
            iconPath: `/images/icons/${sport.file}` 
        };
    });

    // Filtro FINAL: Remove da tela o que estiver OFF no Admin
    let finalList = list.filter(s => {
        if (props.isLive) return s.isActive; 
        return s.isVisible;                  
    });

    // Ordenação
    return finalList.sort((a, b) => {
        if (a.key === 'all') return -1;
        if (b.key === 'all') return 1;

        if (props.isLive) return b.count - a.count;

        // Pré-jogo: Prioriza os que estão ATIVOS (coloridos) sobre os inativos (cinza)
        if (a.isActive && !b.isActive) return -1;
        if (!a.isActive && b.isActive) return 1;

        // Prioridade fixa Futebol
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
    
    // REGRA 3: Só clica se a API liberou (isActive = true, ou seja, count > 0)
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

// --- API FETCHING ---
onMounted(async () => {
    if (props.isLive) {
        loadingAdmin.value = false;
        loadingApi.value = false;
        return;
    }

    // ---------------------------------------------------------
    // PASSO 1: BUSCAR O QUE ESTÁ "ON" NO PAINEL MASTER
    // ---------------------------------------------------------
    try {
        const adminData = await SportsService.getAdminConfig(); 
        
        //console.log("📦 RESPOSTA DO ADMIN CONFIG:", adminData);

        const enabledSet = new Set<string>();
        // Garante que é um array, caso venha nulo ou formato errado
        const list = Array.isArray(adminData) ? adminData : [];

        list.forEach((item: any) => {
            // Verifica maiúsculo/minúsculo (C# vs JSON)
            const isEnabled = item.isActive === true || item.IsActive === true;
            const sportKey = item.key || item.Key;

            if (isEnabled && sportKey) {
                const normalized = normalizeKey(sportKey);
                if (normalized) enabledSet.add(normalized);
            }
        });

        adminVisibleSports.value = enabledSet;

    } catch (err) {
        //console.error("❌ Erro config admin", err);
        // Fallback: Apenas futebol para não quebrar a tela, mas indicando erro
        adminVisibleSports.value = new Set(['soccer']); 
    } finally {
        loadingAdmin.value = false; // Aqui os ícones aparecem (CINZA se não tiver jogo)
    }

    // ---------------------------------------------------------
    // PASSO 2: VERIFICAR NA API QUAIS TEM JOGOS (PARA DAR COR)
    // ---------------------------------------------------------
    try {
        const data = await SportsService.getActiveSports(); 
        const availableKeys = new Set<string>();
        
        data.forEach((item: any) => {
            // ⚠️ CORREÇÃO: Verifica se tem jogos (> 0)
            // Se vier count: 0, ignoramos e o ícone fica cinza (inativo)
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
        loadingApi.value = false; // Aqui os ícones ganham COR se tiverem jogo
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
      
      <div v-if="loadingAdmin && !isLive" class="flex gap-3">
         <div v-for="i in 5" :key="i" class="w-[60px] h-[50px] bg-white/5 animate-pulse rounded-md"></div>
      </div>

      <div 
        v-else
        v-for="sport in orderedSports" 
        :key="sport.key"
        @click="handleSportClick(sport)"
        class="group flex flex-col items-center min-w-[60px] relative transition-all duration-300"
        :class="[
            { 'pointer-events-none': isDragging },
            /* Lógica de Cursor */
            sport.isActive 
                ? 'cursor-pointer' 
                : 'cursor-not-allowed' // Mostra proibido se estiver Cinza
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
                            : 'grayscale opacity-30 contrast-50' // TOM DE CINZA AQUI
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
                  : 'text-gray-700' // Texto mais escuro para item desativado
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