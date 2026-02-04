<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { Check, X } from 'lucide-vue-next';
import TournamentService from '../../services/Tournament/TournamentService';

// ✅ Importação dos Componentes de Navegação
import TournamentHeaderCarousel from '../../components/Tournament/TournamentHeaderCarousel.vue';
import TournamentRanking from '../../components/Tournament/TournamentRanking.vue';

const route = useRoute();
const router = useRouter();
const tournamentId = Number(route.params.id);

// --- ESTADOS DO CABEÇALHO (CARROSSEL) ---
const myTournaments = ref<any[]>([]);
const fantasyBalance = ref(0);
const userRank = ref(0);
const showRanking = ref(false);
const currentUser = ref('');

// --- ESTADOS DAS APOSTAS ---
interface BetSelection {
  homeTeam: string;
  awayTeam: string;
  marketName: string;
  selectionName: string;
  odds: number;
  status: string;
}

interface Bet {
  id: number;
  amount: number;
  totalOdds: number;
  potentialWin: number;
  status: string;
  placedAt: string;
  selections: BetSelection[];
  expanded?: boolean;
}

const bets = ref<Bet[]>([]);
const loading = ref(true);

onMounted(async () => {
  loadCurrentUser();
  
  // Carrega tudo em paralelo para ser rápido
  await Promise.all([
    loadHeaderData(),      // Dados do Carrossel (Lista, Saldo, Rank)
    loadMyBetsHistory()    // Dados da página (Histórico)
  ]);
});

// --- LÓGICA DO CABEÇALHO ---
const loadCurrentUser = () => {
    try {
        const stored = localStorage.getItem('user') || localStorage.getItem('user_data');
        if (stored) {
            const userData = JSON.parse(stored);
            const rawId = userData.Code || userData.code || userData.id || '';
            currentUser.value = String(rawId).replace(/\D/g, ''); 
        }
    } catch (e) { console.error("Erro user:", e); }
};

const loadHeaderData = async () => {
    try {
        // 1. Carrega dados do torneio atual (Saldo/Rank)
        if (currentUser.value) {
            const res = await TournamentService.getTournament(tournamentId, currentUser.value);
            if (res.data) {
                fantasyBalance.value = res.data.currentFantasyBalance ?? res.data.initialFantasyBalance ?? 0;
                userRank.value = res.data.rank || 0;
            }
            
            // 2. Carrega lista de torneios para o carrossel
            const listRes = await TournamentService.listTournaments(currentUser.value);
            if (listRes && listRes.data) {
                const joined = listRes.data.filter((t: any) => t.isJoined === true);
                myTournaments.value = joined.map((t: any) => ({
                    id: t.id || t.Id,
                    name: t.name || t.Name,
                    endDate: t.endDate || t.EndDate,
                    isJoined: true,
                    userRank: t.rank || t.Rank || t.userRank
                }));
            }
        }
    } catch (e) { console.error("Erro header:", e); }
};

const handleCarouselSelect = (newId: number) => {
    if (newId === tournamentId) return; // Já está aqui
    // Ao trocar de torneio, volta para o PLAY daquele torneio
    router.push(`/tournament/${newId}/play`);
};

// --- LÓGICA DE HISTÓRICO ---
const loadMyBetsHistory = async () => {
  try {
    const response = await TournamentService.getMyBets(tournamentId);
    bets.value = response.data.map((b: any) => ({
        id: b.id || b.Id,
        amount: b.amount || b.Amount,
        totalOdds: b.totalOdds || b.TotalOdds,
        potentialWin: b.potentialWin || b.PotentialWin,
        status: b.status || b.Status,
        placedAt: b.placedAt || b.PlacedAt,
        selections: (b.selections || b.Selections || []).map((s: any) => ({
            homeTeam: s.homeTeam || s.HomeTeam,
            awayTeam: s.awayTeam || s.AwayTeam,
            marketName: s.marketName || s.MarketName,
            selectionName: s.selectionName || s.SelectionName,
            odds: s.odds || s.Odds,
            status: s.status || s.Status
        })),
        expanded: (b.selections || b.Selections || []).length === 1
    }));
  } catch (error) {
    console.error("Erro bets:", error);
  } finally {
    loading.value = false;
  }
};

// --- HELPERS VISUAIS ---
const formatCurrency = (v: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v).replace('R$', '🪙');

const formatDate = (dateString: string) => {
  if (!dateString || dateString.startsWith('0001')) return '--/--';
  const date = new Date(dateString);
  return date.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
};

const formatMarketName = (name: string) => {
  if (!name) return '';
  const clean = name.toLowerCase().trim();
  if (clean === '1x2' || clean === 'match winner') return 'Resultado Final';
  return name;
};

const getSelectionDisplay = (sel: BetSelection) => {
    const rawSel = (sel.selectionName || '').toLowerCase().trim();
    if (rawSel === '1') return sel.homeTeam;
    if (rawSel === '2') return sel.awayTeam;
    if (rawSel === 'x' || rawSel === 'empate') return 'Empate';
    return sel.selectionName;
};

