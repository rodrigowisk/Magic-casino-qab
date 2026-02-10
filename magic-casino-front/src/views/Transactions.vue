<template>
  <div class="min-h-screen bg-[#0f212e] text-white p-4 font-sans flex justify-center">
    <div class="w-full max-w-5xl">
      
      <div class="mb-4 relative pl-2 animate-fade-in-down flex justify-between items-end">
        <div>
          <div class="absolute -top-6 -left-6 w-20 h-20 bg-blue-500/10 rounded-full blur-2xl"></div>
          <h1 class="text-xl font-bold text-white flex items-center gap-2 relative z-10">
            <FileText class="w-5 h-5 text-blue-500" />
            Transações
          </h1>
          <p class="text-[11px] text-gray-400 relative z-10">Histórico de movimentações.</p>
        </div>
      </div>

      <div class="bg-[#1a2c38] rounded-xl border border-gray-700/50 shadow-2xl overflow-hidden animate-fade-in-up flex flex-col h-[calc(100vh-120px)] md:h-auto">
        
        <div class="px-3 py-2 border-b border-gray-700/50 flex flex-col md:flex-row justify-between items-center gap-3 bg-[#15222b]">
          <div class="flex bg-[#0f212e] p-0.5 rounded-lg w-full md:w-auto overflow-x-auto custom-scrollbar">
            <button 
              v-for="filter in filters" 
              :key="filter.id"
              @click="activeFilter = filter.id"
              class="flex-1 md:flex-none px-3 py-1 text-[10px] font-bold uppercase tracking-wider rounded transition-all whitespace-nowrap"
              :class="activeFilter === filter.id ? 'bg-[#2a455a] text-white shadow-sm' : 'text-gray-500 hover:text-gray-300'"
            >
              {{ filter.label }}
            </button>
          </div>

          <div class="md:hidden w-full flex justify-between items-center border-t border-gray-700/50 pt-2">
            <span class="text-[10px] text-gray-500 font-bold uppercase">Total:</span>
            <span class="text-xs font-mono font-bold" :class="numericTotal >= 0 ? 'text-green-400' : 'text-red-400'">
              R$ {{ totalAmount }}
            </span>
          </div>
        </div>

        <div class="overflow-auto flex-1 custom-scrollbar">
          <table class="w-full text-left border-collapse">
            <thead class="sticky top-0 bg-[#1a2c38] z-10 shadow-sm">
              <tr class="text-gray-500 text-[9px] uppercase font-bold border-b border-gray-700/50 tracking-wider">
                <th class="py-2 px-3">Tipo</th>
                <th class="py-2 px-3 hidden md:table-cell">ID</th>
                <th class="py-2 px-3">Data</th>
                <th class="py-2 px-3 text-right">Valor</th>
                <th class="py-2 px-3 text-center">Status</th>
              </tr>
            </thead>
            
            <tbody v-if="loading">
              <tr v-for="i in 8" :key="i" class="border-b border-gray-700/10">
                <td class="p-2"><div class="h-6 w-6 bg-gray-700/30 rounded-md animate-pulse"></div></td>
                <td class="p-2 hidden md:table-cell"><div class="h-2 w-12 bg-gray-700/30 rounded animate-pulse"></div></td>
                <td class="p-2"><div class="h-2 w-16 bg-gray-700/30 rounded animate-pulse"></div></td>
                <td class="p-2"><div class="h-2 w-10 bg-gray-700/30 rounded animate-pulse ml-auto"></div></td>
                <td class="p-2"><div class="h-3 w-12 bg-gray-700/30 rounded animate-pulse mx-auto"></div></td>
              </tr>
            </tbody>

            <tbody v-else-if="filteredTransactions.length === 0">
              <tr>
                <td colspan="5" class="py-12 text-center text-gray-500">
                  <Inbox class="w-8 h-8 mx-auto mb-2 opacity-30" />
                  <p class="text-[10px]">Sem registros.</p>
                </td>
              </tr>
            </tbody>

            <tbody v-else>
              <tr 
                v-for="tx in filteredTransactions" 
                :key="tx.id" 
                class="border-b border-gray-700/10 hover:bg-[#203442]/50 transition-colors group cursor-default"
              >
                <td class="py-1.5 px-3">
                  <div class="flex items-center gap-2.5">
                    <div 
                      class="w-7 h-7 rounded-[6px] flex items-center justify-center border border-white/5 shadow-sm shrink-0"
                      :class="{
                        'bg-green-500/10 text-green-500': tx.type === 'deposit' || tx.type === 'win' || tx.type === 'tournament_prize',
                        'bg-red-500/10 text-red-500': tx.type === 'withdraw' || tx.type === 'bet' || tx.type === 'tournament',
                        'bg-gray-700/30 text-gray-400': !['deposit','withdraw','win','bet','tournament','tournament_prize'].includes(tx.type)
                      }"
                    >
                      <ArrowUpRight v-if="tx.type === 'deposit'" class="w-3.5 h-3.5" />
                      <ArrowDownLeft v-else-if="tx.type === 'withdraw'" class="w-3.5 h-3.5" />
                      <Trophy v-else-if="tx.type === 'win' || tx.type === 'tournament_prize'" class="w-3.5 h-3.5" />
                      <Dices v-else-if="tx.type === 'bet'" class="w-3.5 h-3.5" />
                      <Medal v-else-if="tx.type === 'tournament'" class="w-3.5 h-3.5" />
                      <FileText v-else class="w-3.5 h-3.5" />
                    </div>
                    <div class="min-w-0">
                      <p class="text-[11px] font-semibold text-gray-200 leading-tight">{{ getLabel(tx.type) }}</p>
                      
                      <p class="hidden md:block text-[9px] text-gray-500 truncate max-w-[120px]">
                        {{ formatDescription(tx.method) }}
                      </p>
                      <p class="md:hidden text-[9px] text-gray-500 truncate">
                        {{ getMobileDescription(tx) }}
                      </p>
                    </div>
                  </div>
                </td>

                <td class="py-1.5 px-3 hidden md:table-cell">
                  <span class="font-mono text-[9px] text-gray-500 bg-[#0f212e]/50 px-1 py-0.5 rounded border border-gray-700/30 tracking-tight">
                    {{ tx.id }}
                  </span>
                </td>

                <td class="py-1.5 px-3">
                  <div class="flex flex-col">
                    <span class="text-[10px] text-gray-300">{{ formatDate(tx.date) }}</span>
                    <span class="text-[8px] text-gray-600">{{ formatTime(tx.date) }}</span>
                  </div>
                </td>

                <td class="py-1.5 px-3 text-right whitespace-nowrap">
                  <span 
                    class="font-mono text-[11px] font-bold tabular-nums tracking-tight" 
                    :class="getAmountColor(tx)"
                  >
                    {{ isDebit(tx.type) ? '-' : '+' }} {{ formatCurrency(tx.amount) }}
                  </span>
                </td>

                <td class="py-1.5 px-3 text-center">
                  <div 
                    class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[8px] font-bold uppercase tracking-wide border border-transparent"
                    :class="{
                      'bg-green-500/5 text-green-500 border-green-500/10': tx.status === 'completed' || tx.status === 'paid',
                      'bg-yellow-500/5 text-yellow-500 border-yellow-500/10': tx.status === 'pending',
                      'bg-red-500/5 text-red-500 border-red-500/10': tx.status === 'failed'
                    }"
                  >
                    <span class="w-1 h-1 rounded-full shrink-0" :class="{
                      'bg-green-500': tx.status === 'completed' || tx.status === 'paid',
                      'bg-yellow-500': tx.status === 'pending',
                      'bg-red-500': tx.status === 'failed'
                    }"></span>
                    {{ getStatusLabel(tx.status) }}
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="p-1.5 bg-[#15222b] border-t border-gray-700/50 flex justify-center text-[9px] text-gray-600">
          Mostrando últimos registros
        </div>

      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import axios from 'axios';
