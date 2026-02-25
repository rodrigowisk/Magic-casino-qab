<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { CalendarDays, ChevronDown, Check, Calendar } from 'lucide-vue-next';
import { useRouter, useRoute } from 'vue-router'; 
import FiltroDatas from '../FiltroDatas.vue'; 

// --- PROPS ---
const props = withDefaults(defineProps<{
  games?: any[]; 
  activeMode?: 'prematch' | 'live';
  selectedSport?: string;    
  selectedDate?: string;
  tournamentRules?: any; // <-- PROP PARA LER AS REGRAS DO TEMPLATE
}>(), {
  games: () => [],
  activeMode: 'prematch',
  selectedSport: 'all',
  selectedDate: 'all',
  tournamentRules: null
});

// --- EMITS ---
const emit = defineEmits<{
  (e: 'update:activeMode', val: 'prematch' | 'live'): void;
  (e: 'update:selectedSport', val: string): void;
  (e: 'update:selectedDate', val: string): void;
}>();

const router = useRouter();
const route = useRoute();

const handleModeSwitch = (mode: 'prematch' | 'live') => {
    if (props.activeMode === mode) return;
    emit('update:activeMode', mode);
    const tournamentId = route.params.id;
    if (tournamentId) {
        if (mode === 'live') {
            router.push({ name: 'TournamentLive', params: { id: tournamentId } });
        } else {
            router.push({ name: 'TournamentPlay', params: { id: tournamentId } });
        }
    }
};

const isDateDropdownOpen = ref(false);
const toggleDateDropdown = () => { isDateDropdownOpen.value = !isDateDropdownOpen.value; };
const closeDateDropdown = () => { isDateDropdownOpen.value = false; };

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
    if (k.includes('tennis') || k.includes('tênis')) return 'tennis';
    if (k.includes('volley')) return 'volleyball';
    if (k.includes('hockey')) return 'ice-hockey';
    return 'all'; 
};

// --- LÓGICA INTELIGENTE: FILTRO & CONTAGEM DE ESPORTES ---
const availableSports = computed(() => {
    const rawGames = props.games || []; 
    const safeGames = Array.isArray(rawGames) ? rawGames : [];

    const counts: Record<string, number> = { all: 0 };
    
    // Conta quantos jogos tem para cada esporte disponível
    safeGames.forEach((g: any) => {
        if (!g) return;
        const sportRaw = g.sportKey || g.Sport || g.sport || g.SportKey || 'soccer';
        const key = normalizeKey(String(sportRaw));
        
        counts[key] = (counts[key] ?? 0) + 1;
        counts['all'] = (counts['all'] ?? 0) + 1;
    });

    const allowedKeys = new Set<string>();
    let isMisto = false;

    // 1. Prioriza 100% as regras do template se forem passadas
    if (props.tournamentRules && props.tournamentRules.sports && Array.isArray(props.tournamentRules.sports)) {
        props.tournamentRules.sports.forEach((s: any) => {
            if (s.key) allowedKeys.add(normalizeKey(s.key));
        });
        if (allowedKeys.size > 1) {
            isMisto = true;
        }
    } else {
        // 2. Fallback caso a prop não seja passada (lê dos jogos listados)
        safeGames.forEach(g => {
            const sportRaw = g.sportKey || g.Sport || g.sport || g.SportKey || 'soccer';
            allowedKeys.add(normalizeKey(String(sportRaw)));
        });
        if (props.selectedSport && props.selectedSport !== 'all') {
            allowedKeys.add(normalizeKey(props.selectedSport));
        }
        if (allowedKeys.size > 1) {
            isMisto = true;
        }
    }

    // Monta o resultado final combinando os esportes permitidos com as contagens
    let result = ALL_SPORTS_CONFIG
        .filter(s => {
            if (s.key === 'all') return isMisto; // Só exibe "Todos" se o torneio for misto (mais de 1 esporte)
            return allowedKeys.has(s.key);
        })
        .map(s => ({
            ...s,
            count: counts[s.key] ?? 0
        }));

    return result;
});

const isMobileCompact = computed(() => {
  return availableSports.value.length <= 2;
});

// --- LÓGICA PARA AS OPÇÕES DE DATA ---
const mobileDateOptions = computed(() => {
  const lista = [{ label: 'DATA: TODAS', value: 'all' }];
  const rawGames = props.games || [];
  if (rawGames.length === 0) return lista;

  const datasUnicas = new Set<string>();
  const hoje = new Date();
  hoje.setHours(0, 0, 0, 0);
  const amanha = new Date(hoje);
  amanha.setDate(amanha.getDate() + 1);

  rawGames.forEach((game: any) => {
      const timeData = game.commenceTime || game.start_at || game.date;
      if (!timeData) return;
      const d = new Date(timeData);
      if (isNaN(d.getTime())) return;
      const ano = d.getFullYear();
      const mes = String(d.getMonth() + 1).padStart(2, '0');
      const dia = String(d.getDate()).padStart(2, '0');
      datasUnicas.add(`${ano}-${mes}-${dia}`);
  });

  const datasOrdenadas = Array.from(datasUnicas).sort();

  datasOrdenadas.forEach(dataStr => {
      const parts = dataStr.split('-');
      const y = Number(parts[0]);
      const m = Number(parts[1]);
      const d = Number(parts[2]);
      const dataRef = new Date(y, m - 1, d);
      if (dataRef.getTime() < hoje.getTime()) return;

      let label = '';
      if (dataRef.getTime() === hoje.getTime()) label = 'HOJE';
      else if (dataRef.getTime() === amanha.getTime()) label = 'AMANHÃ';
      else label = `${String(d).padStart(2, '0')}/${String(m).padStart(2, '0')}`;

      lista.push({ label: label, value: dataStr });
  });

  return lista;
});

