<script setup lang="ts">
import { ref, watch, computed, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ChevronDown, ChevronRight, Clock, ArrowLeft } from 'lucide-vue-next';
import SportsService from '../services/SportsService';
import TeamLogo from '../components/TeamLogo.vue';
import { useBetStore, type BetType } from '../stores/useBetStore';

// Interface ajustada com as IMAGENS
interface SportEvent {
  externalId?: string;
  id?: string;
  sportsEventId?: string;
  eventId?: string;
  homeTeam: string;
  awayTeam: string;
  commenceTime: string;
  league?: string;
  
  // ✅ NOVOS CAMPOS: URLs das imagens vindas da API
  homeTeamLogo?: string;
  awayTeamLogo?: string;

  // Odds puras vindas do backend
  rawOddsHome?: number;
  rawOddsDraw?: number;
  rawOddsAway?: number;

  odds?: Array<{
    outcomeName: string;
    finalPrice: number | string;
  }>;
}

const route = useRoute();
const router = useRouter();
const betStore = useBetStore();

// --- ESTADO DA PAGINAÇÃO ---
const loading = ref(true);          
const loadingMore = ref(false);     
const events = ref<SportEvent[]>([]);
const currentPage = ref(1);         
const hasMore = ref(true);          
const pageSize = 20;                

// --- FILTROS ---
const openLeagues = ref<Set<string>>(new Set());
const dataSelecionada = ref<string>('all');

// Garante que sportKey nunca seja undefined
const sportKey = computed(() => {
    const id = route.params?.id; 
    return (Array.isArray(id) ? id[0] : id) || 'soccer';
});

const leagueFilter = computed(() => {
    const l = route.query?.league;
    return (Array.isArray(l) ? l[0] : l) as string | undefined;
});

// Elemento Gatilho para Scroll Infinito
const loadTrigger = ref<HTMLElement | null>(null);
let observer: IntersectionObserver | null = null;

// --- IDENTIDADE ÚNICA DO JOGO ---
const getGameId = (game: SportEvent): string => {
  const id = game.externalId ?? game.id ?? game.sportsEventId ?? game.eventId;
  if (id) return String(id);

  const home = (game.homeTeam || '').trim();
  const away = (game.awayTeam || '').trim();
  const time = (game.commenceTime || '').trim();
  return `${home}__${away}__${time}`;
};

// --- LÓGICA DE ODDS ---
const getOdd = (game: SportEvent, type: BetType): string => {
  let val: number | undefined = 0;

  if (type === '1') {
    val = game.rawOddsHome;
  } else if (type === '2') {
    val = game.rawOddsAway;
  } else {
    val = game.rawOddsDraw;
  }

  return (val && val > 1.0) ? Number(val).toFixed(2) : '0.00';
};

const getSelectedType = (gameId: string): BetType | null => {
  const found = betStore.selections.find(s => s.id === gameId);
  return found ? found.type : null;
};

const handleSelection = (game: SportEvent, type: BetType) => {
  const gameId = getGameId(game);
  const priceStr = getOdd(game, type);
  const price = parseFloat(priceStr);

  if (price <= 0 || isNaN(price)) return;

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

// --- FUNÇÃO DE BUSCA OTIMIZADA (COM PAGINAÇÃO) ---
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

    if (newEvents.length < pageSize) {
      hasMore.value = false; 
    }

    if (reset) {
      events.value = newEvents;
    } else {
      events.value.push(...newEvents); 
    }

    newEvents.forEach(e => { if (e.league) openLeagues.value.add(e.league); });

    currentPage.value++;
  } catch (e) {
    console.error(e);
  } finally {
    loading.value = false;
    loadingMore.value = false;
  }
};

// --- WATCHER DE ROTA ---
watch(() => route.params.id, () => fetchEvents(true), { immediate: true });

// --- CONFIGURAÇÃO DO SCROLL INFINITO ---
onMounted(() => {
  observer = new IntersectionObserver((entries) => {
    if (entries && entries[0] && entries[0].isIntersecting && !loading.value && hasMore.value) {
      fetchEvents(false);
    }
  }, { rootMargin: '400px' }); 

  if (loadTrigger.value) observer.observe(loadTrigger.value);
});

onUnmounted(() => {
  if (observer) observer.disconnect();
});

