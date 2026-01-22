<template>
  <div class="p-3 md:p-6 text-white min-h-screen bg-gray-900">
    <div class="max-w-4xl mx-auto">
      
      <div class="flex items-center justify-between mb-6">
        <h1 class="text-2xl font-bold text-blue-400 flex items-center gap-2">
          <span class="text-3xl">📜</span> Minhas Apostas
        </h1>
        <router-link to="/" class="text-sm px-3 py-1.5 bg-gray-800 rounded-md hover:bg-gray-700 transition border border-gray-700">
          ⬅ Voltar
        </router-link>
      </div>

      <div v-if="loading" class="text-center py-20">
        <div class="animate-spin rounded-full h-10 w-10 border-b-2 border-blue-500 mx-auto"></div>
      </div>

      <div v-else class="space-y-4">
        <div v-for="bet in bets" :key="bet.id" 
             class="bg-gray-800 rounded-lg border overflow-hidden shadow-lg transition-all duration-300"
             :class="getBorderClass(bet.status)">
          
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
                <span class="text-[10px] text-gray-500 block leading-none uppercase">Data</span>
                <span class="text-xs text-gray-300">{{ formatDate(bet.createdAt) }}</span>
              </div>
            </div>
            
            <div :class="getStatusClass(bet.status)" class="px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-wider shadow-sm">
              {{ translateStatus(bet.status) }}
            </div>
          </div>

          <div class="p-4 bg-gray-800/50">
            
            <div v-if="bet.selections.length > 1" class="flex justify-between items-center" :class="{'mb-4': bet.expanded}">
              <div class="flex items-center gap-3">
                <div class="bg-blue-600 text-[10px] px-2 py-0.5 rounded font-bold uppercase shadow-sm">Múltipla</div>
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
                 class="space-y-4 max-h-[450px] overflow-y-auto pr-2 custom-dark-scroll">
              
              <div v-for="(sel, idx) in bet.selections" :key="idx" 
                   class="pb-3 last:pb-0 border-b last:border-0 border-gray-700/50 relative pl-2 transition-all duration-300"
                   :class="{'opacity-60 grayscale': sel.status === 'Lost', 'opacity-100': sel.status === 'Won'}">
                
                <div class="absolute left-0 top-1 bottom-4 w-1 rounded-full"
                     :class="{
                        'bg-green-500': sel.status === 'Won',
                        'bg-red-500': sel.status === 'Lost',
                        'bg-gray-600': sel.status === 'pending'
                     }">
                </div>

                <div class="flex justify-between items-start">
                  <div class="space-y-1 flex-1">
                    <div class="flex items-center gap-2">
                        <div class="flex flex-col text-sm font-bold text-gray-200 uppercase tracking-wide leading-tight">
                            <span>{{ getTeams(sel.matchName).home }}</span>
                            <span>{{ getTeams(sel.matchName).away }}</span>
                        </div>
                        
                        <div v-if="sel.finalScore" class="flex flex-col items-center justify-center bg-black/40 border border-white/10 rounded px-1.5 py-0.5 ml-2">
                            <span class="text-xs font-bold text-blue-400 leading-none">{{ splitScore(sel.finalScore).home }}</span>
                            <span class="text-xs font-bold text-blue-400 leading-none">{{ splitScore(sel.finalScore).away }}</span>
                        </div>
                    </div>

                    <div class="flex flex-wrap items-center gap-2 mt-2">
                      <p class="text-[11px] text-gray-400">
                        Aposta: <span class="text-blue-300 font-bold border-b border-blue-300/30">{{ sel.marketName }} - {{ sel.selectionName }}</span>
                      </p>
                      
                      <span v-if="sel.status === 'Won'" class="text-[10px] text-green-400 font-bold bg-green-900/20 px-1 rounded flex items-center gap-1 border border-green-900/30">
                        Venceu ✅
                      </span>
                      <span v-else-if="sel.status === 'Lost'" class="text-[10px] text-red-400 font-bold bg-red-900/20 px-1 rounded flex items-center gap-1 border border-red-900/30">
                        Perdeu ❌
                      </span>
                    </div>
                    
                    <span class="text-[9px] text-gray-600 block">{{ formatDate(sel.commenceTime) }}</span>
                  </div>

                  <div class="text-right ml-4">
                    <span class="text-[10px] text-gray-500 block uppercase mb-0.5">Odd</span>
                    <span class="text-lg font-black text-blue-400">{{ sel.odd.toFixed(2) }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="px-4 py-3 bg-gray-900/50 flex justify-between items-center gap-2 border-t border-gray-700/50">
            <div class="text-xs text-gray-500">
                <span v-if="bet.status === 'Lost'">Bilhete encerrado</span>
                <span v-else-if="bet.status === 'Won'">Bilhete premiado</span>
                <span v-else>Aguardando resultados...</span>
            </div>

            <div class="text-right">
                <span class="text-[10px] font-bold uppercase block" 
                      :class="getReturnLabelClass(bet.status)">
                      {{ getReturnLabel(bet.status) }}
                </span>
                <span class="font-black text-xl" 
                      :class="getReturnValueClass(bet.status)">
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

// Ajustando a interface para bater com o DTO do C#
interface BetSelection {
  matchName: string; 
  marketName: string; 
  selectionName: string; 
  odd: number; 
  status: string; // 'Won', 'Lost', 'pending'
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
  status: string; // 'confirmed' (no banco é confirmed mas vamos tratar), 'Won', 'Lost'
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

// --- HELPERS VISUAIS ---

const getTeams = (matchName: string) => {
  if(!matchName) return { home: 'Mandante', away: 'Visitante' };
  const parts = matchName.split(/ x | vs | X /i);
  return {
    home: parts[0]?.trim() || 'Mandante',
    away: parts[1]?.trim() || 'Visitante'
  };
};

const splitScore = (score: string) => {
  if (!score || !score.includes('-')) return { home: '', away: '' };
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

// --- LÓGICA DE STATUS E CORES (AQUI ESTÁ A MÁGICA) ---

const translateStatus = (s: string) => {
  if (!s) return 'Pendente';
  const status = s.toLowerCase();
  const map: Record<string, string> = { 
      'pending': 'Em Andamento ⏳', 
      'confirmed': 'Em Andamento ⏳', 
      'won': 'Green ✅', 
      'lost': 'Red ❌' 
  };
  return map[status] || s;
};

const getStatusClass = (status: string) => {
  if (!status) return 'bg-gray-600 text-white';
  const s = status.toLowerCase();
  if (s === 'won') return 'bg-green-600 text-white shadow-[0_0_10px_rgba(34,197,94,0.6)]';
  if (s === 'lost') return 'bg-red-600 text-white shadow-[0_0_10px_rgba(239,68,68,0.6)]';
  return 'bg-blue-600 text-white';
};

const getBorderClass = (status: string) => {
    if (!status) return 'border-gray-700';
    const s = status.toLowerCase();
    if (s === 'won') return 'border-green-500/50 shadow-[0_0_15px_rgba(34,197,94,0.1)]';
    if (s === 'lost') return 'border-red-500/50 opacity-90';
    return 'border-gray-700 hover:border-blue-500/50';
}

// --- LÓGICA DE RETORNO NO RODAPÉ ---

const getReturnLabel = (status: string) => {
    const s = status?.toLowerCase() || '';
    if (s === 'won') return 'Retorno Recebido:';
    if (s === 'lost') return 'Status:';
    return 'Retorno Possível:';
}

const getReturnLabelClass = (status: string) => {
    const s = status?.toLowerCase() || '';
    if (s === 'won') return 'text-green-400';
    if (s === 'lost') return 'text-red-400';
    return 'text-gray-400';
}

const formatReturnValue = (bet: Bet) => {
    const s = bet.status?.toLowerCase() || '';
    
    // Se perdeu, mostra explicitamente que não tem retorno
    if (s === 'lost') return 'Aposta Perdida';
    
    // Se ganhou ou está pendente, mostra o valor
    return formatCurrency(bet.potentialReturn);
}

const getReturnValueClass = (status: string) => {
    const s = status?.toLowerCase() || '';
    if (s === 'won') return 'text-green-400 drop-shadow-md';
    if (s === 'lost') return 'text-red-500 line-through decoration-2 opacity-70';
    return 'text-yellow-400'; // Cor de destaque para possível retorno
}

</script>

<style>
/* Manter o estilo global da scrollbar que você já tinha */
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