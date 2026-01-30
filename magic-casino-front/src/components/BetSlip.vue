<script setup lang="ts">
import { ref, computed, watch, nextTick, onUnmounted, onMounted } from 'vue';
import { useBetStore } from '../stores/useBetStore';
import { useAuthStore } from '../stores/useAuthStore';
import apiSports from '../services/apiSports'; 
import { X, Trash2, Trophy, Loader2, ChevronRight, AlertCircle, ArrowRight, Hourglass, StopCircle, Calendar, AlertTriangle } from 'lucide-vue-next';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import Swal from 'sweetalert2';

defineProps<{ isOpen?: boolean }>();
const emit = defineEmits(['toggle']);

const store = useBetStore();
const authStore = useAuthStore();

// Inicializa com o valor salvo no localStorage, se existir
const savedStake = localStorage.getItem('lastStake');
const stake = ref<number | null>(savedStake ? Number(savedStake) : null);

const isLoading = ref(false); // Loading da requisição HTTP
const stakeError = ref(false);

// --- ESTADOS DO TIMER AO VIVO ---
const isProcessingLive = ref(false); // Controla a tela de espera
const showCancelledOverlay = ref(false); // Controla o overlay de cancelamento
const countdown = ref(12); // Segundos iniciais
const countdownTotal = 12;
let timerInterval: any = null;

// --- SIGNALR (Para limpar jogos encerrados) ---
let connection: HubConnection | null = null;

// --- ESTADO PARA CONFLITO DE ODDS ---
const oddConflict = ref<{
    matchName: string;
    oldOdd: number;
    newOdd: number;
    selectionId: string;
} | null>(null);

const selectionsContainer = ref<HTMLElement | null>(null);

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

// --- FUNÇÃO DE REMOÇÃO SEGURA ---
const removeEndedSelections = (idsToRemove: any[]) => {
    if (!idsToRemove || !Array.isArray(idsToRemove)) return;

    const safeIdsToRemove = idsToRemove.map(id => String(id));

    const selectionsToRemove = store.selections.filter(s => {
        const currentId = String(s.id);
        const parts = currentId.split('_');
        const matchId = parts[0] ?? currentId; 
        return safeIdsToRemove.includes(matchId);
    });

    if (selectionsToRemove.length > 0) {
        console.log("🎯 Removendo do cupom:", selectionsToRemove);
        
        selectionsToRemove.forEach(s => {
            if (s.id) {
                store.removeSelection(String(s.id));
            }
        });
        
        Toast.fire({
            icon: 'info',
            title: 'Jogos encerrados foram removidos do cupom.'
        });
    }
};

// --- CICLO DE VIDA (SIGNALR E TIMERS) ---

onMounted(async () => {
    console.log("🚀 [DEBUG V4.4 FINAL] BetSlip.vue montado! Overlay Centralizado.");

    const signalRUrl = "/gameHub";
    
    const newConnection = new HubConnectionBuilder()
        .withUrl(signalRUrl)
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Information)
        .build();

    newConnection.on('RemoveGames', (endedIds: any[]) => {
        if (!endedIds || !Array.isArray(endedIds) || store.selections.length === 0) return;
        removeEndedSelections(endedIds);
    });

    newConnection.on('LiveOddsUpdate', (updatedGames: any[]) => {
        if (!updatedGames || !Array.isArray(updatedGames) || store.selections.length === 0) return;
        
        const endedIdsFromUpdate: string[] = [];

        updatedGames.forEach(u => {
            if (u.status === 'Ended' || u.status === 'Completed' || u.status === 'FT' || u.status === '3') {
                if (u.id) {
                    endedIdsFromUpdate.push(String(u.id));
                }
            }
        });

        if (endedIdsFromUpdate.length > 0) {
            removeEndedSelections(endedIdsFromUpdate);
        }
    });

    newConnection.on('GameWentLive', () => {}); 

    try {
        await newConnection.start();
        connection = newConnection; 
    } catch (err) {
        console.error("❌ Erro SignalR no Cupom:", err);
    }
});

onUnmounted(() => {
    if (timerInterval) clearInterval(timerInterval);
    if (connection) connection.stop();
});