const getSelectionStatusClasses = (status: string) => {
    const s = (status || '').toLowerCase();
    if (s === 'won') return 'text-green-400 border-green-500/20';
    if (s === 'lost') return 'text-red-400 border-red-500/20';
    return 'text-blue-400 border-blue-400/20';
};

const getRealStatus = (bet: Bet): string => bet.status?.toLowerCase() || '';

const getBetStatusLabel = (bet: Bet) => {
  const status = getRealStatus(bet);
  if (status === 'won' || status === 'lost') return 'Finalizada';
  return 'Confirmada';
};

const getStatusClasses = (bet: Bet) => {
  const status = getRealStatus(bet);
  if (status === 'won') return 'text-green-400 border-green-500/30 bg-green-500/10';
  if (status === 'lost') return 'text-red-400 border-red-500/30 bg-red-500/10';
  return 'text-blue-400 border-blue-500/30 bg-blue-500/10';
};

const getBorderClass = (bet: Bet) => {
    const status = getRealStatus(bet);
    if (status === 'won') return 'border-green-500 border-l-4';
    if (status === 'lost') return 'border-red-500 border-l-4';
    return 'border-blue-500 border-l-4'; 
}

const getReturnLabel = (bet: Bet) => {
    const status = getRealStatus(bet);
    if (status === 'won') return 'Retorno Pago';
    if (status === 'lost') return 'Retorno';
    return 'Retorno Potencial';
}

const formatReturnValue = (bet: Bet) => {
    const status = getRealStatus(bet);
    if (status === 'lost') return '🪙 0,00';
    return formatCurrency(bet.potentialWin);
}

const getReturnValueClass = (bet: Bet) => {
    const status = getRealStatus(bet);
    if (status === 'won') return 'text-[#00ffb9] drop-shadow-[0_0_5px_rgba(0,255,185,0.3)]'; 
    if (status === 'lost') return 'text-slate-500 line-through decoration-1';
    return 'text-white';
}
</script>

