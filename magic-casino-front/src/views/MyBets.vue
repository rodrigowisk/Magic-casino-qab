<template>
  <div class="min-h-screen bg-[#0f172a] text-slate-200 font-sans pb-10">
    
    <div class="sticky top-0 z-50 bg-[#0f172a] border-b border-slate-800/50 shadow-sm">
      <div class="max-w-4xl mx-auto px-4 h-16 flex items-center gap-4">
        
        <router-link to="/" class="text-slate-400 hover:text-white transition p-2 hover:bg-white/5 rounded-full border border-slate-700/30 bg-[#1e293b]/50 backdrop-blur-sm">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </router-link>
        
        <h1 class="text-lg font-bold text-white tracking-widest uppercase flex items-center gap-2.5">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-blue-500" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z"/>
            <path d="M13 5v2"/>
            <path d="M13 17v2"/>
            <path d="M13 11v2"/>
          </svg>
          Minhas Apostas
        </h1>

      </div>
    </div>

    <div class="max-w-4xl mx-auto px-4 mt-6">
      
      <div v-if="loading" class="flex flex-col items-center justify-center py-20 opacity-50">
        <div class="w-8 h-8 border-2 border-t-transparent border-blue-500 rounded-full animate-spin mb-3"></div>
        <span class="text-xs uppercase tracking-widest text-slate-500">Sincronizando...</span>
      </div>

      <div v-else class="space-y-4">
        
        <div v-if="bets.length === 0" class="text-center py-16 bg-[#1e293b]/30 rounded-lg border border-slate-700/30 border-dashed">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-10 w-10 mx-auto text-slate-600 mb-3 opacity-50" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
          </svg>
          <p class="text-sm text-slate-400 font-medium">Você ainda não tem apostas.</p>
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
                <span class="text-[11px] text-slate-500 font-mono tracking-wide">#{{ bet.code }}</span>
              </div>
              <span class="text-[11px] text-slate-400 ml-0.5">Apostado em: {{ formatDate(bet.createdAt) }}</span>
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
                     'bg-slate-700': sel.status === 'pending'
                   }"></div>

              <div class="flex justify-between items-center ml-2">
                <div class="flex-1 min-w-0 pr-4">
                  
                  <div class="text-[10px] text-slate-500 mb-1.5 font-mono flex items-center gap-1.5">
                    <span class="opacity-70">📅</span> {{ formatDate(sel.commenceTime) }}
                  </div>

                  <div class="text-[13px] text-slate-200 font-medium mb-2 flex items-center flex-wrap leading-snug">
                    
                    <span class="mr-2">{{ sel.matchName }}</span>

                    <div v-if="sel.finalScore" class="flex items-center gap-2">
                        <span class="text-slate-600">|</span>
                        <span class="font-mono text-white font-bold bg-slate-700/50 px-1.5 rounded text-xs">
                        {{ sel.finalScore }}
                        </span>
                    </div>
                    
                    <span v-if="sel.gameStatus === 'Live'" class="ml-2 text-[9px] font-bold text-red-500 animate-pulse bg-red-500/10 px-1.5 py-0.5 rounded border border-red-500/20">LIVE</span>
                  </div>
                  
                  <div class="flex items-center flex-wrap gap-1.5 text-xs">
                    <span class="text-slate-500 font-bold uppercase text-[10px] tracking-wide bg-slate-800/50 px-1.5 py-0.5 rounded">
                        {{ formatMarketName(sel.marketName) }}
                    </span>
                    <span class="text-slate-600">👉</span>
                    <span class="font-bold text-blue-400 border-b border-blue-400/20 pb-0.5">
                        {{ sel.selectionName }}
                    </span>
                  </div>

                </div>

                <div class="flex flex-col items-end justify-center h-full min-w-[60px]">
                  <span class="text-sm font-bold text-white bg-slate-700/50 px-2.5 py-1 rounded border border-slate-600/50 shadow-sm">
                    {{ sel.odd.toFixed(2) }}
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
              <span class="text-base font-black tracking-wide" 
                    :class="getReturnValueClass(bet)">
                {{ formatReturnValue(bet) }}
              </span>
            </div>
          </div>

        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import BetService from '../services/BetService';

interface BetSelection {
  matchName: string; 
  marketName: string; 
  selectionName: string; 
  odd: number; 
  status: string; 
  gameStatus?: string; 
  commenceTime: string;
  finalScore?: string;
  isWinner?: boolean;
}

interface Bet {
  id: string; 
  code: string; 
  amount: number; 
  totalOdd: number; 
  potentialReturn: number;
  status: string; 
  createdAt: string; 
  selections: BetSelection[]; 
  expanded?: boolean;
}

const bets = ref<Bet[]>([]);
const loading = ref(true);

onMounted(async () => {
  try {
    const response = await BetService.getMyBets();
    bets.value = response.data.map((b: Bet) => ({ 
        ...b, 
        expanded: b.selections.length === 1 
    }));
  } catch (error) {
    console.error("Erro ao carregar histórico:", error);
  } finally {
    loading.value = false;
  }
});

// --- HELPERS ---

const formatCurrency = (v: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v);

const formatDate = (dateString: string) => {
  if (!dateString || dateString.startsWith('0001')) return '--/--';
  const date = new Date(dateString);
  return date.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
};

// --- CORREÇÃO: TRADUÇÃO DE NOME DE MERCADO ---
const formatMarketName = (name: string) => {
  if (!name) return '';
  const clean = name.toLowerCase().trim();
  // Se for "1x2" ou variações, retorna o padrão visual do Cupom
  if (clean === '1x2' || clean === 'match winner' || clean === 'moneyline') {
    return 'Resultado Final';
  }
  return name;
};

// --- LÓGICA DE STATUS ---

const getRealStatus = (bet: Bet): string => {
    const mainStatus = bet.status?.toLowerCase() || '';

    if (mainStatus === 'won' || mainStatus === 'lost') return mainStatus;

    if (bet.selections && bet.selections.length > 0) {
        const allWon = bet.selections.every(s => s.status?.toLowerCase() === 'won');
        if (allWon) return 'won'; 

        const anyLost = bet.selections.some(s => s.status?.toLowerCase() === 'lost');
        if (anyLost) return 'lost'; 
    }

    return mainStatus;
};

const getBetStatusLabel = (bet: Bet) => {
  const status = getRealStatus(bet);
  if (status === 'won' || status === 'lost') {
      return 'Finalizada'; 
  }
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
    if (status === 'lost') return 'R$ 0,00';
    return formatCurrency(bet.potentialReturn);
}

const getReturnValueClass = (bet: Bet) => {
    const status = getRealStatus(bet);
    if (status === 'won') return 'text-[#00ffb9] drop-shadow-[0_0_5px_rgba(0,255,185,0.3)]'; 
    if (status === 'lost') return 'text-slate-500 line-through decoration-1';
    return 'text-white';
}
</script>