// ✅ MONITORAMENTO DE ALTERAÇÕES NO CUPOM
watch(() => store.selections, () => {
    // Se estiver processando e o cupom mudar (add/remove/odds), cancela e mostra aviso CENTRAL
    if (isProcessingLive.value) {
        cancelLiveProcessing();
        
        // Ativa o overlay de aviso
        showCancelledOverlay.value = true;
        
        // Remove o overlay automaticamente após 2.5 segundos
        setTimeout(() => {
            showCancelledOverlay.value = false;
        }, 2500);
    }
}, { deep: true });

// Scroll automático ao adicionar itens
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

watch(stake, (newVal) => {
    if (newVal) {
        localStorage.setItem('lastStake', String(newVal));
    }
    if (stakeError.value) stakeError.value = false;
});

const potentialReturn = computed(() => {
  const valor = stake.value || 0;
  const odds = store.totalOdds || 0;
  return (valor * odds).toFixed(2);
});

const isUserLoggedIn = computed(() => !!(authStore?.token && authStore?.user));

const hasLiveSelection = computed(() => {
    const now = new Date();
    return store.selections.some(s => {
        const time = s.commenceTime || (s as any).commence_time;
        if (!time) return false;
        const start = new Date(time);
        return start < now; 
    });
});

const progressDashoffset = computed(() => {
    const radius = 20;
    const circumference = 2 * Math.PI * radius; 
    return circumference - (countdown.value / countdownTotal) * circumference;
});

const truncateName = (name: any, limit: number = 20) => {
  if (!name) return '';
  const n = String(name); 
  return n.length > limit ? n.substring(0, limit) + '...' : n;
};

const formatGameDate = (dateString: any) => {
    if (!dateString) return '';
    try {
        const date = new Date(dateString);
        const day = String(date.getDate()).padStart(2, '0');
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');
        return `${day}/${month} • ${hours}:${minutes}`;
    } catch {
        return '';
    }
};

const getMarketLabel = (type: any, marketName: any) => {
  const raw = type || marketName || '';
  if (['1', '2', 'X', 'x'].includes(raw)) {
    return 'Resultado Final';
  }
  return raw; 
};

const formatCurrency = (value: number | string) => {
    return Number(value).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
};

const updateSelectionOdd = (selectionId: string, newOdd: number) => {
    const index = store.selections.findIndex(s => s.id === selectionId);
    if (index !== -1) {
        store.selections[index]!.odds = newOdd;
    }
};

const confirmOddChange = () => {
    if (!oddConflict.value) return;
    
    updateSelectionOdd(oddConflict.value.selectionId, oddConflict.value.newOdd);
    oddConflict.value = null;
    handlePlaceBet(false); 
};

const cancelOddChange = () => {
    if (!oddConflict.value) return;
    store.removeSelection(oddConflict.value.selectionId);
    oddConflict.value = null;
};

const cancelLiveProcessing = () => {
    if (timerInterval) clearInterval(timerInterval);
    isProcessingLive.value = false;
    countdown.value = countdownTotal;
};

const handlePlaceBet = async (forceSubmit = false) => {
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

  oddConflict.value = null;

  if (hasLiveSelection.value && !forceSubmit) {
      isProcessingLive.value = true;
      countdown.value = countdownTotal;
      
      if (timerInterval) clearInterval(timerInterval);

      timerInterval = setInterval(() => {
          countdown.value--;
          if (countdown.value <= 0) {
              clearInterval(timerInterval);
              submitBetToApi(); 
          }
      }, 1000);
      return; 
  }

  await submitBetToApi();
};