// --- DATAS E FILTROS ---
const datasFiltro = computed(() => {
  const opcoes = [{ label: 'TODAS', valor: 'all' }];
  const hoje = new Date();
  
  for (let i = 0; i < 4; i++) {
    const d = new Date();
    d.setDate(hoje.getDate() + i);
    
    let label = '';
    if (i === 0) label = 'Hoje';
    else if (i === 1) label = 'Amanhã';
    else label = d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });

    const ano = d.getFullYear();
    const mes = String(d.getMonth() + 1).padStart(2, '0');
    const dia = String(d.getDate()).padStart(2, '0');
    const valor = `${ano}-${mes}-${dia}`;

    opcoes.push({ label: label.toUpperCase(), valor });
  }
  return opcoes;
});

const traduzirEsporte = (key: string) => {
  if (!key) return '';
  const k = key.toLowerCase();
  
  if (k === 'esoccer') return 'E-Soccer';
  if (k.includes('soccer')) return 'Futebol';
  if (k.includes('basket')) return 'Basquete';
  if (k.includes('mma')) return 'MMA';
  if (k.includes('boxing')) return 'Boxe';
  if (k.includes('tennis')) return 'Tênis';
  
  return key.toUpperCase();
};

const getFlagUrl = (leagueName: string) => {
  const name = (leagueName || '').toLowerCase();
  const map: Record<string, string> = { brazil: 'br', england: 'gb', spain: 'es', italy: 'it', germany: 'de', france: 'fr', usa: 'us' };
  const found = Object.keys(map).find(c => name.includes(c));
  return `/images/flags/${found ? map[found] : 'un'}.svg`;
};

const betTypesToShow = computed(() => {
  const s = sportKey.value.toLowerCase();
  if (s.includes('soccer')) {
    return ['1', 'X', '2'] as BetType[];
  }
  return ['1', '2'] as BetType[];
});

// --- AGRUPAMENTO COM CORREÇÃO DE DATA ---
const groupedEvents = computed(() => {
  const groups: Record<string, SportEvent[]> = {};
  
  events.value.forEach(event => {
    if (leagueFilter.value && event.league !== leagueFilter.value) return;

    const dateObj = new Date(event.commenceTime);
    const ano = dateObj.getFullYear();
    const mes = String(dateObj.getMonth() + 1).padStart(2, '0');
    const dia = String(dateObj.getDate()).padStart(2, '0');
    const eventDateLocal = `${ano}-${mes}-${dia}`;

    if (dataSelecionada.value !== 'all' && eventDateLocal !== dataSelecionada.value) return;

    const league = event.league || 'Outros';
    if (!groups[league]) groups[league] = [];
    groups[league].push(event);
  });
  
  return groups;
});

const formatTime = (d: string) =>
  d ? new Date(d).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) : '--:--';

const formatDate = (d: string) => {
  if (!d) return '---';
  const date = new Date(d);
  return date.getDate().toString().padStart(2, '0') + '/' + (date.getMonth() + 1).toString().padStart(2, '0');
};
</script>