<template>
  <div class="min-h-screen bg-[#0f172a] text-slate-200 font-sans pb-10 flex flex-col">
    
    <TournamentHeaderCarousel 
        :tournaments="myTournaments"
        :active-tournament-id="tournamentId"
        :fantasy-balance="fantasyBalance"
        :user-rank="userRank"
        @select="handleCarouselSelect"
        @open-history="null" 
        @open-ranking="showRanking = true"
        class="!mb-0" 
    />

    <div class="max-w-4xl mx-auto px-4 mt-2 w-full flex-1">
      
      <div class="flex items-center justify-between mb-3 border-b border-slate-800 pb-2">
          <div class="flex items-center gap-2"> <button @click="router.push(`/tournament/${tournamentId}/play`)" class="p-1.5 rounded-full bg-slate-800 hover:bg-slate-700 transition-colors text-slate-400 hover:text-white">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
                  </svg>
              </button>
              <h1 class="text-sm md:text-xl font-bold text-white tracking-wide uppercase">Minhas Apostas</h1>
          </div>
          <div class="text-[10px] md:text-xs text-slate-500 uppercase font-bold tracking-widest hidden md:block">
              Histórico Completo
          </div>
      </div>

      <div v-if="loading" class="flex flex-col items-center justify-center py-20 opacity-50">
        <div class="w-8 h-8 border-2 border-t-transparent border-blue-500 rounded-full animate-spin mb-3"></div>
        <span class="text-xs uppercase tracking-widest text-slate-500">Sincronizando...</span>
      </div>

      <div v-else class="space-y-4">
        
        <div v-if="bets.length === 0" class="text-center py-16 bg-[#1e293b]/30 rounded-lg border border-slate-700/30 border-dashed">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-10 w-10 mx-auto text-slate-600 mb-3 opacity-50" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
          </svg>
          <p class="text-sm text-slate-400 font-medium">Você ainda não apostou neste torneio.</p>
          <button @click="router.push(`/tournament/${tournamentId}/play`)" class="mt-4 px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold uppercase rounded shadow-lg transition-all">
              Fazer uma aposta
          </button>
        </div>

        <div v-for="bet in bets" :key="bet.id" 
             class="bg-[#1e293b] rounded-lg shadow-lg overflow-hidden border-l-[4px] transition-all hover:bg-[#263345]"
             :class="getBorderClass(bet)">
          
          <div class="px-4 py-3 flex justify-between items-start cursor-pointer border-b border-slate-700/50" 
               @click="bet.selections.length > 1 ? bet.expanded = !bet.expanded : null">
            
            <div class="flex flex-col gap-1">
              <div class="flex items-center gap-2">
                <span class="text-[10px] font-black uppercase tracking-wider px-2 py-0.5 rounded-sm shadow-sm"
                      :class="bet.selections.length > 1 ? 'bg-blue-500/20 text-blue-400' : 'bg-slate-600/20 text-slate-400'">
                  {{ bet.selections.length > 1 ? 'Múltipla' : 'Simples' }}
                </span>
                <span class="text-[11px] text-slate-500 font-mono tracking-wide">#{{ bet.id }}</span>
              </div>
              <span class="text-[11px] text-slate-400 ml-0.5">Apostado em: {{ formatDate(bet.placedAt) }}</span>
            </div>

            <div class="text-right">
              <div class="flex items-center justify-end gap-2 mb-1">
                <span class="text-[10px] font-bold uppercase border px-2 py-0.5 rounded shadow-sm" 
                      :class="getStatusClasses(bet)">
                  {{ getBetStatusLabel(bet) }}
                </span>
              </div>
              
              <div v-if="bet.selections.length > 1" class="text-[10px] text-slate-500 flex items-center justify-end gap-1 hover:text-white transition">
                {{ bet.expanded ? 'Fechar' : 'Ver' }} {{ bet.selections.length }} Jogos
                <span class="text-xs">{{ bet.expanded ? '▲' : '▼' }}</span>
              </div>
            </div>
          </div>

          <div v-show="bet.selections.length === 1 || bet.expanded" class="bg-[#0f172a]/40">
            <div v-for="(sel, idx) in bet.selections" :key="idx" 
                 class="p-4 border-b border-slate-700/30 last:border-0 relative group">
              
              <div class="absolute left-0 top-0 bottom-0 w-[3px]"
                   :class="{
                     'bg-green-500': sel.status === 'Won',
                     'bg-red-500': sel.status === 'Lost',
                     'bg-slate-700': sel.status === 'Pending' || sel.status === 'pending'
                   }"></div>

              <div class="flex justify-between items-center ml-2">
                <div class="flex-1 min-w-0 pr-4">
                  
                  <div class="text-[10px] text-slate-500 mb-1.5 font-mono flex items-center gap-1.5">
                    <span class="opacity-70">⚽</span> {{ sel.homeTeam }} x {{ sel.awayTeam }}
                  </div>

                  <div class="text-[13px] text-slate-200 font-medium mb-2 flex items-center flex-wrap leading-snug">
                    <span v-if="sel.status === 'Live'" class="mr-2 text-[9px] font-bold text-red-500 animate-pulse bg-red-500/10 px-1.5 py-0.5 rounded border border-red-500/20">LIVE</span>
                  </div>
                  
                  <div class="flex items-center flex-wrap gap-2 text-xs">
                    <span class="text-slate-500 font-bold uppercase text-[10px] tracking-wide bg-slate-800/50 px-1.5 py-0.5 rounded">
                        {{ formatMarketName(sel.marketName) }}
                    </span>

                    <div v-if="sel.status === 'Won'" class="flex items-center justify-center bg-green-500/10 w-4 h-4 rounded-full">
                        <Check class="w-3 h-3 text-green-500" stroke-width="3" />
                    </div>
                    <div v-else-if="sel.status === 'Lost'" class="flex items-center justify-center bg-red-500/10 w-4 h-4 rounded-full">
                        <X class="w-3 h-3 text-red-500" stroke-width="3" />
                    </div>
                    <span v-else class="text-slate-600">👉</span>
                    
                    <span class="font-bold border-b pb-0.5" :class="getSelectionStatusClasses(sel.status)">
                        {{ getSelectionDisplay(sel) }}
                    </span>
                  </div>
                </div>

                <div class="flex flex-col items-end justify-center h-full min-w-[60px]">
                  <span class="text-sm font-bold text-white bg-slate-700/50 px-2.5 py-1 rounded border border-slate-600/50 shadow-sm">
                    {{ sel.odds.toFixed(2) }}
                  </span>
                  <span v-if="sel.status === 'Won'" class="text-[9px] text-green-500 mt-1.5 font-bold tracking-wider flex items-center gap-1"><span class="w-1 h-1 bg-green-500 rounded-full"></span> VENCEU</span>
                  <span v-if="sel.status === 'Lost'" class="text-[9px] text-red-500 mt-1.5 font-bold tracking-wider flex items-center gap-1"><span class="w-1 h-1 bg-red-500 rounded-full"></span> PERDEU</span>
                </div>
              </div>
            </div>
          </div>

          <div class="px-4 py-3 bg-[#1e293b] flex items-center justify-between border-t border-slate-700/50">
            <div class="flex flex-col">
              <span class="text-[9px] text-slate-500 uppercase tracking-wide font-bold">Valor Apostado</span>
              <span class="text-sm font-bold text-white tracking-wide">{{ formatCurrency(bet.amount) }}</span>
            </div>

            <div class="text-right">
              <span class="text-[9px] uppercase tracking-wide block mb-0.5 font-bold" 
                    :class="bet.status?.toLowerCase() === 'lost' ? 'text-slate-500' : 'text-slate-400'">
                {{ getReturnLabel(bet) }}
              </span>
              <span class="text-base font-black tracking-wide" :class="getReturnValueClass(bet)">
                {{ formatReturnValue(bet) }}
              </span>
            </div>
          </div>

        </div>
      </div>
    </div>

    <TournamentRanking 
        v-if="showRanking" 
        :is-open="showRanking" 
        :tournament-id="tournamentId" 
        :current-user-id="currentUser" 
        @close="showRanking = false" 
    />

  </div>
</template>