import { FileText, ArrowUpRight, ArrowDownLeft, Dices, Inbox, Trophy, Medal } from 'lucide-vue-next';

interface Transaction {
  id: string;
  type: string;
  amount: number;
  status: 'pending' | 'completed' | 'paid' | 'failed';
  date: string;
  method: string;
}

const loading = ref(true);
const transactions = ref<Transaction[]>([]);
const activeFilter = ref('all');

const filters = [
  { id: 'all', label: 'Todas' },
  { id: 'deposit', label: 'Depósitos' },
  { id: 'withdraw', label: 'Saques' },
  { id: 'tournament', label: 'Torneio' }
];

// Helper para identificar débitos
const isDebit = (type: string) => {
    return ['withdraw', 'bet', 'tournament'].includes(type);
};

const filteredTransactions = computed(() => {
  if (activeFilter.value === 'all') return transactions.value;
  if (activeFilter.value === 'tournament') {
      return transactions.value.filter(t => t.type === 'tournament' || t.type === 'tournament_prize');
  }
  return transactions.value.filter(t => t.type === activeFilter.value);
});

// ✅ CÁLCULO TOTAL CORRIGIDO (Subtrai inscrições)
const numericTotal = computed(() => {
  return filteredTransactions.value.reduce((acc, curr) => {
    if (isDebit(curr.type)) {
      return acc - curr.amount;
    }
    return acc + curr.amount;
  }, 0);
});

