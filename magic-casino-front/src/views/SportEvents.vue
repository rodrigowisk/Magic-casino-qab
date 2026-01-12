<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ChevronDown, ChevronRight, Clock, ArrowLeft } from 'lucide-vue-next';
import SportsService from '../services/SportsService';
import TeamLogo from '../components/TeamLogo.vue';
import { useBetStore, type BetType } from '../stores/useBetStore';

// Interface rigorosa para evitar erros de undefined
interface SportEvent {
  id?: string;
  sportsEventId?: string;
  eventId?: string;
  homeTeam: string;
  awayTeam: string;
  commenceTime: string; // Definido como string obrigatória no mapeamento
  league?: string;
  odds: Array<{
    outcomeName: string;
    finalPrice: number | string;
  }>;
}

const route = useRoute();
const router = useRouter();
const betStore = useBetStore();

const loading = ref(true);
const events = ref<SportEvent[]>([]);
const openLeagues = ref<Set<string>>(new Set());
const dataSelecionada = ref<string>('all');

const sportKey = computed(() => (route.params.id as string) || '');
const leagueFilter = computed(() => route.query.league as string | undefined);

// --- IDENTIDADE ÚNICA DO JOGO ---
const getGameId = (game: SportEvent): string => {
  const id = game.id ?? game.sportsEventId ?? game.eventId;
  if (id) return String(id);
  
  const home = (game.homeTeam || '').trim();
  const away = (game.awayTeam || '').trim();
  const time = (game.commenceTime || '').trim();
  return `${home}__${away}__${time}`;
};

