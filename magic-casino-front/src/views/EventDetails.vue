<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ArrowLeft, Clock, ChevronDown, ChevronRight, Trophy } from 'lucide-vue-next';
import SportsService from '../services/SportsService';
import TeamLogo from '../components/TeamLogo.vue';
import { useBetStore, type BetType } from '../stores/useBetStore';

/* =========================================================
   🔒 HELPERS
========================================================= */
const asString = (v: string | number | null | undefined): string => String(v ?? '');

/* =========================================================
   INTERFACES
========================================================= */
interface SportEvent {
  externalId?: string | number;
  id?: string | number;
  homeTeam: string;
  awayTeam: string;
  commenceTime: string;
  league?: string;
  odds: Array<{
    id?: string | number;
    marketName: string;
    outcomeName: string | number;
    price: number;
    point?: number;
  }>;
}

/* =========================================================
   STATE
========================================================= */
const route = useRoute();
const router = useRouter();
const betStore = useBetStore();

const event = ref<SportEvent | null>(null);
const loading = ref(true);
const expandedMarkets = ref<Set<string>>(new Set());

/* =========================================================
   UI HELPERS
========================================================= */
const toggleMarket = (marketName: string) => {
  if (expandedMarkets.value.has(marketName)) {
    expandedMarkets.value.delete(marketName);
  } else {
    expandedMarkets.value.add(marketName);
  }
};

// 🌟 TRADUÇÃO E AGRUPAMENTO DE MERCADOS
const translateMarket = (key: string) => {
  const k = asString(key).toLowerCase().trim();

  // Resultado Final
  if (k === '1x2' || k === 'full time result' || k === 'match winner' || k === 'money line')
    return 'Resultado Final';

  // Dupla Chance (Separação Rigorosa)
  if (k.includes('double chance')) {
      if (k.includes('1st') || k.includes('half')) return 'Dupla Hipótese - 1º Tempo'; 
      return 'Dupla Hipótese'; // Somente jogo completo
  }

  // Gols
  if (k.includes('goals over under') || k.includes('total goals')) {
      if (k.includes('1st') || k.includes('half')) return 'Gols Mais/Menos - 1º Tempo';
      return 'Gols Mais/Menos'; 
  }

  // Outros
  if (k.includes('handicap result') || k.includes('3-way handicap')) return 'Handicap - Resultado';
  if (k.includes('handicap')) return 'Handicap Asiático';
  if (k.includes('draw no bet')) return 'Empate Anula Aposta';
  if (k.includes('both teams to score') || k === 'btts') return 'Ambos Marcam';
  if (k.includes('result/both teams to score')) return 'Resultado/Ambos Marcam';
  if (k.includes('correct score')) return 'Placar Correto';
  if (k.includes('half time/full time') || k.includes('ht/ft')) return 'Intervalo/Final de Jogo';
  if (k.includes('half time')) return 'Intervalo';
  if (k.includes('to qualify')) return 'Para se Classificar';
  if (k.includes('method of victory') || k.includes('winning method')) return 'Método de Vitória';

  return key;
};

/* =========================================================
   API
========================================================= */
const fetchDetails = async () => {
  try {
    const data = await SportsService.getEventDetails(route.params.id as string);
    event.value = data as SportEvent;
  } catch (err) {
    console.error('Erro ao carregar detalhes', err);
  } finally {
    loading.value = false;
  }
};

/* =========================================================
   COMPUTEDS (AGRUPAMENTO)
========================================================= */
const groupedMarkets = computed<Record<string, any[]>>(() => {
  if (!event.value?.odds) return {};

  const groups: Record<string, any[]> = {};

  event.value.odds.forEach((odd) => {
    const name = translateMarket(odd.marketName);
    
    // Filtro de Segurança: Dupla Hipótese não pode ter "Sim" ou "Não"
    if (name === 'Dupla Hipótese') {
        const outName = asString(odd.outcomeName).toLowerCase();
        if (outName === 'yes' || outName === 'no' || outName === 'sim' || outName === 'não') {
            return; // Ignora lixo
        }
    }

    if (!groups[name]) groups[name] = [];
    groups[name].push(odd);
  });

  // Ordenação do Resultado Final
  if (groups['Resultado Final']) {
    groups['Resultado Final'].sort((a, b) => {
      const getScore = (outcome: string) => {
        const out = outcome.toLowerCase();
        if (out === event.value!.homeTeam.toLowerCase()) return 1;
        if (['draw', 'x', 'empate'].includes(out)) return 2;
        if (out === event.value!.awayTeam.toLowerCase()) return 3;
        return 4;
      };
      return getScore(asString(a.outcomeName)) - getScore(asString(b.outcomeName));
    });
  }

  // Ordenação da Dupla Hipótese (1X, 12, X2)
  if (groups['Dupla Hipótese']) {
      groups['Dupla Hipótese'].sort((a, b) => {
          const nameA = asString(a.outcomeName);
          const nameB = asString(b.outcomeName);
          // Tenta ordenar: Casa ou Empate, Casa ou Fora, Empate ou Fora
          if (nameA.includes(event.value!.homeTeam) && nameA.includes('Empate')) return -1;
          if (nameB.includes(event.value!.homeTeam) && nameB.includes('Empate')) return 1;
          return 0;
      });
  }

  return groups;
});