const selectedDateLabel = computed(() => {
  const found = mobileDateOptions.value.find(opt => opt.value === props.selectedDate);
  return found ? found.label : 'DATA: TODAS'; 
});

const selectDateOption = (value: string) => {
  emit('update:selectedDate', value);
  closeDateDropdown();
};

// 🔥 CORREÇÃO: GARANTIR QUE O PRIMEIRO ESPORTE ESTEJA SEMPRE SELECIONADO (E SEGURO PARA TYPESCRIPT)
watch(availableSports, (newList) => {
    if (!newList || newList.length === 0) return;

    // Verifica se o esporte atualmente selecionado existe na nova lista gerada
    const isCurrentSportAvailable = newList.some(sport => sport && sport.key === props.selectedSport);

    // Se não existir (ou se a prop vier vazia), força a seleção do primeiro item da lista.
    if (!isCurrentSportAvailable || !props.selectedSport) {
        const firstSport = newList[0];
        // Proteção contra undefined extra exigida pelo compilador do Vite/TS
        if (firstSport && firstSport.key) {
            emit('update:selectedSport', firstSport.key);
        }
    }
}, { immediate: true });

// --- DRAG ---
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
  <div class="w-full bg-[#0f172a]/50 backdrop-blur border-b border-white/5 relative z-30 transition-all duration-300">
    
    <div 
        class="max-w-[1200px] mx-auto px-2 md:px-4 py-2 md:py-0 md:h-[72px]"
        :class="[
            'md:grid md:grid-cols-[1fr_auto_1fr] md:items-center md:gap-4',
            'flex',
            isMobileCompact ? 'flex-row items-center justify-between px-2 gap-2' : 'flex-col gap-2 pt-1 pb-2 px-1'
        ]"
    >

        <div 
            class="relative overflow-visible order-1 flex items-center justify-start transition-all duration-300"
            :class="isMobileCompact ? 'w-auto flex-none' : 'w-full'"
        >
            <div 
                ref="scrollContainer"
                class="flex items-center gap-4 overflow-x-auto no-scrollbar scroll-smooth cursor-grab active:cursor-grabbing w-full mask-fade-right py-1"
                :class="isMobileCompact ? 'px-0' : 'px-2'"
                @mousedown="startDrag"
                @mouseleave="stopDrag"
                @mouseup="stopDrag"
                @mousemove="moveDrag"
            >
                <div 
                    v-for="sport in availableSports" 
                    :key="sport.key"
                    @click="selectSport(sport.key)"
                    class="group flex flex-col items-center justify-center min-w-[75px] md:min-w-[80px] px-2 h-[55px] rounded-lg transition-all duration-300 relative flex-shrink-0 cursor-pointer"
                    :class="[
                        { 'pointer-events-none': isDragging },
                        selectedSport === sport.key 
                            ? 'scale-110 z-10' 
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
                            class="absolute -top-2 -right-3 min-w-[14px] h-[14px] flex items-center justify-center bg-[#1e293b] text-[8px] font-bold rounded-full border border-white/10 shadow-sm transition-colors px-1"
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

        <div 
            class="md:hidden order-2 relative z-40 transition-all duration-300 flex items-center justify-between gap-2"
            :class="isMobileCompact ? 'flex-1' : 'w-full'"
        >
            
            <div v-if="activeMode === 'prematch'" class="relative flex-1 min-w-0">
                <button 
                    @click="toggleDateDropdown"
                    class="w-full flex items-center justify-between bg-[#020617] border rounded-lg py-1.5 px-3 transition-all duration-300 group shadow-lg shadow-black/20"
                    :class="isDateDropdownOpen ? 'border-blue-500/50 ring-1 ring-blue-500/20 text-white' : 'border-white/10 text-slate-300 hover:border-white/20'"
                >   
                    <div class="flex items-center gap-1.5 overflow-hidden">
                        <Calendar class="w-3 h-3 text-blue-400 shrink-0" />
                        <span class="text-[10px] font-bold uppercase tracking-wide whitespace-nowrap truncate">
                           {{ selectedDateLabel }}
                        </span>
                    </div>
                    <ChevronDown 
                        class="w-3 h-3 text-slate-500 transition-transform duration-300 shrink-0 ml-1"
                        :class="{ 'rotate-180 text-blue-400': isDateDropdownOpen }"
                    />
                </button>
                
                <div v-if="isDateDropdownOpen" class="fixed inset-0 z-40 bg-transparent" @click="closeDateDropdown"></div>
                <div 
                    v-if="isDateDropdownOpen"
                    class="absolute top-[calc(100%+8px)] left-0 w-[160px] bg-[#0f172a]/95 backdrop-blur-xl border border-white/10 rounded-xl shadow-2xl z-50 overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200"
                >
                    <div class="max-h-[250px] overflow-y-auto py-1 custom-scrollbar">
                        <button
                            v-for="opt in mobileDateOptions"
                            :key="opt.value"
                            @click="selectDateOption(opt.value)"
                            class="w-full flex items-center justify-between px-4 py-2.5 text-[10px] font-bold uppercase transition-colors border-b border-white/5 last:border-0"
                            :class="props.selectedDate === opt.value 
                                ? 'bg-blue-500/10 text-[#00FF7F]' 
                                : 'text-slate-300 hover:bg-white/5 hover:text-white'"
                        >
                            <span>{{ opt.label }}</span>
                            <Check v-if="props.selectedDate === opt.value" class="w-3 h-3 text-[#00FF7F]" />
                        </button>
                    </div>
                </div>
            </div>
            
            <div v-else class="flex-1"></div>

            <div class="flex items-center justify-center bg-[#020617] p-1 rounded-full border border-white/10 shadow-inner shrink-0">
              <button 
                @click="handleModeSwitch('live')"
                class="relative flex items-center justify-center gap-1.5 px-3 py-1.5 rounded-full transition-all duration-300 text-[9px] font-bold uppercase tracking-wider border"
                :class="activeMode === 'live' 
                  ? 'bg-[#00FF7F]/10 border-[#00FF7F]/50 text-[#00FF7F] shadow-[0_0_10px_rgba(0,255,127,0.3)]' 
                  : 'bg-transparent border-transparent text-slate-500 hover:text-slate-300 hover:bg-white/5'"
              >
                <div class="relative flex h-1.5 w-1.5">
                    <span v-if="activeMode === 'live'" class="animate-ping absolute inline-flex h-full w-full rounded-full bg-[#00FF7F] opacity-75"></span>
                    <span class="relative inline-flex rounded-full h-1.5 w-1.5" :class="activeMode === 'live' ? 'bg-[#00FF7F]' : 'bg-slate-600'"></span>
                </div>
                Ao Vivo
              </button>

              <button 
                @click="handleModeSwitch('prematch')"
                class="relative flex items-center justify-center gap-1.5 px-3 py-1.5 rounded-full transition-all duration-300 text-[9px] font-bold uppercase tracking-wider border"
                :class="activeMode === 'prematch' 
                  ? 'bg-blue-500/10 border-blue-500/50 text-blue-400 shadow-[0_0_10px_rgba(96,165,250,0.3)]' 
                  : 'bg-transparent border-transparent text-slate-500 hover:text-slate-300 hover:bg-white/5'"
              >
                <CalendarDays class="w-3 h-3" :class="activeMode === 'prematch' ? 'drop-shadow-[0_0_5px_rgba(96,165,250,0.8)]' : ''" />
                Pré
              </button>
            </div>
        </div>

        <div class="hidden md:flex justify-center order-2">
            <div class="flex items-center bg-[#020617] p-1 rounded-full border border-white/10 shadow-inner">
              <button 
                @click="handleModeSwitch('live')"
                class="relative flex items-center justify-center gap-2 px-4 py-1.5 rounded-full transition-all duration-300 text-[10px] font-bold uppercase tracking-wider border"
                :class="activeMode === 'live' 
                  ? 'bg-[#00FF7F]/10 border-[#00FF7F]/50 text-[#00FF7F] shadow-[0_0_10px_rgba(0,255,127,0.3)]' 
                  : 'bg-transparent border-transparent text-slate-500 hover:text-slate-300 hover:bg-white/5'"
              >
                <div class="relative flex h-2 w-2">
                    <span v-if="activeMode === 'live'" class="animate-ping absolute inline-flex h-full w-full rounded-full bg-[#00FF7F] opacity-75"></span>
                    <span class="relative inline-flex rounded-full h-2 w-2" :class="activeMode === 'live' ? 'bg-[#00FF7F]' : 'bg-slate-600'"></span>
                </div>
                Ao Vivo
              </button>

              <button 
                @click="handleModeSwitch('prematch')"
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

        <div v-if="activeMode === 'prematch'" class="hidden md:flex justify-end min-w-0 order-3">
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

.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 4px; }

.mask-fade-right {
    -webkit-mask-image: linear-gradient(to right, black 90%, transparent 100%);
    mask-image: linear-gradient(to right, black 90%, transparent 100%);
}

@keyframes fadeIn { from { opacity: 0; transform: scale(0.95); } to { opacity: 1; transform: scale(1); } }
.animate-in { animation: fadeIn 0.15s ease-out forwards; }
</style>