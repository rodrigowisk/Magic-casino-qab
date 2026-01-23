<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue';
import { useBetStore } from '../stores/useBetStore';
import { useAuthStore } from '../stores/useAuthStore';
import apiSports from '../services/apiSports'; 
import { X, Trash2, Trophy, Loader2, ChevronRight, AlertCircle } from 'lucide-vue-next';
import Swal from 'sweetalert2';

defineProps<{ isOpen?: boolean }>();
const emit = defineEmits(['toggle']);

const store = useBetStore();
const authStore = useAuthStore();
const stake = ref<number | null>(null);
const isLoading = ref(false);
const stakeError = ref(false);

const selectionsContainer = ref<HTMLElement | null>(null);

watch(() => store.selections.length, async (newVal, oldVal) => {
  if (newVal > (oldVal || 0)) {
    await nextTick();
    if (selectionsContainer.value) {
      selectionsContainer.value.scrollTo({
        top: selectionsContainer.value.scrollHeight,
        behavior: 'smooth'
      });
    }
  }
});

watch(stake, () => {
    if (stakeError.value) stakeError.value = false;
});

const Toast = Swal.mixin({
  toast: true,
  position: 'top-end',
  showConfirmButton: false,
  timer: 3000,
  timerProgressBar: true,
  background: '#1e293b',
  color: '#fff',
  didOpen: (toast) => {
    toast.addEventListener('mouseenter', Swal.stopTimer);
    toast.addEventListener('mouseleave', Swal.resumeTimer);
  }
});

// Calcula o retorno (mantém numérico/string interna com ponto para cálculo)
const potentialReturn = computed(() => {
  const valor = stake.value || 0;
  const odds = store.totalOdds || 0;
  return (valor * odds).toFixed(2);
});

const isUserLoggedIn = computed(() => !!(authStore?.token && authStore?.user));

const truncateName = (name: string, limit: number = 20) => {
  if (!name) return '';
  return name.length > limit ? name.substring(0, limit) + '...' : name;
};

const getMarketLabel = (type: string | undefined, marketName: string | undefined) => {
  const raw = type || marketName || '';
  if (['1', '2', 'X', 'x'].includes(raw)) {
    return 'Resultado Final';
  }
  return raw; 
};

// Formata moeda para padrão Brasileiro (R$ 1.000,00)
const formatCurrency = (value: number | string) => {
    return Number(value).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
};

const handlePlaceBet = async () => {
  if (!isUserLoggedIn.value) {
    Toast.fire({ icon: 'info', title: 'Faça login para apostar' });
    return;
  }
  if (store.count === 0) return;
  
  if (!stake.value || stake.value <= 0) {
    stakeError.value = true;
    setTimeout(() => stakeError.value = false, 3000);
    return;
  }

  const valorApostado = Number(stake.value);
  isLoading.value = true;

  try {
    const payload = {
      requestId: crypto.randomUUID?.() ?? `${Date.now()}${Math.random()}`,
      amount: valorApostado,
      totalOdd: Number(store.totalOdds || 0),
      potentialReturn: Number(potentialReturn.value),
      selections: store.selections.map((s: any) => ({
        matchId: String(s.id).includes('_') ? String(s.id).split('_')[0] : String(s.id),
        matchName: `${s.homeTeam} x ${s.awayTeam}`,
        selectionName: s.selection,
        marketName: ['1', '2', 'X', 'x'].includes(s.type) ? '1x2' : (s.type || s.marketName || 'Mercado'), 
        odd: Number(s.odds || 0),
        commenceTime: s.commenceTime || s.commence_time 
      }))
    };

    const response = await apiSports.post('/bets/place', payload);

    if (authStore.user) {
        if (response.data?.newBalance !== undefined && response.data?.newBalance !== -1) {
             authStore.user.balance = response.data.newBalance;
        } else {
             authStore.user.balance = (authStore.user.balance || 0) - valorApostado;
        }
    }
    await authStore.fetchBalance();

    Swal.fire({
      title: 'Aposta Confirmada!',
      html: `ID do Bilhete: <b style="color: #4ade80">${response.data?.betId ?? '-'}</b><br>Boa sorte!`,
      icon: 'success',
      background: '#162032',
      color: '#ffffff',
      confirmButtonColor: '#16a34a',
      confirmButtonText: 'OK',
      backdrop: `rgba(0,0,0,0.8)`
    });

    store.clearStore();
    stake.value = null;
    emit('toggle'); 

  } catch (error: any) {
    const msg = error?.response?.data?.error || "Erro ao processar aposta.";
    Swal.fire({ title: 'Ops!', text: msg, icon: 'error', background: '#162032', color: '#ffffff', confirmButtonColor: '#ef4444' });
  } finally {
    isLoading.value = false;
  }
};
</script>

