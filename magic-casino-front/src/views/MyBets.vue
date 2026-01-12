<template>
  <div class="p-3 md:p-6 text-white min-h-screen bg-gray-900">
    <div class="max-w-4xl mx-auto">
      
      <div class="flex items-center justify-between mb-6">
        <h1 class="text-2xl font-bold text-blue-400 flex items-center gap-2">
          <span class="text-3xl">📜</span> Minhas Apostas
        </h1>
        <router-link to="/sports" class="text-sm px-3 py-1.5 bg-gray-800 rounded-md hover:bg-gray-700 transition border border-gray-700">
          ⬅ Voltar
        </router-link>
      </div>

      <div v-if="loading" class="text-center py-20">
        <div class="animate-spin rounded-full h-10 w-10 border-b-2 border-blue-500 mx-auto"></div>
      </div>

      <div v-else class="space-y-4">
        <div v-for="bet in bets" :key="bet.id" class="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden shadow-lg">
          
          <div class="p-4 bg-gray-850 border-b border-gray-700 flex flex-wrap justify-between items-center gap-4">
            <div class="flex items-center gap-6">
              <div>
                <span class="text-[10px] text-gray-500 block leading-none uppercase">Valor Apostado</span>
                <span class="font-bold text-white">{{ formatCurrency(bet.amount) }}</span>
              </div>
              <div>
                <span class="text-[10px] text-gray-500 block leading-none uppercase">ID Aposta</span>
                <span class="font-mono text-xs text-blue-400">#{{ bet.code }}</span>
              </div>
              <div>
                <span class="text-[10px] text-gray-500 block leading-none uppercase">Data Aposta</span>
                <span class="text-xs text-gray-300">{{ formatDate(bet.createdAt) }}</span>
              </div>
            </div>
            
            <div :class="getStatusClass(bet.status)" class="px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-wider">
              {{ translateStatus(bet.status) }}
            </div>
          </div>

          <div class="p-4 bg-gray-800/50">
            
            <div v-if="bet.selections.length > 1" class="flex justify-between items-center" :class="{'mb-4': bet.expanded}">
              <div class="flex items-center gap-3">
                <div class="bg-blue-600 text-[10px] px-2 py-0.5 rounded font-bold uppercase">Múltipla</div>
                <span class="text-sm font-bold text-white tracking-wide">APOSTA MÚLTIPLA ({{ bet.selections.length }} Jogos)</span>
                <span v-if="!bet.expanded" class="text-[10px] text-gray-500 italic hidden sm:inline">
                  Clique para detalhar os jogos
                </span>
              </div>
              <button @click="bet.expanded = !bet.expanded" class="flex items-center gap-2 text-xs font-bold text-gray-400 hover:text-white transition uppercase">
                {{ bet.expanded ? 'Ocultar Jogos ▲' : 'Ver Jogos ▼' }}
              </button>
            </div>

            <div v-if="bet.selections.length === 1 || bet.expanded" 
                 class="space-y-6 max-h-[450px] overflow-y-auto pr-3 custom-dark-scroll">
              <div v-for="(sel, idx) in bet.selections" :key="idx" 
                   class="pb-4 last:pb-0 border-b last:border-0 border-gray-700/50">
                
                <div class="flex justify-between items-start">
                  <div class="space-y-2 flex-1">
                    <div class="flex items-center justify-between max-w-[320px]">
                      <span class="text-sm font-bold text-gray-200 uppercase tracking-wide">{{ getTeams(sel.matchName).home }}</span>
                      <span v-if="sel.finalScore" class="text-sm font-black text-blue-400 bg-black/40 px-2 rounded min-w-[30px] text-center border border-blue-900/30">
                        {{ splitScore(sel.finalScore).home }}
                      </span>
                    </div>

                    <div class="flex items-center justify-between max-w-[320px]">
                      <span class="text-sm font-bold text-gray-200 uppercase tracking-wide">{{ getTeams(sel.matchName).away }}</span>
                      <span v-if="sel.finalScore" class="text-sm font-black text-blue-400 bg-black/40 px-2 rounded min-w-[30px] text-center border border-blue-900/30">
                        {{ splitScore(sel.finalScore).away }}
                      </span>
                    </div>

                    <div class="flex flex-wrap items-center gap-3 mt-3">
                      <p class="text-[11px] text-gray-400">
                        Resultado Final : <span class="text-blue-400 font-bold ml-1">{{ sel.selectionName }}</span>
                      </p>
                      <span class="text-[10px] text-gray-500 bg-gray-900/50 px-2 py-0.5 rounded border border-gray-700/30">
                        {{ formatDate(sel.commenceTime) }}
                      </span>
                    </div>
                  </div>

                  <div class="text-right ml-4">
                    <span class="text-[10px] text-gray-500 block uppercase mb-1">Odd</span>
                    <span class="text-xl font-black text-blue-400">{{ sel.odd.toFixed(2) }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="px-4 py-2 bg-gray-900/30 flex justify-end items-center gap-2 border-t border-gray-700/30">
            <span class="text-[10px] text-gray-500 font-bold uppercase">Retorno Possível:</span>
            <span class="text-green-400 font-bold text-lg">{{ formatCurrency(bet.potentialReturn) }}</span>
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
    bets.value = response.data.map((b: Bet) => ({ ...b, expanded: false }));
  } catch (error) {
    console.error("Erro ao carregar histórico:", error);
  } finally {
    loading.value = false;
  }
});

const getTeams = (matchName: string) => {
  const parts = matchName.split(/ x | vs | X /i);
  return {
    home: parts[0]?.trim() || 'Mandante',
    away: parts[1]?.trim() || 'Visitante'
  };
};

const splitScore = (score: string) => {
  const parts = score.split('-');
  return {
    home: parts[0] || '0',
    away: parts[1] || '0'
  };
};

const formatCurrency = (v: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v);

const formatDate = (dateString: string) => {
  if (!dateString || dateString.startsWith('0001')) return 'Pendente';
  const date = new Date(dateString);
  return date.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
};

const translateStatus = (s: string) => {
  const map: Record<string, string> = { 'pending': 'Pendente', 'confirmed': 'Confirmada', 'won': 'Ganhou ✅', 'lost': 'Perdeu ❌' };
  return map[s.toLowerCase()] || s;
};

const getStatusClass = (status: string) => {
  const s = status.toLowerCase();
  if (s === 'won') return 'bg-green-600 text-white';
  if (s === 'lost') return 'bg-red-600 text-white';
  return 'bg-blue-600 text-white';
};
</script>

<style>
/* ✅ ESTILO GLOBAL SEM SCOPED PARA FORÇAR A BARRA DARK EM TODOS OS NAVEGADORES */
.custom-dark-scroll {
  scrollbar-width: thin !important;
  scrollbar-color: #334155 #0f172a !important;
}

.custom-dark-scroll::-webkit-scrollbar {
  width: 6px !important;
  display: block !important;
}

.custom-dark-scroll::-webkit-scrollbar-track {
  background: #0f172a !important;
  border-radius: 10px !important;
}

.custom-dark-scroll::-webkit-scrollbar-thumb {
  background-color: #334155 !important;
  border-radius: 10px !important;
  border: 1px solid #0f172a !important;
}

.custom-dark-scroll::-webkit-scrollbar-thumb:hover {
  background-color: #475569 !important;
}
</style>

<style scoped>
.bg-gray-850 { background-color: #1a202c; }
</style>