const submitBetToApi = async () => {
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
        selectionName: ['1', '2', 'X', 'x'].includes(s.type) ? s.type : s.selection,
        marketName: ['1', '2', 'X', 'x'].includes(s.type) ? '1x2' : (s.type || s.marketName || 'Mercado'), 
        odd: Number(s.odds || 0),
        commenceTime: s.commenceTime || (s as any).commence_time || new Date().toISOString()
      }))
    };

    const tokenLimpo = authStore.token?.replace(/['"]+/g, '') || '';
    
    const response = await apiSports.post('/bets/place', payload, {
        headers: { Authorization: `Bearer ${tokenLimpo}` }
    });

    isProcessingLive.value = false;

    if (authStore.user) {
        if (response.data?.newBalance !== undefined && response.data?.newBalance !== -1) {
             authStore.user!.balance = response.data.newBalance;
        } else {
             const currentBalance = authStore.user!.balance || 0;
             authStore.user!.balance = currentBalance - valorApostado;
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
    emit('toggle'); 

  } catch (error: any) {
    console.error("Erro ao apostar:", error);
    isProcessingLive.value = false; 

    if (error.response?.status === 409 || error.response?.data?.code === 'ODDS_CHANGED') {
        const data = error.response.data;
        oddConflict.value = {
            matchName: truncateName(data.matchName || 'Jogo', 25),
            oldOdd: parseFloat(data.oldOdd),
            newOdd: parseFloat(data.newOdd),
            selectionId: String(data.selectionId || data.matchId)
        };
        return; 
    }

    const msg = error?.response?.data?.error || "Erro ao processar aposta.";
    Swal.fire({ title: 'Ops!', text: msg, icon: 'error', background: '#162032', color: '#ffffff', confirmButtonColor: '#ef4444' });
  } finally {
    isLoading.value = false;
  }
};
</script>

<template>
  <div class="flex flex-col h-full bg-[#0f172a] text-slate-300 font-sans border-l border-slate-800/50 shadow-2xl relative overflow-hidden">
    
    <div v-if="isProcessingLive" class="absolute inset-0 z-50 bg-black/60 backdrop-blur-[2px] flex items-center justify-center p-4 animate-in fade-in duration-200">
        
        <div class="bg-[#1e293b] border border-white/10 shadow-2xl rounded-xl p-5 flex flex-col items-center w-full max-w-[240px] relative overflow-hidden">
            
            <div class="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-transparent via-green-500 to-transparent opacity-50"></div>

            <div class="relative w-12 h-12 mb-3 flex items-center justify-center">
                <svg class="w-full h-full -rotate-90 transform">
                    <circle cx="24" cy="24" r="20" stroke="currentColor" stroke-width="3" fill="transparent" class="text-slate-700" />
                    <circle cx="24" cy="24" r="20" stroke="currentColor" stroke-width="3" fill="transparent" 
                        :stroke-dasharray="2 * Math.PI * 20" 
                        :stroke-dashoffset="progressDashoffset" 
                        class="text-green-500 transition-all duration-1000 ease-linear" />
                </svg>
                <Hourglass class="w-5 h-5 text-green-400 absolute animate-pulse" />
            </div>

            <div class="flex flex-col items-center gap-0.5 mb-4 text-center">
                <h3 class="text-white font-bold text-sm">Processando...</h3>
                <span class="text-slate-400 text-[10px] leading-tight">Aguarde enquanto processamos sua aposta</span>
            </div>

            <div class="text-3xl font-mono font-black text-white mb-4 tracking-tighter">
                {{ countdown }}<span class="text-xs text-slate-500 font-bold ml-0.5">s</span>
            </div>

            <button @click="cancelLiveProcessing" class="w-full flex items-center justify-center gap-2 px-3 py-2 bg-red-500/10 hover:bg-red-500/20 text-red-500 border border-red-500/30 rounded-lg font-bold text-[10px] uppercase tracking-wide transition-all active:scale-95">
                <StopCircle class="w-3.5 h-3.5" /> Cancelar
            </button>
        </div>
    </div>

    <div v-if="showCancelledOverlay" class="absolute inset-0 z-50 bg-black/60 backdrop-blur-[2px] flex items-center justify-center p-4 animate-in fade-in duration-200">
        <div class="bg-[#1e293b] border border-red-500/50 shadow-2xl rounded-xl p-6 flex flex-col items-center w-full max-w-[240px] relative overflow-hidden">
             <div class="absolute top-0 left-0 w-full h-1 bg-red-500 opacity-70"></div>
             
             <div class="bg-red-500/10 p-3 rounded-full mb-3 ring-1 ring-red-500/20">
                 <AlertTriangle class="w-8 h-8 text-red-500 animate-bounce" />
             </div>

             <h3 class="text-white font-bold text-sm text-center mb-1">Processamento Cancelado</h3>
             <p class="text-slate-400 text-[11px] text-center leading-tight">
                 O cupom foi alterado durante o processamento. Tente novamente.
             </p>
        </div>
    </div>

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
                        
                        <div class="flex items-center gap-1 text-[9px] text-slate-400 mb-0.5 font-medium tracking-wide">
                            <Calendar class="w-2.5 h-2.5 opacity-70" />
                            {{ formatGameDate(item.commenceTime || (item as any).commence_time) }}
                        </div>

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

                        <div class="flex items-center gap-2">
                            <div class="bg-[#0f172a] text-yellow-400 font-mono font-bold text-xs px-2 py-1 rounded border border-slate-700 shadow-inner">
                                {{ (item.odds || 0).toFixed(2) }}
                            </div>
                        </div>
                    </div>
                </div>
                <div v-if="oddConflict && oddConflict.selectionId === item.id" class="absolute inset-0 border-2 border-yellow-500/50 rounded pointer-events-none animate-pulse"></div>
                <div v-else class="absolute left-0 top-0 bottom-0 w-[2px] bg-blue-500"></div>
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

        <div v-if="oddConflict" class="mb-3 bg-yellow-500/10 border border-yellow-500/30 rounded p-2 flex flex-col gap-2 animate-in fade-in slide-in-from-bottom-2 duration-300">
            <div class="flex items-start gap-2">
                <AlertCircle class="w-4 h-4 text-yellow-500 mt-0.5 shrink-0" />
                <div class="text-[10px] leading-tight text-slate-300">
                    <span class="font-bold text-yellow-500">Atenção:</span> A odd de 
                    <span class="text-white font-bold">{{ oddConflict.matchName }}</span> alterou.
                    <div class="flex items-center gap-1.5 mt-1 bg-black/20 w-fit px-1.5 py-0.5 rounded">
                        <span class="line-through text-red-400 font-mono">{{ oddConflict.oldOdd.toFixed(2) }}</span>
                        <ArrowRight class="w-3 h-3 text-slate-500" />
                        <span class="text-green-400 font-bold font-mono text-xs">{{ oddConflict.newOdd.toFixed(2) }}</span>
                    </div>
                </div>
            </div>
            <div class="flex gap-2 w-full">
                <button @click="confirmOddChange" class="flex-1 bg-yellow-600 hover:bg-yellow-500 text-white text-[10px] font-bold py-1.5 rounded transition-colors uppercase">
                    Aceitar
                </button>
                <button @click="cancelOddChange" class="flex-1 bg-slate-700 hover:bg-slate-600 text-slate-300 text-[10px] font-bold py-1.5 rounded transition-colors uppercase">
                    Remover
                </button>
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
                :disabled="!isUserLoggedIn || !!oddConflict || isProcessingLive" 
                class="w-full bg-[#0b1120] text-white text-sm font-bold border border-slate-700 rounded pl-9 pr-3 py-2.5 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/50 transition-all placeholder:text-slate-600 disabled:opacity-50 disabled:cursor-not-allowed"
                :class="stakeError ? 'border-red-500 animate-shake' : ''"
            />

            <span v-if="stakeError" class="absolute right-3 top-1/2 -translate-y-1/2 text-red-500 flex items-center gap-1 text-[10px] font-bold">
                <AlertCircle class="w-3 h-3" /> Inválido
            </span>
        </div>

        <button 
            @click="() => handlePlaceBet(false)" 
            :disabled="isLoading || store.count === 0 || !!oddConflict || isProcessingLive" 
            class="w-full py-2.5 rounded bg-gradient-to-r from-green-600 to-green-500 hover:from-green-500 hover:to-green-400 text-white font-bold text-xs uppercase tracking-widest shadow-lg shadow-green-900/20 transition-all transform active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed flex justify-center items-center gap-2 border border-green-400/20"
        >
            <Loader2 v-if="isLoading" class="w-4 h-4 animate-spin" />
            <span v-else>{{ isUserLoggedIn ? 'CONFIRMAR APOSTA' : 'ENTRE PARA APOSTAR' }}</span>
        </button>

    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #0f172a; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #475569; }

@keyframes shake {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-4px); }
  75% { transform: translateX(4px); }
}
.animate-shake { animation: shake 0.3s ease-in-out; }
</style>