watch(groupedMarkets, (newVal) => {
  Object.keys(newVal).forEach((k) => expandedMarkets.value.add(k));
});

/* =========================================================
   HELPERS - MÉTODO DE VITÓRIA (TABELA)
========================================================= */
const getMethodTable = (odds: any[]) => {
    const table: any = {};
    const methodsOrder = ['Tempo Regulamentar', 'Prorrogação', 'Pênaltis'];

    odds.forEach(odd => {
        let name = asString(odd.outcomeName); 
        let method = "Vencer";
        let teamType = ""; 

        if (event.value) {
            if (name.includes(event.value.homeTeam)) {
                // Remove o time para sobrar o método (ex: "Tempo Regulamentar - Time" -> "Tempo Regulamentar")
                method = name.replace(event.value.homeTeam, "").replace(" - ", "").trim();
                teamType = 'home';
            } else if (name.includes(event.value.awayTeam)) {
                method = name.replace(event.value.awayTeam, "").replace(" - ", "").trim();
                teamType = 'away';
            }
        }

        // Se a string ficou vazia ou estranha, usa o original
        if (!method || method.length < 3) method = name;
        
        // Normaliza chaves
        if (!table[method]) table[method] = { method };
        table[method][teamType] = odd;
    });
    
    // Retorna ordenado
    const sorted: any = {};
    methodsOrder.forEach(m => { if(table[m]) sorted[m] = table[m]; });
    Object.keys(table).forEach(k => { if(!sorted[k]) sorted[k] = table[k]; });
    
    return sorted;
};

/* =========================================================
   BET LOGIC & FORMATAÇÃO
========================================================= */
const formatOutcomeName = (val: string | number) => {
  let str = asString(val);
  const lower = str.toLowerCase();

  if (['draw', 'x'].includes(lower)) return 'Empate';
  if (lower === 'yes') return 'Sim';
  if (lower === 'no') return 'Não';
  
  // Tradução Específica da Dupla Hipótese
  str = str.replace(/ or /gi, ' ou ')
           .replace(/Draw/gi, 'Empate')
           .replace(/Home/gi, 'Casa')
           .replace(/Away/gi, 'Fora');
  
  return str;
};

const getBetTypeForSlip = (odd: any): string => {
  const marketTranslated = translateMarket(odd.marketName);

  if (marketTranslated === 'Resultado Final') {
    const outcome = asString(odd.outcomeName).toLowerCase();
    const home = asString(event.value?.homeTeam).toLowerCase();
    const away = asString(event.value?.awayTeam).toLowerCase();

    if (outcome === home) return '1';
    if (outcome === away) return '2';
    if (['draw', 'x', 'empate'].includes(outcome)) return 'X';
  }

  return marketTranslated;
};

const getSelectionId = (gameId: string | number, odd: any): string => {
  const gid = asString(gameId);
  const type = getBetTypeForSlip(odd);

  if (['1', '2', 'X'].includes(type)) return gid;

  return `${gid}_${asString(odd.marketName)}_${asString(odd.outcomeName)}`;
};

const isSelected = (odd: any): boolean => {
  if (!event.value) return false;
  const gameId = asString(event.value.externalId || event.value.id);
  const uniqueId = getSelectionId(gameId, odd);
  const found = betStore.selections.find((s) => s.id === uniqueId);
  if (!found) return false;
  const myType = getBetTypeForSlip(odd);
  if (['1', '2', 'X'].includes(myType)) return found.type === myType;
  return true;
};