// --- LÓGICA PARA ENCONTRAR A ODD ---
const getOdd = (game: SportEvent, type: BetType): string => {
  if (!game.odds || !Array.isArray(game.odds)) return '0.00';

  const hTeam = (game.homeTeam || '').toLowerCase();
  const aTeam = (game.awayTeam || '').toLowerCase();

  let foundOdd: any;

  if (type === '1') {
    foundOdd = game.odds.find((o) => (o.outcomeName || '').toLowerCase() === hTeam);
  } else if (type === '2') {
    foundOdd = game.odds.find((o) => (o.outcomeName || '').toLowerCase() === aTeam);
  } else {
    foundOdd = game.odds.find((o) => 
      ['draw', 'empate', 'x', 'tie'].includes((o.outcomeName || '').toLowerCase())
    );
  }

  const fp = foundOdd?.finalPrice;
  return fp ? Number(fp).toFixed(2) : '0.00';
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

// --- DATAS E FILTROS ---
const datasFiltro = computed(() => {
  const opcoes = [{ label: 'TODAS', valor: 'all' }];
  const hoje = new Date();
  for (let i = 0; i < 4; i++) {
    const d = new Date();
    d.setDate(hoje.getDate() + i);
    let label = i === 0 ? 'Hoje' : i === 1 ? 'Amanhã' : d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
    opcoes.push({ label: label.toUpperCase(), valor: d.toISOString().split('T')[0] || '' });
  }
  return opcoes;
});

const traduzirEsporte = (key: string) => {
  if (!key) return '';
  const k = key.toLowerCase();
  if (k.includes('soccer')) return 'Futebol';
  if (k.includes('basket')) return 'Basquete';
  if (k.includes('mma')) return 'MMA';
  return key.toUpperCase();
};

const getFlagUrl = (leagueName: string) => {
  const name = (leagueName || '').toLowerCase();
  const map: Record<string, string> = { brazil: 'br', england: 'gb', spain: 'es', italy: 'it', germany: 'de', france: 'fr' };
  const found = Object.keys(map).find(c => name.includes(c));
  return `/images/flags/${found ? map[found] : 'un'}.svg`;
};

const fetchEvents = async () => {
  if (!sportKey.value) return;
  loading.value = true;
  try {
    const data = await SportsService.getEventsBySport(sportKey.value);
    events.value = (data || []) as SportEvent[];
    openLeagues.value.clear();
    events.value.forEach(e => { if(e.league) openLeagues.value.add(e.league); });
  } catch (e) {
    console.error(e);
  } finally {
    loading.value = false;
  }
};

watch(() => route.params.id, fetchEvents, { immediate: true });

// ✅ CORREÇÃO TS2322 NA LINHA 111: Garantindo fallbacks para strings
const groupedEvents = computed(() => {
  const groups: Record<string, SportEvent[]> = {};
  events.value.forEach(event => {
    if (leagueFilter.value && event.league !== leagueFilter.value) return;
    
    const rawTime = event.commenceTime || '';
    const eventDate = rawTime.split('T')[0] || ''; // Garante que nunca seja undefined
    
    if (dataSelecionada.value !== 'all' && eventDate !== dataSelecionada.value) return;
    
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
  return date.getDate().toString().padStart(2,'0') + '/' + (date.getMonth()+1).toString().padStart(2,'0');
};
</script>

<template>
  <div class="space-y-4 pb-20"> 
    <div class="flex flex-col md:flex-row md:items-center gap-4 pb-2">
      <div class="flex items-center gap-3">
        <button v-if="leagueFilter" @click="router.push({ name: 'sport-events', params: { id: sportKey } })" class="bg-stake-card p-2 rounded hover:bg-white/10 text-white">
          <ArrowLeft class="w-5 h-5"/>
        </button>
        <h2 class="text-white text-xl font-bold uppercase italic whitespace-nowrap">
          <span class="text-stake-blue">#</span> {{ leagueFilter || traduzirEsporte(sportKey) }}
        </h2>
      </div>

      <div class="flex gap-2 overflow-x-auto no-scrollbar pb-1">
        <button v-for="data in datasFiltro" :key="data.valor" @click="dataSelecionada = data.valor"
          :class="['px-4 py-1.5 rounded font-bold text-[11px] uppercase whitespace-nowrap', dataSelecionada === data.valor ? 'bg-stake-blue text-white' : 'bg-stake-card text-stake-text hover:bg-stake-hover']">
          {{ data.label }}
        </button>
      </div>
    </div>

    <div v-if="loading" class="text-stake-text animate-pulse pl-2">Carregando jogos...</div>

    <div v-else class="space-y-4">
      <div v-if="Object.keys(groupedEvents).length === 0" class="py-10 text-center text-stake-text opacity-50 border border-dashed border-stake-text/20 rounded">
        Nenhum jogo encontrado para esta data.
      </div>

      <div v-else v-for="(games, league) in groupedEvents" :key="league" class="rounded overflow-hidden">
        <div @click="openLeagues.has(String(league)) ? openLeagues.delete(String(league)) : openLeagues.add(String(league))"
             class="bg-stake-card p-3 flex items-center justify-between border-l-4 border-stake-blue cursor-pointer hover:brightness-110 transition-all">
          <div class="flex items-center gap-3">
            <img :src="getFlagUrl(String(league))" class="w-5 h-3.5 rounded-sm shadow-sm" />
            <h3 class="text-white font-bold text-sm uppercase">{{ league }}</h3>
          </div>
          <component :is="openLeagues.has(String(league)) ? ChevronDown : ChevronRight" class="w-5 h-5 text-stake-text"/>
        </div>

        <div v-show="openLeagues.has(String(league))" class="bg-stake-dark border-x border-b border-stake-card/30">
          <div v-for="game in games" :key="getGameId(game)" class="p-4 border-b border-stake-card/30 flex flex-col md:flex-row items-center gap-4 hover:bg-white/5 transition-colors">
            
            <div class="flex flex-col items-center min-w-[70px]">
              <div class="text-[10px] font-bold text-stake-blue mb-0.5">{{ formatDate(game.commenceTime) }}</div>
              <div class="flex items-center gap-1 text-white text-sm font-bold">
                <Clock class="w-3 h-3 text-stake-text"/> {{ formatTime(game.commenceTime) }}
              </div>
            </div>

            <div class="flex-1 w-full text-white">
              <div class="flex items-center gap-2 mb-2">
                <TeamLogo :teamName="game.homeTeam || ''" size="w-5 h-5" />
                <span class="font-bold text-sm">{{ game.homeTeam }}</span>
              </div>
              <div class="flex items-center gap-2">
                <TeamLogo :teamName="game.awayTeam || ''" size="w-5 h-5" />
                <span class="font-bold text-sm">{{ game.awayTeam }}</span>
              </div>
            </div>

            <div class="flex gap-2 w-full md:w-auto">
              <button v-for="type in (['1','X','2'] as BetType[])" :key="type" 
                @click="handleSelection(game, type)"
                :class="[
                  'flex-1 md:w-24 py-2 rounded flex flex-col items-center justify-center border transition-all active:scale-95 group',
                  getSelectedType(getGameId(game)) === type 
                    ? 'bg-stake-blue border-stake-blue shadow-[0_0_10px_rgba(0,231,1,0.4)]' 
                    : 'bg-stake-card border-transparent hover:border-stake-text/30'
                ]"
              >
                <span :class="['text-[10px] font-bold mb-0.5', getSelectedType(getGameId(game)) === type ? 'text-white' : 'text-stake-text']">
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
    </div>
  </div>
</template>