<template>
  <div class="flex flex-col h-full bg-[#0f172a] text-slate-300 font-sans border-l border-slate-800/50 shadow-2xl">
    
    <div 
        @click="emit('toggle')"
        class="h-12 px-3 border-b border-slate-800 bg-[#1e293b] flex items-center justify-between cursor-pointer hover:bg-[#253248] transition-colors"
    >
      <div class="flex items-center gap-2">
        <div class="bg-yellow-500/10 p-1 rounded">
            <Trophy class="w-4 h-4 text-yellow-500" />
        </div>
        <h3 class="text-white font-bold text-sm uppercase tracking-wide">
            Cupom
        </h3>
        <span v-if="store.count > 0" class="bg-blue-600 text-white text-[10px] px-1.5 rounded-full font-bold min-w-[18px] text-center">
            {{ store.count }}
        </span>
      </div>
      <div class="flex items-center gap-3">
        <button v-if="store.count > 0" @click.stop="store.clearStore()" class="text-[10px] text-slate-400 hover:text-red-400 flex items-center gap-1 transition-colors uppercase font-bold tracking-wider">
            LIMPAR <Trash2 class="w-3 h-3" />
        </button>
        <ChevronRight class="w-4 h-4 text-slate-500 hover:text-white" />
      </div>
    </div>

    <div class="flex flex-col flex-1 overflow-hidden bg-[#0f172a]">
        
        <div v-if="store.count === 0" class="flex-1 flex flex-col items-center justify-center opacity-40 p-6 text-center">
            <div class="bg-slate-800/50 w-12 h-12 rounded-full flex items-center justify-center mb-3">
                <Trophy class="w-6 h-6 text-slate-500" />
            </div>
            <p class="text-slate-400 text-xs font-bold uppercase tracking-wide">Cupom Vazio</p>
            <p class="text-slate-600 text-[10px] mt-1">Selecione odds para começar</p>
        </div>

        <div 
            v-else 
            ref="selectionsContainer"
            class="flex-1 overflow-y-auto p-2 space-y-2 custom-scrollbar"
        >
            <div class="sticky top-0 z-10 flex justify-center mb-2 pointer-events-none">
                <span class="bg-blue-600/90 backdrop-blur text-white text-[9px] font-black uppercase tracking-widest px-3 py-0.5 rounded shadow-lg border border-blue-400/20">
                    {{ store.count === 1 ? 'Aposta Simples' : 'Múltipla (' + store.count + ')' }}
                </span>
            </div>

            <div v-for="item in store.selections" :key="item.id" class="bg-[#1e293b] rounded border border-slate-700/50 hover:border-slate-600 transition-all group relative overflow-hidden">
                
                <button @click="store.removeSelection(item.id)" class="absolute top-0 right-0 p-1.5 text-slate-600 hover:text-red-500 transition-colors z-10 opacity-0 group-hover:opacity-100">
                    <X class="w-3.5 h-3.5" />
                </button>

                <div class="p-2.5">
                    <div class="pr-4 mb-1.5">
                        <div class="text-[11px] font-bold text-white leading-tight truncate">
                            {{ truncateName(item.homeTeam) }} 
                            <span class="text-slate-500 font-normal">vs</span> 
                            {{ truncateName(item.awayTeam) }}
                        </div>
                    </div>
                    
                    <div class="flex items-center justify-between gap-2">
                        <div class="flex flex-col min-w-0">
                            <span class="text-[9px] text-slate-500 font-bold uppercase truncate">{{ getMarketLabel(item.type, item.marketName) }}</span>
                            <span class="text-[11px] text-blue-400 font-bold truncate">{{ item.selection }}</span>
                        </div>

                        <div class="bg-[#0f172a] text-yellow-400 font-mono font-bold text-xs px-2 py-1 rounded border border-slate-700 shadow-inner">
                            {{ (item.odds || 0).toFixed(2) }}
                        </div>
                    </div>
                </div>
                <div class="absolute left-0 top-0 bottom-0 w-[2px] bg-blue-500"></div>
            </div>
        </div>
    </div>

    <div v-if="store.count > 0" class="bg-[#162032] border-t border-slate-700 p-3 shadow-[0_-4px_10px_rgba(0,0,0,0.5)] z-20">
        
        <div class="flex items-end justify-between mb-3 text-xs">
            <div class="flex flex-col">
                <span class="text-[10px] text-slate-500 uppercase font-bold tracking-wide">Odd Total</span>
                <span class="text-yellow-400 font-bold font-mono text-sm bg-yellow-400/10 px-1.5 rounded w-fit">{{ (store.totalOdds || 0).toFixed(2) }}</span>
            </div>
            <div class="flex flex-col items-end">
                <span class="text-[10px] text-slate-500 uppercase font-bold tracking-wide">Retorno Potencial</span>
                <span class="text-green-400 font-bold font-mono text-sm bg-green-400/10 px-1.5 rounded">
                    {{ formatCurrency(potentialReturn) }}
                </span>
            </div>
        </div>

        <div class="relative mb-3 group">
            <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                <span class="text-slate-500 font-bold text-xs">R$</span>
            </div>
            
            <input 
                v-model="stake" 
                type="number" 
                placeholder="Valor da Aposta" 
                :disabled="!isUserLoggedIn" 
                class="w-full bg-[#0b1120] text-white text-sm font-bold border border-slate-700 rounded pl-9 pr-3 py-2.5 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50 transition-all placeholder:text-slate-600 disabled:opacity-50"
                :class="stakeError ? 'border-red-500 animate-shake' : ''"
            />

            <span v-if="stakeError" class="absolute right-3 top-1/2 -translate-y-1/2 text-red-500 flex items-center gap-1 text-[10px] font-bold">
                <AlertCircle class="w-3 h-3" /> Inválido
            </span>
        </div>

        <button 
            @click="handlePlaceBet" 
            :disabled="isLoading || store.count === 0" 
            class="w-full py-2.5 rounded bg-gradient-to-r from-green-600 to-green-500 hover:from-green-500 hover:to-green-400 text-white font-bold text-xs uppercase tracking-widest shadow-lg shadow-green-900/20 transition-all transform active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed flex justify-center items-center gap-2 border border-green-400/20"
        >
            <Loader2 v-if="isLoading" class="w-4 h-4 animate-spin" />
            <span v-else>{{ isUserLoggedIn ? 'CONFIRMAR APOSTA' : 'ENTRE PARA APOSTAR' }}</span>
        </button>

    </div>
  </div>
</template>

<style scoped>
/* Scrollbar fina para o cupom */
.custom-scrollbar::-webkit-scrollbar {
  width: 4px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: #0f172a; 
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #334155; 
  border-radius: 4px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: #475569; 
}

@keyframes shake {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-4px); }
  75% { transform: translateX(4px); }
}
.animate-shake {
  animation: shake 0.3s ease-in-out;
}
</style>