const countSelectedInMarket = (odds: any[]) => {
    let count = 0;
    odds.forEach(odd => { if (isSelected(odd)) count++; });
    return count;
};

const handleSelection = (odd: any) => {
  if (!event.value) return;

  const gameId = asString(event.value.externalId || event.value.id);
  const betTypeString = getBetTypeForSlip(odd);
  const betType = betTypeString as BetType;
  const uniqueId = getSelectionId(gameId, odd);

  if (isSelected(odd)) {
    betStore.removeSelection(uniqueId);
    return;
  }

  if (['1', '2', 'X'].includes(betTypeString)) {
    betStore.selections
      .filter((s) => s.id === gameId)
      .forEach((s) => betStore.removeSelection(s.id));
  }
  else {
      betStore.selections.forEach((s) => {
          if (String(s.id).startsWith(String(gameId))) {
              const selId = String(s.id);
              if (selId.includes(`_${asString(odd.marketName)}_`)) {
                  betStore.removeSelection(s.id);
              }
          }
      });
  }

  betStore.addOrReplaceSelection(
    uniqueId,
    event.value.homeTeam,
    event.value.awayTeam,
    asString(odd.outcomeName),
    Number(odd.price),
    betType,
    event.value.commenceTime
  );
};

/* =========================================================
   FORMATTERS
========================================================= */
const formatDate = (d: string) => {
  if (!d) return '---';
  const date = new Date(d);
  return `${date.getDate().toString().padStart(2, '0')}/${(date.getMonth() + 1).toString().padStart(2, '0')}`;
};

const formatTime = (d: string) =>
  d ? new Date(d).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) : '--:--';

onMounted(fetchDetails);
</script>

