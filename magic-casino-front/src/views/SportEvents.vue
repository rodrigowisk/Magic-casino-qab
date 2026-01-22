<script setup lang="ts">
import { ref, watch, computed, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { Play, Clock, ArrowLeft } from 'lucide-vue-next';
import SportsService from '../services/SportsService';
import TeamLogo from '../components/TeamLogo.vue';
import { useBetStore, type BetType } from '../stores/useBetStore';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';

// ✅ IMPORTAÇÃO DA NOVA FUNÇÃO DE BANDEIRAS
import { getFlag } from '../utils/flags'; 

// --- ⚙️ CONFIGURAÇÃO DE URL INTELIGENTE (CORREÇÃO DE ROTA) ---
const getBaseUrl = () => {
  // Pega a URL do .env (ex: http://localhost:8090/api/sports)
  const envUrl = import.meta.env.VITE_API_URL || 'http://localhost:8888';
  try {
    const url = new URL(envUrl);
    
    // Se a URL estiver apontando direto para o backend (8090), 
    // forçamos para o NGINX (8888) para garantir que o roteamento e o túnel funcionem.
    if (url.port === '8090' && url.hostname === 'localhost') {
        return `${url.protocol}//${url.hostname}:8888`;
    }
    
    // Retorna apenas a origem (http://dominio:porta), removendo "/api/sports"
    return url.origin; 
  } catch {
    return 'http://localhost:8888';
  }
};

const BASE_URL = getBaseUrl(); 

// Interface flexível
interface SportEvent {
  externalId?: string;
  id?: string;
  homeTeam: string;
  awayTeam: string;
  commenceTime: string;
  league?: string;
  countryCode?: string;
  leagueId?: string;
  homeTeamLogo?: string;
  awayTeamLogo?: string;
  
  rawOddsHome?: number;
  rawOddsDraw?: number;
  rawOddsAway?: number;
  RawOddsHome?: number;
  RawOddsDraw?: number;
  RawOddsAway?: number;
}

const route = useRoute();
const router = useRouter();
const betStore = useBetStore();

// --- ESTADO ---
const loading = ref(true);
const loadingMore = ref(false);
const events = ref<SportEvent[]>([]);
const currentPage = ref(1);
const hasMore = ref(true);
const pageSize = 1000; 

// --- STATE SIGNALR ---
const connection = ref<HubConnection | null>(null);

// --- FILTROS ---
const openLeagues = ref<Set<string>>(new Set());
const dataSelecionada = ref<string>('all');

const sportKey = computed(() => {
  const id = route.params?.id;
  return (Array.isArray(id) ? id[0] : id) || 'soccer';
});

const leagueFilter = computed(() => {
  const l = route.query?.league;
  return (Array.isArray(l) ? l[0] : l) as string | undefined;
});

const loadTrigger = ref<HTMLElement | null>(null);
let observer: IntersectionObserver | null = null;

// --- UTILS ---
const getLocalDateString = (dateObj: Date) => {
    const year = dateObj.getFullYear();
    const month = String(dateObj.getMonth() + 1).padStart(2, '0');
    const day = String(dateObj.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
};

const getGameId = (game: SportEvent): string => {
  const id = game.externalId || game.id || (game as any).ExternalId;
  if (id) return String(id);
  return `${game.homeTeam}_${game.awayTeam}_${game.commenceTime}`;
};

const countBetsInLeague = (leagueGames: SportEvent[]) => {
  const leagueGameIds = leagueGames.map(g => getGameId(g));
  return betStore.selections.filter(sel => {
    return leagueGameIds.some(id => sel.id === id || String(sel.id).startsWith(id + '_'));
  }).length;
};

const getOdd = (game: SportEvent, type: BetType): string => {
  let val: number | undefined | null = 0;
  if (type === '1') val = game.rawOddsHome ?? game.RawOddsHome;
  else if (type === '2') val = game.rawOddsAway ?? game.RawOddsAway;
  else val = game.rawOddsDraw ?? game.RawOddsDraw;

  if (!val || val <= 1.0) return '0.00';
  return Number(val).toFixed(2);
};

const getSelectedType = (gameId: string): BetType | null => {
  const found = betStore.selections.find(s => s.id === gameId);
  return found ? found.type : null;
};

const handleSelection = (game: SportEvent, type: BetType) => {
  const gameId = getGameId(game);
  const priceStr = getOdd(game, type);
  const price = parseFloat(priceStr);

  if (price <= 1.0 || isNaN(price)) return;

  if (getSelectedType(gameId) === type) {
    betStore.removeSelection(gameId);
    return;
  }

  const selectionName = type === '1' ? game.homeTeam : type === '2' ? game.awayTeam : 'Empate';

  betStore.addOrReplaceSelection(
    gameId,
    game.homeTeam,
    game.awayTeam,
    selectionName,
    price,
    type,
    game.commenceTime
  );
};

// --- FETCHING ---
const fetchEvents = async (reset = false) => {
  if (!sportKey.value) return;
  
  if (reset) {
    loading.value = true;
    currentPage.value = 1;
    events.value = [];
    hasMore.value = true;
    openLeagues.value.clear();
  } else {
    if (!hasMore.value || loadingMore.value) return;
    loadingMore.value = true;
  }

  try {
    const data = await SportsService.getEvents(sportKey.value, currentPage.value, pageSize);
    const newEvents = (data || []) as SportEvent[];

    if (newEvents.length < pageSize) hasMore.value = false;

    if (reset) {
        events.value = newEvents;
    } else {
        const existingIds = new Set(events.value.map(e => getGameId(e)));
        const uniqueNewEvents = newEvents.filter(e => !existingIds.has(getGameId(e)));
        events.value.push(...uniqueNewEvents);
    }
    
    if (currentPage.value === 1) {
        newEvents.forEach(e => { if (e.league) openLeagues.value.add(e.league); });
    }
    
    currentPage.value++;
  } catch (e) {
    console.error(e);
    hasMore.value = false; 
  } finally {
    loading.value = false;
    loadingMore.value = false;
  }
};

watch(() => route.params.id, () => fetchEvents(true), { immediate: true });

// --- LIFECYCLE (SIGNALR E SCROLL) ---
onMounted(async () => {
  // 1. Scroll Infinito
  observer = new IntersectionObserver((entries) => {
    if (entries[0]?.isIntersecting && !loading.value && !loadingMore.value && hasMore.value) {
      fetchEvents(false);
    }
  }, { rootMargin: '200px' });
  if (loadTrigger.value) observer.observe(loadTrigger.value);

  // 2. Conexão SignalR - CORRIGIDA
  try {
    // Monta a URL correta: http://localhost:8888/gameHub
    const signalRUrl = `${BASE_URL}/gameHub`; 
    console.log(`🔌 [PRÉ-JOGO] Conectando SignalR em: ${signalRUrl}`);

    connection.value = new HubConnectionBuilder()
      .withUrl(signalRUrl)
      .configureLogging(LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    // 🔥 OUVINTE 1: Remove o jogo da lista de pré-jogo assim que começa
    connection.value.on("RemoveGames", (gameIds: string[]) => {
      console.log("🔥 [SIGNALR] Jogo começou! Removendo da lista:", gameIds);
      
      if (events.value.length > 0) {
        const initialCount = events.value.length;
        
        events.value = events.value.filter(e => {
            const id = getGameId(e);
            return !gameIds.includes(id);
        });
        
        const removedCount = initialCount - events.value.length;
        if (removedCount > 0) {
            console.log(`✅ Sucesso! ${removedCount} jogos removidos do pré-jogo.`);
        }
      }
    });

    // 🔇 OUVINTE 2: Silenciador de Warning
    connection.value.on("LiveOddsUpdate", () => {
        // Ignora atualizações de odds ao vivo nesta tela (é pré-jogo)
    });

    await connection.value.start();
    console.log("🟢 SignalR Conectado e Pronto!");

  } catch (err) {
    console.error("❌ Erro SignalR:", err);
  }
});

onUnmounted(() => {
  if (observer) observer.disconnect();
  if (connection.value) {
    connection.value.stop();
  }
});

const datasFiltro = computed(() => {
  const opcoes = [{ label: 'TODAS', valor: 'all' }];
  const hoje = new Date();
  for (let i = 0; i < 4; i++) {
    const d = new Date();
    d.setDate(hoje.getDate() + i);
    let label = i === 0 ? 'Hoje' : i === 1 ? 'Amanhã' : d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
    const valor = getLocalDateString(d);
    opcoes.push({ label: label.toUpperCase(), valor });
  }
  return opcoes;
});

const betTypesToShow = computed(() => {
  const s = sportKey.value.toLowerCase();
  if (s.includes('soccer') || s.includes('futebol')) return ['1', 'X', '2'] as BetType[];
  return ['1', '2'] as BetType[];
});

const groupedEvents = computed(() => {
  const groups: Record<string, SportEvent[]> = {};
  events.value.forEach(event => {
    if (leagueFilter.value && event.league !== leagueFilter.value) return;
    
    if (dataSelecionada.value !== 'all') {
        const dateObj = new Date(event.commenceTime);
        const eventDateLocal = getLocalDateString(dateObj);
        if (eventDateLocal !== dataSelecionada.value) return;
    }

    const league = event.league || 'Outros';
    if (!groups[league]) groups[league] = [];
    groups[league].push(event);
  });
  return groups;
});

const formatTime = (d: string) => d ? new Date(d).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) : '--:--';
const formatDate = (d: string) => {
  if (!d) return '---';
  const date = new Date(d);
  return date.getDate().toString().padStart(2, '0') + '/' + (date.getMonth() + 1).toString().padStart(2, '0');
};
const traduzirEsporte = (key: string) => {
  if (!key) return '';
  const k = key.toLowerCase();
  if (k.includes('soccer')) return 'Futebol';
  if (k.includes('basket')) return 'Basquete';
  if (k.includes('voley') || k.includes('volei')) return 'Vôlei';
  if (k.includes('tennis')) return 'Tênis';
  return key.toUpperCase();
};

const handleImageError = (event: Event) => {
    const target = event.target as HTMLImageElement;
    if (target) target.style.display = 'none';
};
</script>

<template>
  <div class="space-y-4 pb-20">
    <div class="flex flex-col md:flex-row md:items-center gap-4 pb-2">
      <div class="flex items-center gap-3">
        <button v-if="leagueFilter" @click="router.push({ name: 'sport-events', params: { id: sportKey } })" class="bg-stake-card p-2 rounded hover:bg-white/10 text-white transition">
          <ArrowLeft class="w-5 h-5" />
        </button>
        <h2 class="text-white text-xl font-bold uppercase italic whitespace-nowrap">
          <span class="text-stake-blue">#</span> {{ leagueFilter || traduzirEsporte(sportKey) }}
        </h2>
      </div>
      <div class="flex gap-2 overflow-x-auto no-scrollbar pb-1">
        <button v-for="data in datasFiltro" :key="data.valor" @click="dataSelecionada = data.valor"
          :class="['px-4 py-1.5 rounded font-bold text-[11px] uppercase whitespace-nowrap transition', dataSelecionada === data.valor ? 'bg-stake-blue text-white' : 'bg-stake-card text-stake-text hover:bg-stake-hover']">
          {{ data.label }}
        </button>
      </div>
    </div>

    <div v-if="loading" class="space-y-3">
        <div v-for="i in 5" :key="i" class="bg-stake-card h-24 rounded animate-pulse border border-white/5"></div>
    </div>

    <div v-else class="space-y-4">
      
      <div v-if="Object.keys(groupedEvents).length === 0" class="py-10 text-center text-stake-text opacity-50 border border-dashed border-stake-text/20 rounded">
        <p>Nenhum jogo carregado para esta data.</p>
        <button @click="fetchEvents(false)" class="mt-3 text-stake-blue font-bold text-xs hover:underline cursor-pointer">
            Buscar mais jogos...
        </button>
      </div>

      <div v-else v-for="(games, league) in groupedEvents" :key="league" class="rounded overflow-hidden">
        <div @click="openLeagues.has(String(league)) ? openLeagues.delete(String(league)) : openLeagues.add(String(league))"
          class="bg-stake-card p-3 flex items-center justify-between border-l-4 border-stake-blue cursor-pointer hover:brightness-110 transition-all select-none">
          <div class="flex items-center gap-3">
            
            <img :src="getFlag(league, games[0].countryCode)" class="w-5 h-3.5 rounded-sm shadow-sm object-cover bg-black/20" @error="handleImageError" />
            
            <h3 class="text-white font-bold text-sm uppercase">{{ league }}</h3>
            <span v-if="countBetsInLeague(games) > 0" class="bg-yellow-500 text-black text-[10px] font-black w-5 h-5 flex items-center justify-center rounded-full ml-1">{{ countBetsInLeague(games) }}</span>
          </div>
          <Play fill="currentColor" class="w-3.5 h-3.5 text-stake-text transition-transform duration-300" :class="{'rotate-90': openLeagues.has(String(league))}" />
        </div>

        <div v-show="openLeagues.has(String(league))" class="bg-stake-dark border-x border-b border-stake-card/30">
          <div v-for="game in games" :key="getGameId(game)" class="p-4 border-b border-stake-card/30 flex flex-col md:flex-row items-center gap-4 transition-all hover:bg-[#B6FF00]/[0.05]">
            <div class="flex flex-col items-center min-w-[70px]">
              <div class="text-[10px] font-bold text-stake-blue mb-0.5">{{ formatDate(game.commenceTime) }}</div>
              <div class="flex items-center gap-1 text-white text-sm font-bold"><Clock class="w-3 h-3 text-stake-text" /> {{ formatTime(game.commenceTime) }}</div>
            </div>
            <div class="flex-1 w-full text-white cursor-pointer hover:text-stake-blue transition-colors" @click="router.push({ name: 'event-details', params: { id: getGameId(game) } })">
              <div class="flex items-center gap-2 mb-2"><TeamLogo :teamName="game.homeTeam || ''" :remoteUrl="game.homeTeamLogo" size="w-5 h-5" /><span class="font-bold text-sm">{{ game.homeTeam }}</span></div>
              <div class="flex items-center gap-2"><TeamLogo :teamName="game.awayTeam || ''" :remoteUrl="game.awayTeamLogo" size="w-5 h-5" /><span class="font-bold text-sm">{{ game.awayTeam }}</span></div>
            </div>
            <div class="flex gap-2 w-full md:w-auto">
              <button v-for="type in betTypesToShow" :key="type" @click.stop="handleSelection(game, type)" :disabled="parseFloat(getOdd(game, type)) <= 1.0"
                :class="['flex-1 md:w-24 py-2 rounded flex flex-col items-center justify-center border transition-all active:scale-95 group', parseFloat(getOdd(game, type)) <= 1.0 ? 'opacity-50 cursor-not-allowed bg-stake-card border-transparent' : getSelectedType(getGameId(game)) === type ? 'bg-stake-blue border-stake-blue' : 'bg-stake-card border-transparent hover:border-stake-text/30']">
                <span :class="['text-[10px] font-bold mb-0.5', getSelectedType(getGameId(game)) === type ? 'text-white' : 'text-stake-text']">{{ type === '1' ? 'Casa' : type === '2' ? 'Fora' : 'Empate' }}</span>
                <span class="text-sm font-bold text-white">{{ getOdd(game, type) }}</span>
              </button>
            </div>
          </div>
        </div>
      </div>

      <div ref="loadTrigger" class="h-20 flex items-center justify-center py-4">
          <div v-if="loadingMore" class="flex items-center gap-2 text-stake-blue font-bold text-xs uppercase animate-pulse">
              <div class="w-2 h-2 bg-stake-blue rounded-full animate-bounce"></div> Carregando mais jogos...
          </div>
          <div v-else-if="!hasMore" class="text-xs text-stake-text/30">
              Fim da lista
          </div>
      </div>

    </div>
  </div>
</template>