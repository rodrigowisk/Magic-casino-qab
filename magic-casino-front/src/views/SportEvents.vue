<script setup lang="ts">
import { ref, watch, computed, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ArrowLeft, ChevronDown, ChevronRight, Calendar, AlertCircle } from 'lucide-vue-next';
import SportsService from '../services/SportsService';
import TeamLogo from '../components/TeamLogo.vue';
import { useBetStore, type BetType } from '../stores/useBetStore';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';

// Importação do Menu Híbrido
import TopSportsMenu from '../components/TopSportsMenu.vue';

// ✅ IMPORTAÇÃO DA NOVA FUNÇÃO DE BANDEIRAS
import { getFlag } from '../utils/flags';

// Interface flexível para lidar com PascalCase (C#) e camelCase (JS)
interface SportEvent {
  externalId?: string;
  ExternalId?: string; // Compatibilidade C#
  id?: string;
  Id?: string;
  homeTeam: string;
  HomeTeam?: string;
  awayTeam: string;
  AwayTeam?: string;
  commenceTime: string;
  CommenceTime?: string;
  league?: string;
  League?: string;
  countryCode?: string;
  homeTeamLogo?: string;
  awayTeamLogo?: string;

  rawOddsHome?: number;
  rawOddsDraw?: number;
  rawOddsAway?: number;
  RawOddsHome?: number;
  RawOddsDraw?: number;
  RawOddsAway?: number;
  [key: string]: any;
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
const selectedSport = ref<string>('all'); // Adicionado para controle do menu

const sportKey = computed(() => {
  const id = route.params?.id;
  return (Array.isArray(id) ? id[0] : id) || 'soccer';
});

// Sincroniza o sportKey com o selectedSport do menu
watch(sportKey, (newVal) => {
    selectedSport.value = newVal;
}, { immediate: true });

const handleMenuSelect = (key: string) => {
    // No pré-jogo, ao selecionar um esporte, navegamos para a rota dele
    if (key !== sportKey.value) {
        router.push({ name: 'sport-events', params: { id: key } });
    }
};

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

// 🔥 FUNÇÃO CRÍTICA: Prioriza o ID Externo e LIMPA ESPAÇOS
const getGameId = (game: SportEvent): string => {
  // Tenta pegar ExternalId (PascalCase do C#) ou externalId (camelCase) ou Id interno
  const candidates = [
    game.ExternalId,
    game.externalId,
    game.id,
    game.Id
  ];

  for (const cand of candidates) {
    if (cand !== null && cand !== undefined && String(cand).trim() !== '') {
       return String(cand).trim();
    }
  }

  // Fallback único (evita erros se vier sem ID)
  const home = game.homeTeam || game.HomeTeam || 'Home';
  const away = game.awayTeam || game.AwayTeam || 'Away';
  return `${home}_${away}`;
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

  const hTeam = game.homeTeam || game.HomeTeam || '';
  const aTeam = game.awayTeam || game.AwayTeam || '';
  const selectionName = type === '1' ? hTeam : type === '2' ? aTeam : 'Empate';
  const time = game.commenceTime || game.CommenceTime || new Date().toISOString();

  betStore.addOrReplaceSelection(
    gameId,
    hTeam,
    aTeam,
    selectionName,
    price,
    type,
    time
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

    // Abre as ligas automaticamente na primeira carga
    if (currentPage.value === 1) {
      newEvents.forEach(e => {
        const lg = e.league || e.League;
        if (lg) openLeagues.value.add(lg);
      });
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

  // 2. Conexão SignalR - CORRIGIDA E COM DEBUG
  try {
    const signalRUrl = "/gameHub";
    // console.log(`🔌 [PRÉ-JOGO] Conectando SignalR em: ${signalRUrl}`);

    connection.value = new HubConnectionBuilder()
      .withUrl(signalRUrl)
      .configureLogging(LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    // 🔥 OUVINTE 1: REMOÇÃO COM TRATAMENTO DE STRING E DEBUG
    connection.value.on("RemoveGames", (idsToRemove: any[]) => {
      if (!idsToRemove || idsToRemove.length === 0) return;

      // 1. Normaliza os IDs que chegaram (String + Trim)
      const idsSet = new Set(idsToRemove.map(id => String(id).trim()));

      if (events.value.length > 0) {
        // 2. Filtragem: Mantém apenas os jogos cujo ID NÃO está no set de remoção
        events.value = events.value.filter(game => {
          const gameId = getGameId(game).trim();
          const deveRemover = idsSet.has(gameId);

          if (deveRemover) {
            console.log(`❌ [RemoveGames] Removendo: ${game.homeTeam} (ID: ${gameId})`);
          }
          return !deveRemover;
        });
      }
    });

    // 🔥 OUVINTE 2 (REDE DE SEGURANÇA): GameWentLive
    // Se o jogo virou Live, ele deve sair desta tela de Pré-Jogo imediatamente.
    // Isso cobre casos onde o evento 'RemoveGames' falha ou se perde na reconexão.
    connection.value.on("GameWentLive", (liveGames: any[]) => {
        if (!liveGames || liveGames.length === 0) return;

        const liveIdsSet = new Set(liveGames.map(g => {
            // Tenta pegar o ID de várias formas possíveis do objeto que vem do hub
            return String(g.externalId || g.ExternalId || g.id || g.Id).trim();
        }));

        if (events.value.length > 0) {
            events.value = events.value.filter(game => {
                const gameId = getGameId(game).trim();
                const deveRemover = liveIdsSet.has(gameId);

                if (deveRemover) {
                    console.log(`🚀 [GameWentLive] Removendo do Pré-Jogo: ${game.homeTeam} (ID: ${gameId})`);
                }
                return !deveRemover;
            });
        }
    });

    // 🔇 OUVINTE 3: LiveOddsUpdate (Silencia updates de odds pois aqui é pré-jogo)
    connection.value.on("LiveOddsUpdate", () => {});

    await connection.value.start();
    // console.log("🟢 SignalR Conectado e Pronto!");

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

// --- COMPUTEDS AUXILIARES ---

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
    const lg = event.league || event.League;
    // Filtro por Liga (Query Param)
    if (leagueFilter.value && lg !== leagueFilter.value) return;

    // Filtro por Data
    const time = event.commenceTime || event.CommenceTime;
    if (dataSelecionada.value !== 'all' && time) {
      const dateObj = new Date(time);
      const eventDateLocal = getLocalDateString(dateObj);
      if (eventDateLocal !== dataSelecionada.value) return;
    }

    const leagueName = lg || 'Outros';
    if (!groups[leagueName]) groups[leagueName] = [];
    groups[leagueName].push(event);
  });
  return groups;
});

const formatTime = (d: string | undefined) => d ? new Date(d).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) : '--:--';
const formatDate = (d: string | undefined) => {
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
  <div class="space-y-0 pb-20 w-full relative">

    <TopSportsMenu 
        class="w-full z-50 sticky top-0 bg-[#0f172a] shadow-lg border-b border-white/5"
        :is-live="false"
        :selected-sport="selectedSport"
        @select="handleMenuSelect"
    />

    <div class="space-y-4 p-4 md:p-6">

        <div class="flex flex-col md:flex-row md:items-center justify-between gap-2 pb-1.5 border-b border-stake-dark/50">

          <div class="flex items-center gap-2">
            <button v-if="leagueFilter" @click="router.push({ name: 'sport-events', params: { id: sportKey } })"
              class="bg-stake-card p-1.5 rounded hover:bg-white/10 text-white transition">
              <ArrowLeft class="w-4 h-4" />
            </button>
            <div class="bg-blue-500/10 p-1 rounded-full animate-pulse">
              <Calendar class="w-4 h-4 text-blue-500" />
            </div>
            <h2 class="text-white text-sm font-bold uppercase italic whitespace-nowrap tracking-wide">
              {{ leagueFilter || traduzirEsporte(sportKey) }}
            </h2>
          </div>

          <div class="flex gap-1.5 overflow-x-auto no-scrollbar pb-1">
            <button v-for="data in datasFiltro" :key="data.valor" @click="dataSelecionada = data.valor"
              :class="['px-3 py-1 rounded-[4px] font-bold text-[10px] uppercase whitespace-nowrap transition', dataSelecionada === data.valor ? 'bg-stake-blue text-white shadow-sm' : 'bg-stake-card text-stake-text hover:bg-stake-hover']">
              {{ data.label }}
            </button>
          </div>
        </div>

        <div v-if="loading" class="space-y-3 pt-2">
          <div v-for="i in 5" :key="i" class="bg-stake-card h-20 rounded animate-pulse border border-white/5"></div>
        </div>

        <div v-else class="space-y-3 pt-2">

          <div v-if="Object.keys(groupedEvents).length === 0"
            class="py-10 text-center text-stake-text opacity-50 border border-dashed border-stake-text/20 rounded">
            <AlertCircle class="w-8 h-8 mb-2 opacity-50 mx-auto" />
            <p class="text-xs">Nenhum jogo encontrado para este filtro.</p>
            <button @click="fetchEvents(false)"
              class="mt-2 text-stake-blue font-bold text-[10px] hover:underline cursor-pointer uppercase">
              Atualizar Lista
            </button>
          </div>

          <div v-else v-for="(games, league) in groupedEvents" :key="league" class="rounded overflow-hidden">

            <div
              @click="openLeagues.has(String(league)) ? openLeagues.delete(String(league)) : openLeagues.add(String(league))"
              class="bg-stake-card/60 backdrop-blur-sm p-2 flex items-center justify-between border-l-2 border-stake-blue cursor-pointer hover:bg-stake-card transition-all select-none">
              <div class="flex items-center gap-2">

                <img :src="getFlag(league, games[0]?.countryCode)"
                  class="w-4 h-3 rounded-[1px] shadow-sm object-cover bg-black/20" @error="handleImageError" />

                <h3 class="text-white font-bold text-xs uppercase tracking-wide">{{ league }}</h3>

                <span v-if="countBetsInLeague(games) > 0"
                  class="bg-yellow-500 text-black text-[10px] font-black w-4 h-4 flex items-center justify-center rounded-full ml-1 animate-pulse">{{
                  countBetsInLeague(games) }}</span>
                <span v-else class="text-[9px] text-stake-text/60 font-bold bg-black/20 px-1.5 py-0.5 rounded-full">{{
                  games.length }}</span>
              </div>
              <component :is="openLeagues.has(String(league)) ? ChevronDown : ChevronRight"
                class="w-4 h-4 text-stake-text" />
            </div>

            <div v-show="openLeagues.has(String(league))" class="bg-stake-dark border-x border-b border-stake-card/20">
              <div v-for="game in games" :key="getGameId(game)"
                class="py-2 px-2 border-b border-white/5 flex flex-col md:flex-row items-center gap-2 transition-all hover:bg-[#B6FF00]/[0.02]">

                <div
                  class="flex flex-row md:flex-col items-center justify-start md:justify-center gap-2 md:gap-0.5 min-w-[60px] md:w-[60px] text-left md:text-center mr-2 md:mr-0 border-r md:border-r-0 md:border-b-0 border-white/10 pr-2 md:pr-0 h-full">
                  <div class="text-[9px] font-bold text-stake-blue leading-none">{{ formatDate(game.commenceTime ||
                    game.CommenceTime) }}</div>
                  <div class="text-white text-[10px] font-bold leading-none">{{ formatTime(game.commenceTime ||
                    game.CommenceTime) }}</div>
                </div>

                <div
                  class="flex-1 w-full text-white cursor-pointer hover:text-stake-blue transition-colors md:border-l md:border-white/5 md:pl-3"
                  @click="router.push({ name: 'event-details', params: { id: getGameId(game) } })">
                  <div class="flex flex-col gap-1.5 justify-center h-full">
                    <div class="flex items-center gap-2">
                      <TeamLogo :teamName="game.homeTeam || game.HomeTeam || ''" :remoteUrl="game.homeTeamLogo"
                        size="w-4 h-4" />
                      <span class="font-medium text-xs text-white/90 truncate">{{ game.homeTeam || game.HomeTeam }}</span>
                    </div>
                    <div class="flex items-center gap-2">
                      <TeamLogo :teamName="game.awayTeam || game.AwayTeam || ''" :remoteUrl="game.awayTeamLogo"
                        size="w-4 h-4" />
                      <span class="font-medium text-xs text-white/90 truncate">{{ game.awayTeam || game.AwayTeam }}</span>
                    </div>
                  </div>
                </div>

                <div class="flex gap-1 w-full md:w-auto mt-2 md:mt-0">
                  <button v-for="type in betTypesToShow" :key="type" @click.stop="handleSelection(game, type)"
                    :disabled="parseFloat(getOdd(game, type)) <= 1.0" :class="['flex-1 md:w-[70px] h-auto py-1.5 rounded-sm flex flex-col items-center justify-center border border-transparent transition-all active:scale-95 group relative overflow-hidden',
                      parseFloat(getOdd(game, type)) <= 1.0 ? 'opacity-50 cursor-not-allowed bg-stake-card border-transparent' : getSelectedType(getGameId(game)) === type ? 'bg-stake-blue shadow-[0_0_8px_rgba(0,146,255,0.4)]' : 'bg-stake-card hover:bg-stake-card/80']">

                    <span
                      :class="['text-[9px] font-bold uppercase mb-0.5 tracking-wide', getSelectedType(getGameId(game)) === type ? 'text-white' : 'text-stake-text/70']">
                      {{ type === '1' ? 'Casa' : type === '2' ? 'Fora' : 'Empate' }}
                    </span>

                    <span
                      :class="['text-xs font-bold transition-colors leading-none', getSelectedType(getGameId(game)) === type ? 'text-white' : 'text-white group-hover:text-stake-blue']">
                      {{ getOdd(game, type) }}
                    </span>
                  </button>
                </div>

              </div>
            </div>
          </div>

          <div ref="loadTrigger" class="h-16 flex items-center justify-center py-4">
            <div v-if="loadingMore" class="flex items-center gap-2 text-stake-blue font-bold text-[10px] uppercase animate-pulse">
              <div class="w-1.5 h-1.5 bg-stake-blue rounded-full animate-bounce"></div> Carregando...
            </div>
          </div>

        </div>
    </div>

  </div>
</template>

<style scoped>
/* Oculta scrollbar mas permite scroll */
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
.no-scrollbar {
  -ms-overflow-style: none;  /* IE and Edge */
  scrollbar-width: none;  /* Firefox */
}
</style>