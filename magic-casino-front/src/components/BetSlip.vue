<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue';
import { useBetStore } from '../stores/useBetStore';
import { useAuthStore } from '../stores/useAuthStore';
import apiSports from '../services/apiSports'; 
import { X, Trash2, Trophy, Loader2, Zap, Layers, ChevronRight, AlertCircle } from 'lucide-vue-next';
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

const potentialReturn = computed(() => {
  const valor = stake.value || 0;
  const odds = store.totalOdds || 0;
  return (valor * odds).toFixed(2);
});

const isUserLoggedIn = computed(() => !!(authStore?.token && authStore?.user));

const truncateName = (name: string, limit: number = 18) => {
  if (!name) return '';
  return name.length > limit ? name.substring(0, limit) + '...' : name;
};

const getMarketLabel = (type: string | undefined, marketName: string | undefined) => {
  const raw = type || marketName || '';
  if (['1', '2', 'X', 'x'].includes(raw)) {
    return 'RESULTADO FINAL';
  }
  return raw; 
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
  <div class="flex flex-col h-full bg-[#1e293b] text-gray-300 font-sans border-l border-gray-700">
    <div 
        @click="emit('toggle')"
        class="p-4 border-b border-gray-700 bg-[#162032] flex items-center justify-between cursor-pointer hover:bg-[#1c283d] transition-colors"
    >
      <div class="flex items-center gap-2">
        <h3 class="text-white font-bold flex items-center gap-2 select-none">
            <Trophy class="w-5 h-5 text-yellow-500" />
            Apostas
            <span v-if="store.count > 0" class="bg-yellow-500 text-gray-900 text-[10px] px-1.5 py-0.5 rounded-full font-black">
            {{ store.count }}
            </span>
        </h3>
      </div>
      <div class="flex items-center gap-3">
        <button v-if="store.count > 0" @click.stop="store.clearStore()" class="text-xs text-gray-400 hover:text-red-400 flex items-center gap-1 transition-colors uppercase font-bold mr-2">
            Limpar <Trash2 class="w-4 h-4" />
        </button>
        <ChevronRight class="w-5 h-5 text-gray-400 hover:text-white" />
      </div>
    </div>

    <div class="flex flex-col flex-1 overflow-hidden bg-[#1e293b]">
        <div v-if="store.count > 0" class="px-4 pt-4">
            <div v-if="store.count === 1" class="bg-gray-800/40 border border-gray-700 rounded-md p-2 flex items-center justify-center gap-2">
                <Zap class="w-3.5 h-3.5 text-yellow-500" />
                <span class="text-[10px] font-black text-gray-400 uppercase tracking-widest">Aposta Simples</span>
            </div>
            <div v-else class="bg-blue-600/10 border border-blue-500/30 rounded-md p-2 flex items-center justify-center gap-2">
                <Layers class="w-3.5 h-3.5 text-blue-400" />
                <span class="text-[10px] font-black text-blue-400 uppercase tracking-widest italic">Aposta Múltipla</span>
            </div>
        </div>

        <div class="flex-1 overflow-hidden flex flex-col">
            <div v-if="store.count === 0" class="text-center py-10 opacity-50 flex flex-col items-center flex-1 justify-center">
                <div class="bg-[#0f172a] w-16 h-16 rounded-full flex items-center justify-center mb-3">
                    <Trophy class="w-8 h-8 text-gray-600" />
                </div>
                <p class="text-gray-400 text-sm font-medium">Seu cupom está vazio.</p>
                <p class="text-gray-500 text-xs">Selecione uma cotação para começar.</p>
            </div>

            <div 
                v-else 
                ref="selectionsContainer"
                class="flex-1 overflow-y-auto p-4 space-y-3 custom-scrollbar"
            >
                <div v-for="item in store.selections" :key="item.id" class="bg-[#0f172a] rounded-lg p-3 border border-gray-700 relative group hover:border-gray-600 transition-colors shadow-sm overflow-hidden">
                    
                    <button @click="store.removeSelection(item.id)" class="absolute top-2 right-2 text-gray-500 hover:text-red-500 transition-colors p-1 z-10">
                        <X class="w-4 h-4" />
                    </button>

                    <div class="flex flex-col h-full justify-between">
                        <div class="text-[10px] font-medium flex items-center gap-1 flex-wrap pr-8 mb-2 text-left w-full">
                            <router-link 
                                :to="{ name: 'event-details', params: { id: String(item.id).split('_')[0] } }" 
                                class="text-gray-200 uppercase font-bold hover:text-blue-400 hover:underline transition-colors leading-tight"
                                @click="emit('toggle')"
                            >
                                {{ truncateName(item.homeTeam) }} x {{ truncateName(item.awayTeam) }}
                            </router-link>
                        </div>
                        
                        <div class="flex items-center justify-between mt-1 gap-2">
                            <span class="bg-[#1e293b] text-[10px] text-gray-400 px-2 py-1.5 rounded uppercase font-bold tracking-wider text-left flex-1 truncate">
                                {{ getMarketLabel(item.type, item.marketName) }}: <span class="text-blue-400">{{ item.selection }}</span>
                            </span>

                            <span class="text-white font-bold bg-blue-600/20 text-blue-400 px-2 py-1.5 text-xs border border-blue-500/20 min-w-[50px] text-center rounded shrink-0">
                                {{ (item.odds || 0).toFixed(2) }}
                            </span>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <div class="p-4 bg-[#162032] border-t border-gray-700 shadow-[0_-4px_6px_-1px_rgba(0,0,0,0.3)] z-10">
            <div class="space-y-2 mb-4 text-sm">
                <div class="flex justify-between text-gray-400 items-center">
                    <span>Retorno Potencial:</span>
                    <span class="text-green-400 font-bold text-lg">R$ {{ potentialReturn }}</span>
                </div>
            </div>

            <div class="mb-4 flex gap-2 h-[50px]">
                <div class="bg-[#0f172a] border border-gray-600 rounded-lg px-3 flex flex-col justify-center items-center min-w-[85px]">
                    <span class="text-[10px] text-gray-500 uppercase font-bold leading-none mb-1">Valor Odd</span>
                    <span class="text-yellow-400 font-bold text-lg leading-none">{{ (store.totalOdds || 0).toFixed(2) }}</span>
                </div>

                <div class="relative group flex-1 h-full">
                    
                    <span 
                        v-if="stakeError" 
                        class="absolute -top-6 left-0 text-[#ff5555] text-[10px] font-bold bg-[#ff5555]/10 px-2 py-0.5 rounded border border-[#ff5555]/20 flex items-center gap-1 animate-pulse z-20"
                    >
                        <AlertCircle class="w-3 h-3" /> Digite um valor válido!
                    </span>

                    <span class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500 font-bold">R$</span>
                    <input 
                        v-model="stake" 
                        type="number" 
                        placeholder="0.00" 
                        :disabled="!isUserLoggedIn" 
                        class="w-full h-full bg-[#0f172a] border rounded-lg pl-10 pr-4 text-white font-bold focus:outline-none transition-all disabled:opacity-50"
                        :class="stakeError ? 'border-red-500 focus:border-red-500 text-red-100' : 'border-gray-600 focus:border-blue-500'"
                    >
                </div>
            </div>

            <button @click="handlePlaceBet" :disabled="isLoading || store.count === 0" class="w-full py-3.5 rounded-lg font-bold uppercase tracking-wide transition-all transform active:scale-[0.98] disabled:opacity-50 flex justify-center items-center gap-2" :class="store.count > 0 ? 'bg-green-600 hover:bg-green-500 text-white' : 'bg-gray-700 text-gray-400'">
                <Loader2 v-if="isLoading" class="w-5 h-5 animate-spin" />
                <span v-else>{{ isUserLoggedIn ? 'FAZER APOSTA' : 'ENTRE PARA APOSTAR' }}</span>
            </button>
        </div>
    </div>
  </div>
</template>