const totalAmount = computed(() => {
  return formatCurrency(numericTotal.value);
});

const formatCurrency = (val: number) => val.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }).replace('R$', '').trim();
const formatDate = (d: string) => new Date(d).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' });
const formatTime = (d: string) => new Date(d).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });

// Formatação Padrão (Desktop)
const formatDescription = (desc: string) => {
  if (!desc) return '';
  let clean = desc.replace('Inscrição em Torneio', 'Inscrição');
  clean = clean.replace('Prêmio Torneio', 'Prêmio');
  return clean;
};

// ✅ FORMATAÇÃO MOBILE (Resumida)
const getMobileDescription = (tx: Transaction) => {
    // Se for prêmio de torneio, retorna apenas "Prêmio"
    if (tx.type === 'tournament_prize' || tx.method.includes('Prêmio')) {
        return 'Prêmio';
    }
    // Se for inscrição de torneio, retorna apenas "Inscrição"
    if (tx.type === 'tournament' || tx.method.includes('Inscrição')) {
        return 'Inscrição';
    }
    // Para outros tipos, mantém o padrão formatado
    return formatDescription(tx.method);
};

const getLabel = (type: string) => {
  const map: Record<string, string> = {
    'deposit': 'Depósito',
    'withdraw': 'Saque',
    'bet': 'Aposta',
    'win': 'Prêmio',
    'tournament': 'Torneio',
    'tournament_prize': 'Torneio',
    'sportbook': 'Sportbook'
  };
  return map[type] || type;
};

const getStatusLabel = (status: string) => {
  const map: Record<string, string> = {
    'completed': 'Finalizada',
    'paid': 'Finalizada',
    'pending': 'Aguardando Pagamento',
    'failed': 'Falha'
  };
  return map[status] || status;
};

// ✅ COR DO VALOR CORRIGIDA (Vermelho para Inscrição)
const getAmountColor = (tx: Transaction) => {
  if (tx.status === 'pending') return 'text-yellow-500';
  if (isDebit(tx.type)) return 'text-red-400';
  return 'text-green-400';
};

const loadTransactions = async () => {
  loading.value = true;
  try {
    const rawToken = localStorage.getItem('token');
    if (!rawToken) return;
    const token = rawToken.replace(/['"]+/g, '');

    const response = await axios.get('/core/api/user/transactions', {
      headers: { Authorization: `Bearer ${token}` }
    });
    transactions.value = response.data;
  } catch (error) {
    console.error("Erro API", error);
  } finally {
    loading.value = false;
  }
};

onMounted(() => loadTransactions());
</script>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 3px; height: 3px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #0f212e; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #2a455a; border-radius: 3px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #3b5e78; }

.animate-fade-in-down { animation: fadeInDown 0.5s ease-out; }
.animate-fade-in-up { animation: fadeInUp 0.5s ease-out; }
@keyframes fadeInDown { from { opacity: 0; transform: translateY(-10px); } to { opacity: 1; transform: translateY(0); } }
@keyframes fadeInUp { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
</style>