<template>
  <div class="space-y-4 pb-20 max-w-5xl mx-auto p-4 md:p-0">
    <div class="flex items-center gap-4 pb-2">
      <button
        @click="router.back()"
        class="bg-stake-card p-2 rounded hover:bg-white/10 text-white transition-colors border border-transparent hover:border-stake-text/30"
      >
        <ArrowLeft class="w-5 h-5" />
      </button>

      <h2 class="text-white text-xl font-bold uppercase italic whitespace-nowrap">
        <span class="text-stake-blue">#</span> {{ event?.league || 'Detalhes do Evento' }}
      </h2>
    </div>

    <div v-if="loading" class="text-stake-text animate-pulse pl-2 font-bold uppercase tracking-widest text-sm">
      Carregando mercados...
    </div>

    <div v-else-if="event" class="space-y-4">
      
      <div class="bg-stake-card border-l-4 border-stake-blue rounded p-6 shadow-lg relative overflow-hidden group">
        <div class="absolute top-0 right-0 p-4 opacity-5 pointer-events-none group-hover:opacity-10 transition-opacity">
            <Clock class="w-32 h-32 text-stake-blue" />
        </div>

        <div class="flex flex-col md:flex-row items-center gap-6 justify-between relative z-10">
          <div class="flex-1 flex flex-col items-center md:items-end text-center md:text-right gap-2">
            <TeamLogo :teamName="event.homeTeam" size="w-12 h-12 md:w-16 md:h-16" />
            <h1 class="text-white font-black text-lg md:text-2xl uppercase leading-tight">
              {{ event.homeTeam }}
            </h1>
          </div>

          <div class="flex flex-col items-center min-w-[100px]">
            <div class="text-3xl font-black text-stake-blue italic opacity-80 select-none">VS</div>
            <div class="mt-2 flex flex-col items-center">
              <span class="text-stake-blue font-bold text-xs uppercase">
                {{ formatDate(event.commenceTime) }}
              </span>
              <div class="flex items-center gap-1 text-white font-bold text-sm bg-black/20 px-3 py-1 rounded-full mt-1 border border-white/5">
                <Clock class="w-3.5 h-3.5 text-stake-text" />
                {{ formatTime(event.commenceTime) }}
              </div>
            </div>
          </div>

          <div class="flex-1 flex flex-col items-center md:items-start text-center md:text-left gap-2">
            <TeamLogo :teamName="event.awayTeam" size="w-12 h-12 md:w-16 md:h-16" />
            <h1 class="text-white font-black text-lg md:text-2xl uppercase leading-tight">
              {{ event.awayTeam }}
            </h1>
          </div>
        </div>
      </div>

      <div v-for="(odds, marketName) in groupedMarkets" :key="marketName" class="rounded overflow-hidden">
        
        <div
          @click="toggleMarket(marketName)"
          class="bg-stake-card p-3 flex items-center justify-between border-l-4 border-stake-blue cursor-pointer hover:brightness-110 transition-all select-none group"
        >
          <div class="flex items-center gap-3">
            <Trophy class="w-4 h-4 text-stake-blue group-hover:scale-110 transition-transform" />
            <h3 class="text-white font-bold text-sm uppercase tracking-wide">
              {{ marketName }}
            </h3>
            
            <span 
                v-if="countSelectedInMarket(odds) > 0" 
                class="ml-2 bg-yellow-500 text-black text-[10px] font-black w-5 h-5 flex items-center justify-center rounded-full shadow-[0_0_10px_rgba(234,179,8,0.5)] scale-100 animate-in zoom-in"
            >
                {{ countSelectedInMarket(odds) }}
            </span>
          </div>
          
          <component :is="expandedMarkets.has(marketName) ? ChevronDown : ChevronRight" class="w-5 h-5 text-stake-text" />
        </div>

        <div v-show="expandedMarkets.has(marketName)" class="bg-stake-dark p-4 border-x border-b border-stake-card/30">
          
          <div v-if="marketName === 'Método de Vitória'" class="flex flex-col gap-2">
              <div class="grid grid-cols-3 text-xs text-stake-text font-bold uppercase mb-1 px-2">
                  <div class="text-left">Método</div>
                  <div class="text-center">{{ event.homeTeam }}</div>
                  <div class="text-center">{{ event.awayTeam }}</div>
              </div>
              
              <div v-for="(row, method) in getMethodTable(odds)" :key="method" class="grid grid-cols-3 items-center gap-2 border-b border-white/5 pb-2 last:border-0">
                  <div class="text-xs text-white font-bold capitalize truncate pr-2">{{ method }}</div>
                  
                  <button v-if="row.home" @click="handleSelection(row.home)" 
                      :class="['py-2 rounded text-center transition-all', isSelected(row.home) ? 'bg-stake-blue text-white' : 'bg-stake-card hover:bg-white/5 text-stake-blue']">
                      <span class="font-black text-sm">{{ Number(row.home.price).toFixed(2) }}</span>
                  </button>
                  <div v-else class="text-center text-white/10">-</div>

                  <button v-if="row.away" @click="handleSelection(row.away)" 
                      :class="['py-2 rounded text-center transition-all', isSelected(row.away) ? 'bg-stake-blue text-white' : 'bg-stake-card hover:bg-white/5 text-stake-blue']">
                      <span class="font-black text-sm">{{ Number(row.away.price).toFixed(2) }}</span>
                  </button>
                  <div v-else class="text-center text-white/10">-</div>
              </div>
          </div>

          <div v-else class="grid grid-cols-2 md:grid-cols-3 gap-3">
            <button
              v-for="odd in odds"
              :key="odd.id || odd.outcomeName"
              @click="handleSelection(odd)"
              :class="[
                'py-3 px-3 rounded flex flex-col items-center border transition-all relative overflow-hidden group/btn active:scale-95',
                isSelected(odd)
                  ? 'bg-stake-blue border-stake-blue shadow-[0_0_15px_rgba(0,231,1,0.4)]'
                  : 'bg-stake-card border-transparent hover:border-stake-text/30 hover:bg-[#ff7a00]/[0.05]'
              ]"
            >
              <div class="flex flex-col items-center text-center gap-0.5 z-10 w-full">
                <span :class="[
                    'text-[10px] font-bold uppercase truncate w-full px-1',
                    isSelected(odd) ? 'text-white' : 'text-stake-text group-hover/btn:text-white'
                ]">
                    {{ formatOutcomeName(odd.outcomeName + '') }}
                </span>

                <span v-if="odd.point" class="text-[9px] font-bold text-stake-blue/90">
                    {{ odd.point > 0 ? '+' : '' }}{{ odd.point }}
                </span>

                <span class="text-base font-black text-white mt-0.5">
                    {{ Number(odd.price).toFixed(2) }}
                </span>
              </div>
            </button>
          </div>

        </div>
      </div>
    </div>
  </div>
</template>