<template>
  <div class="space-y-4 pb-20">
    <div class="flex flex-col md:flex-row md:items-center gap-4 pb-2">
      <div class="flex items-center gap-3">
        <button
          v-if="leagueFilter"
          @click="router.push({ name: 'sport-events', params: { id: sportKey } })"
          class="bg-stake-card p-2 rounded hover:bg-white/10 text-white"
        >
          <ArrowLeft class="w-5 h-5" />
        </button>

        <h2 class="text-white text-xl font-bold uppercase italic whitespace-nowrap">
          <span class="text-stake-blue">#</span> {{ leagueFilter || traduzirEsporte(sportKey) }}
        </h2>
      </div>

      <div class="flex gap-2 overflow-x-auto no-scrollbar pb-1">
        <button
          v-for="data in datasFiltro"
          :key="data.valor"
          @click="dataSelecionada = data.valor"
          :class="[
            'px-4 py-1.5 rounded font-bold text-[11px] uppercase whitespace-nowrap',
            dataSelecionada === data.valor
              ? 'bg-stake-blue text-white'
              : 'bg-stake-card text-stake-text hover:bg-stake-hover'
          ]"
        >
          {{ data.label }}
        </button>
      </div>
    </div>

    <div v-if="loading" class="space-y-3">
        <div v-for="i in 5" :key="i" class="bg-stake-card h-24 rounded animate-pulse border border-white/5"></div>
    </div>

    <div v-else class="space-y-4">
      <div
        v-if="Object.keys(groupedEvents).length === 0"
        class="py-10 text-center text-stake-text opacity-50 border border-dashed border-stake-text/20 rounded"
      >
        Nenhum jogo encontrado para esta data.
      </div>

      <div v-else v-for="(games, league) in groupedEvents" :key="league" class="rounded overflow-hidden">
        <div
          @click="openLeagues.has(String(league)) ? openLeagues.delete(String(league)) : openLeagues.add(String(league))"
          class="bg-stake-card p-3 flex items-center justify-between border-l-4 border-stake-blue cursor-pointer hover:brightness-110 transition-all"
        >
          <div class="flex items-center gap-3">
            <img :src="getFlagUrl(String(league))" class="w-5 h-3.5 rounded-sm shadow-sm" />
            <h3 class="text-white font-bold text-sm uppercase">{{ league }}</h3>
          </div>
          <component :is="openLeagues.has(String(league)) ? ChevronDown : ChevronRight" class="w-5 h-5 text-stake-text" />
        </div>

        <div v-show="openLeagues.has(String(league))" class="bg-stake-dark border-x border-b border-stake-card/30">
          <div
            v-for="game in games"
            :key="getGameId(game)"
            class="
              p-4 border-b border-stake-card/30
              flex flex-col md:flex-row items-center gap-4
              transition-all
              relative
              group
              hover:-translate-y-[1px]
              hover:bg-[#B6FF00]/[0.12]
              hover:border-l-4 hover:border-l-[#B6FF00]/80
              hover:shadow-[0_0_18px_rgba(182,255,0,0.25)]
            "
          >
            <div class="flex flex-col items-center min-w-[70px]">
              <div class="text-[10px] font-bold text-stake-blue mb-0.5">{{ formatDate(game.commenceTime) }}</div>
              <div class="flex items-center gap-1 text-white text-sm font-bold">
                <Clock class="w-3 h-3 text-stake-text" /> {{ formatTime(game.commenceTime) }}
              </div>
            </div>

            <div 
              class="flex-1 w-full text-white cursor-pointer hover:text-stake-blue transition-colors"
              @click="router.push({ name: 'event-details', params: { id: getGameId(game) } })"
            >
              <div class="flex items-center gap-2 mb-2">
                <TeamLogo 
                    :teamName="game.homeTeam || ''" 
                    :remoteUrl="game.homeTeamLogo"  size="w-5 h-5"
                />
                <span class="font-bold text-sm">{{ game.homeTeam }}</span>
              </div>
              <div class="flex items-center gap-2">
                <TeamLogo 
                    :teamName="game.awayTeam || ''" 
                    :remoteUrl="game.awayTeamLogo"
                    size="w-5 h-5" 
                />
                <span class="font-bold text-sm">{{ game.awayTeam }}</span>
              </div>
            </div>

            <div class="flex gap-2 w-full md:w-auto">
              <button
                v-for="type in betTypesToShow"
                :key="type"
                @click="handleSelection(game, type)"
                :disabled="parseFloat(getOdd(game, type)) <= 1.0"
                :class="[
                  'flex-1 md:w-24 py-2 rounded flex flex-col items-center justify-center border transition-all active:scale-95 group',
                  parseFloat(getOdd(game, type)) <= 1.0 
                    ? 'opacity-50 cursor-not-allowed bg-stake-card border-transparent'
                    : getSelectedType(getGameId(game)) === type
                      ? 'bg-stake-blue border-stake-blue shadow-[0_0_10px_rgba(0,231,1,0.4)]'
                      : 'bg-stake-card border-transparent hover:border-stake-text/30'
                ]"
              >
                <span
                  :class="[
                    'text-[10px] font-bold mb-0.5',
                    getSelectedType(getGameId(game)) === type ? 'text-white' : 'text-stake-text'
                  ]"
                >
                  {{ type === '1' ? 'Casa' : type === '2' ? 'Fora' : 'Empate' }}
                </span>
                <span class="text-sm font-bold text-white">
                  {{ getOdd(game, type) }}
                </span>
              </button>
            </div>
          </div>
        </div>
      </div>

      <div ref="loadTrigger" class="h-20 flex items-center justify-center py-4">
          <div v-if="loadingMore" class="flex items-center gap-2 text-stake-blue font-bold text-xs uppercase animate-pulse">
              <div class="w-2 h-2 bg-stake-blue rounded-full animate-bounce"></div>
              Carregando mais jogos...
          </div>
          <div v-else-if="!hasMore && events.length > 0" class="text-stake-text text-[10px] uppercase opacity-50">
              Fim da lista de jogos
          </div>
      </div>

    </div>
  </div>
</template>