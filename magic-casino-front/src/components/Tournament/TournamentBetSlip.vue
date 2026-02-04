<script setup lang="ts">
import { ref, computed, watch, nextTick, onUnmounted, onMounted } from 'vue';
import { useBetStore } from '../../stores/useBetStore';
import tournamentService from '../../services/Tournament/TournamentService';
import { X, Trash2, Trophy, Loader2, AlertCircle, ArrowRight, Hourglass, StopCircle, Calendar, AlertTriangle, Coins } from 'lucide-vue-next';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import Swal from 'sweetalert2';

const props = defineProps<{ 
    isOpen?: boolean;
    tournamentId: number;       
    fantasyBalance: number;     
}>();

const emit = defineEmits(['toggle', 'balance-updated']);

const store = useBetStore();

// --- ESTADO ---
const savedStake = localStorage.getItem('lastTournamentStake');
const stake = ref<number | null>(savedStake ? Number(savedStake) : null);
const isLoading = ref(false);
const stakeError = ref(false);

// --- ESTADOS DO TIMER (PROCESSAMENTO) ---
const isProcessingLive = ref(false);
const showCancelledOverlay = ref(false);
const countdown = ref(12);
const countdownTotal = 12;
let timerInterval: any = null;

// --- SIGNALR ---
let connection: HubConnection | null = null;

// --- CONFLITO DE ODDS ---
const oddConflict = ref<{
    matchName: string;
    oldOdd: number;
    newOdd: number;
    selectionId: string;
} | null>(null);

const selectionsContainer = ref<HTMLElement | null>(null);

const Toast = Swal.mixin({
  toast: true, position: 'top-end', showConfirmButton: false, timer: 3000, background: '#1e293b', color: '#fff'
});

// --- HELPER: REMOVER JOGOS ENCERRADOS ---
const removeEndedSelections = (idsToRemove: any[]) => {
    if (!idsToRemove || !Array.isArray(idsToRemove)) return;
    const safeIdsToRemove = idsToRemove.map(id => String(id));
    
    const selectionsToRemove = store.selections.filter(s => {
        const fullId = String(s.id);
        const baseId = fullId.split('_')[0] || fullId;
        return safeIdsToRemove.includes(baseId);
    });

    if (selectionsToRemove.length > 0) {
        selectionsToRemove.forEach(s => { if (s.id) store.removeSelection(String(s.id)); });
        Toast.fire({ icon: 'info', title: 'Jogos encerrados foram removidos do cupom.' });
    }
};

// --- CICLO DE VIDA ---
onMounted(async () => {
    const signalRUrl = "/gameHub";
    const newConnection = new HubConnectionBuilder()
        .withUrl(signalRUrl)
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Information)
        .build();

    newConnection.on('RemoveGames', (endedIds: any[]) => removeEndedSelections(endedIds));
    
    newConnection.on('LiveOddsUpdate', (updatedGames: any[]) => {
         if (updatedGames && Array.isArray(updatedGames)) {
             const ended = updatedGames
                .filter(u => ['Ended','FT','Completed','3'].includes(u.status))
                .map(u => String(u.id));
             if (ended.length) removeEndedSelections(ended);
         }
    });

    try { await newConnection.start(); connection = newConnection; } catch (err) { console.error("SignalR Error", err); }
});

onUnmounted(() => {
    if (timerInterval) clearInterval(timerInterval);
    if (connection) connection.stop();
});

// --- WATCHERS ---
watch(() => store.selections, () => {
    if (isProcessingLive.value) {
        cancelLiveProcessing();
        showCancelledOverlay.value = true;
        setTimeout(() => { showCancelledOverlay.value = false; }, 2500);
    }
}, { deep: true });

watch(() => store.selections.length, async (newVal, oldVal) => {
  if (newVal > (oldVal || 0)) {
    await nextTick();
    if (selectionsContainer.value) selectionsContainer.value.scrollTo({ top: selectionsContainer.value.scrollHeight, behavior: 'smooth' });
  }
});

watch(stake, (newVal) => {
    if (newVal) localStorage.setItem('lastTournamentStake', String(newVal));
    stakeError.value = false;
});

// --- COMPUTED ---
const potentialReturn = computed(() => {
  const valor = stake.value || 0;
  const odds = store.totalOdds || 0;
  return (valor * odds).toFixed(2);
});

const hasLiveSelection = computed(() => {
    const now = new Date();
    return store.selections.some(s => {
        if (!s.commenceTime) return false;
        return new Date(s.commenceTime) < now;
    });
});

const progressDashoffset = computed(() => {
    const radius = 20;
    const circumference = 2 * Math.PI * radius; 
    return circumference - (countdown.value / countdownTotal) * circumference;
});

// --- HELPERS VISUAIS ---
const truncateName = (name: any, limit: number = 20) => { 
    const n = String(name||''); 
    return n.length > limit ? n.substring(0, limit) + '...' : n; 
};

const formatGameDate = (d: any) => { try { const date = new Date(d); return `${String(date.getDate()).padStart(2,'0')}/${String(date.getMonth()+1).padStart(2,'0')} • ${String(date.getHours()).padStart(2,'0')}:${String(date.getMinutes()).padStart(2,'0')}`; } catch { return ''; }};
const getMarketLabel = (type: any, marketName: any) => ['1','2','X','x'].includes(type || marketName) ? 'Resultado Final' : (type || marketName);

const getSelectionName = (item: any) => {
    const raw = String(item.selection).toUpperCase();
    if (raw === '1') return truncateName(item.homeTeam);
    if (raw === '2') return truncateName(item.awayTeam);
    if (raw === 'X') return 'Empate';
    return item.selection;
};

const formatCurrency = (val: number | string) => {
    const num = Number(val);
    if (isNaN(num)) return '0.00';
    return num.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }).replace('R$', '🪙');
};

// --- AÇÕES ---
const updateSelectionOdd = (selectionId: string, newOdd: number) => {
    const index = store.selections.findIndex(s => s.id === selectionId);
    if (index !== -1) store.selections[index]!.odds = newOdd;
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

// --- LÓGICA DE APOSTA ---
const handlePlaceBet = async (forceSubmit = false) => {
  if (store.count === 0) return;
  
  if (!stake.value || stake.value <= 0) {
    stakeError.value = true;
    setTimeout(() => stakeError.value = false, 3000);
    return;
  }

  // Validação de Saldo (Torneio)
  const totalCost = stake.value * store.count; // Simples em Lote
  if (totalCost > props.fantasyBalance) {
      Swal.fire({ 
          title: 'Saldo Insuficiente!', 
          text: `Você precisa de ${formatCurrency(totalCost)} fichas.`, 
          icon: 'warning', 
          background: '#162032', 
          color: '#fff' 
      });
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

  let currentUserId = '';
  try {
      const stored = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
      if (stored) {
          const u = JSON.parse(stored);
          const raw = u.Code || u.code || u.Cpf || u.cpf || u.id || '';
          currentUserId = String(raw).replace(/\D/g, '');
      }
  } catch {}

  try {
    // Monta o payload IGUAL ao do Sportbook (Lista)
    const selectionsPayload = store.selections.map(s => {
        const rawId = String(s.id);
        const gameId = rawId.includes('_') ? rawId.split('_')[0] : rawId;
        return {
            gameId: gameId,
            homeTeam: s.homeTeam,
            awayTeam: s.awayTeam,
            marketName: ['1','2','X','x'].includes(s.type) ? 'Match Winner' : (s.marketName || 'Mercado'),
            selectionName: s.type, // "1", "X", "2"
            odds: Number(s.odds)
        };
    });

    const payload = {
        userId: currentUserId,
        amount: valorApostado,
        selections: selectionsPayload
    };

    // Chama API de Torneio (Batch)
    await tournamentService.placeBet(props.tournamentId, payload);

    isProcessingLive.value = false;
    emit('balance-updated');

    Swal.fire({
      title: 'Apostas Confirmadas!',
      text: 'Boa sorte no torneio!',
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
        const conflictItem = store.selections.find(s => s.id.includes(String(data.gameId)));
        
        // Pega o nome do time para o visual ser bonito
        const matchName = conflictItem ? truncateName(conflictItem.homeTeam, 25) : 'Jogo';

        oddConflict.value = {
            matchName: matchName,
            oldOdd: parseFloat((conflictItem?.odds || 1.0).toString()),
            newOdd: parseFloat(data.newOdd || 1.0),
            selectionId: conflictItem?.id || ''
        };
        return;
    }

    const msg = error?.response?.data?.error || error?.response?.data?.message || "Erro ao processar aposta.";
    Swal.fire({ title: 'Ops!', text: msg, icon: 'error', background: '#162032', color: '#ffffff', confirmButtonColor: '#ef4444' });
  } finally {
    isLoading.value = false;
  }
};
</script>

<template>
  <div v-if="isOpen">
      <div class="fixed inset-0 bg-black/60 backdrop-blur-[2px] z-40 transition-opacity" @click="emit('toggle')"></div>

      <div class="fixed inset-y-0 right-0 z-50 w-full md:w-80 bg-[#0f172a] border-l border-slate-800/50 shadow-2xl transform transition-transform duration-300 ease-out flex flex-col font-sans translate-x-0">
        
        <div v-if="isProcessingLive" class="absolute inset-0 z-50 bg-black/60 backdrop-blur-[2px] flex items-center justify-center p-4">
            <div class="bg-[#1e293b] border border-white/10 shadow-2xl rounded-xl p-5 flex flex-col items-center w-full max-w-[240px] relative overflow-hidden">
                <div class="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-transparent via-green-500 to-transparent opacity-50"></div>
                <div class="relative w-12 h-12 mb-3 flex items-center justify-center">
                    <svg class="w-full h-full -rotate-90 transform">
                        <circle cx="24" cy="24" r="20" stroke="currentColor" stroke-width="3" fill="transparent" class="text-slate-700" />
                        <circle cx="24" cy="24" r="20" stroke="currentColor" stroke-width="3" fill="transparent" :stroke-dasharray="2 * Math.PI * 20" :stroke-dashoffset="progressDashoffset" class="text-green-500 transition-all duration-1000 ease-linear" />
                    </svg>
                    <Hourglass class="w-5 h-5 text-green-400 absolute animate-pulse" />
                </div>
                <div class="flex flex-col items-center gap-0.5 mb-4 text-center">
                    <h3 class="text-white font-bold text-sm">Processando...</h3>
                    <span class="text-slate-400 text-[10px] leading-tight">Aguarde enquanto processamos sua aposta</span>
                </div>
                <div class="text-3xl font-mono font-black text-white mb-4 tracking-tighter">{{ countdown }}<span class="text-xs text-slate-500 font-bold ml-0.5">s</span></div>
                <button @click="cancelLiveProcessing" class="w-full flex items-center justify-center gap-2 px-3 py-2 bg-red-500/10 hover:bg-red-500/20 text-red-500 border border-red-500/30 rounded-lg font-bold text-[10px] uppercase tracking-wide transition-all active:scale-95"><StopCircle class="w-3.5 h-3.5" /> Cancelar</button>
            </div>
        </div>

        <div v-if="showCancelledOverlay" class="absolute inset-0 z-50 bg-black/60 backdrop-blur-[2px] flex items-center justify-center p-4">
            <div class="bg-[#1e293b] border border-red-500/50 shadow-2xl rounded-xl p-6 flex flex-col items-center w-full max-w-[240px] relative overflow-hidden">
                 <div class="absolute top-0 left-0 w-full h-1 bg-red-500 opacity-70"></div>
                 <div class="bg-red-500/10 p-3 rounded-full mb-3 ring-1 ring-red-500/20"><AlertTriangle class="w-8 h-8 text-red-500 animate-bounce" /></div>
                 <h3 class="text-white font-bold text-sm text-center mb-1">Processamento Cancelado</h3>
                 <p class="text-slate-400 text-[11px] text-center leading-tight">O cupom foi alterado durante o processamento. Tente novamente.</p>
            </div>
        </div>

        <div class="h-12 px-3 border-b border-slate-800 bg-[#1e293b] flex items-center justify-between relative border-t border-yellow-500/50 shadow-[0_-4px_15px_rgba(234,179,8,0.2)]">
          <div class="flex items-center gap-2 flex-shrink-0">
            <div class="bg-yellow-500/10 p-1 rounded"><Trophy class="w-4 h-4 text-yellow-500" /></div>
            <h3 class="text-white font-bold text-sm uppercase tracking-wide">Cupom Torneio</h3>
            <span v-if="store.count > 0" class="bg-blue-600 text-white text-[10px] px-1.5 rounded-full font-bold min-w-[18px] text-center">{{ store.count }}</span>
          </div>
          <div class="flex items-center gap-2">
              <button v-if="store.count > 0" @click.stop="store.clearStore()" class="text-[10px] text-slate-400 hover:text-red-400 flex items-center gap-1 transition-colors uppercase font-bold tracking-wider mr-2">LIMPAR <Trash2 class="w-3 h-3" /></button>
              <button @click="emit('toggle')" class="text-slate-400 hover:text-white transition-colors p-1 rounded hover:bg-white/5"><X class="w-5 h-5" /></button>
          </div>
        </div>

        <div v-if="store.count > 0" class="hidden md:flex bg-blue-600/10 border-b border-blue-600/20 py-1.5 justify-center items-center shadow-inner">
             <span class="text-blue-400 text-[10px] font-bold uppercase tracking-widest">{{ store.count === 1 ? 'Aposta Simples' : 'Aposta Múltipla' }}</span>
        </div>

        <div class="flex flex-col flex-1 overflow-hidden bg-[#0f172a]">
            <div v-if="store.count === 0" class="flex-1 flex flex-col items-center justify-center opacity-40 p-6 text-center">
                <Trophy class="w-10 h-10 text-slate-500 mb-2" />
                <p class="text-slate-400 text-xs font-bold uppercase">Selecione jogos</p>
            </div>

            <div v-else ref="selectionsContainer" class="flex-1 overflow-y-auto p-2 space-y-2 custom-scrollbar">
                <div v-for="item in store.selections" :key="item.id" class="bg-[#1e293b] rounded border border-slate-700/50 relative overflow-hidden group">
                    <button @click="store.removeSelection(item.id)" class="absolute top-0 right-0 p-1.5 text-slate-600 hover:text-red-500 transition-colors z-10"><X class="w-3.5 h-3.5" /></button>
                    <div class="p-2.5">
                        <div class="pr-4 mb-1.5">
                            <div class="flex items-center gap-1 text-[9px] text-slate-400 mb-0.5 font-medium"><Calendar class="w-2.5 h-2.5 opacity-70" /> {{ formatGameDate(item.commenceTime || (item as any).commence_time) }}</div>
                            <div class="text-[11px] font-bold text-white leading-tight truncate">{{ truncateName(item.homeTeam) }} <span class="text-slate-500 font-normal">vs</span> {{ truncateName(item.awayTeam) }}</div>
                        </div>
                        <div class="flex items-center justify-between gap-2">
                            <div class="flex flex-col min-w-0">
                                <span class="text-[9px] text-slate-500 font-bold uppercase truncate">{{ getMarketLabel(item.type, item.marketName) }}</span>
                                <span class="text-[11px] text-blue-400 font-bold truncate">{{ getSelectionName(item) }}</span>
                            </div>
                            <div class="bg-[#0f172a] text-yellow-400 font-mono font-bold text-xs px-2 py-1 rounded border border-slate-700 shadow-inner">{{ (item.odds || 0).toFixed(2) }}</div>
                        </div>
                    </div>
                    <div v-if="oddConflict && oddConflict.selectionId === item.id" class="absolute inset-0 border-2 border-yellow-500/50 rounded pointer-events-none animate-pulse"></div>
                    <div v-else class="absolute left-0 top-0 bottom-0 w-[2px] bg-blue-500"></div>
                </div>
            </div>
        </div>

        <div v-if="store.count > 0" class="bg-[#162032] p-3 z-20 border-t border-yellow-500/50 shadow-[0_-4px_15px_rgba(234,179,8,0.2)]">
            
            <div v-if="oddConflict" class="mb-3 bg-yellow-500/10 border border-yellow-500/30 rounded p-2 flex flex-col gap-2">
                <div class="flex items-start gap-2 text-[10px] leading-tight text-slate-300">
                    <AlertCircle class="w-4 h-4 text-yellow-500 mt-0.5 shrink-0" />
                    <div>
                        <span class="font-bold text-yellow-500">Atenção:</span> A odd de <span class="text-white font-bold">{{ oddConflict.matchName }}</span> alterou.
                        <div class="flex items-center gap-1 mt-1">
                            <span class="line-through text-red-400">{{ oddConflict.oldOdd }}</span>
                            <ArrowRight class="w-3 h-3 text-gray-400" />
                            <span class="text-green-400 font-bold">{{ oddConflict.newOdd }}</span>
                        </div>
                    </div>
                </div>
                <div class="flex gap-2 w-full"><button @click="confirmOddChange" class="flex-1 bg-yellow-600 text-white text-[10px] font-bold py-1.5 rounded uppercase">Aceitar</button><button @click="cancelOddChange" class="flex-1 bg-slate-700 text-slate-300 text-[10px] font-bold py-1.5 rounded uppercase">Remover</button></div>
            </div>

            <div class="flex flex-wrap items-end justify-between gap-2 mb-3">
                <div class="flex flex-col items-start min-w-[60px] order-1">
                    <span class="text-[10px] text-slate-500 uppercase font-bold tracking-wide mb-0.5">Odd Total</span>
                    <span class="text-yellow-400 font-bold font-mono text-sm bg-yellow-400/10 px-2 py-0.5 rounded">{{ (store.totalOdds || 0).toFixed(2) }}</span>
                </div>

                <div class="flex flex-col items-end min-w-[80px] order-3 md:order-2">
                    <span class="text-[10px] text-slate-500 uppercase font-bold tracking-wide mb-0.5">Retorno</span>
                    <span class="text-green-400 font-bold font-mono text-sm bg-green-400/10 px-2 py-0.5 rounded">{{ formatCurrency(potentialReturn) }}</span>
                </div>

                <div class="relative group order-2 md:order-3 flex-1 md:w-full md:mt-2">
                    <div class="absolute inset-y-0 left-0 pl-2.5 flex items-center pointer-events-none">
                        <span class="text-slate-500 font-bold text-xs"><Coins class="w-3 h-3" /></span>
                    </div>
                    <input v-model="stake" type="number" placeholder="Valor" :disabled="!!oddConflict || isProcessingLive" class="w-full bg-[#0b1120] text-white text-center text-sm font-bold border border-slate-700 rounded py-2 pl-6 pr-2 focus:border-blue-500 focus:ring-1 outline-none transition-all" :class="stakeError ? 'border-red-500 animate-shake' : ''" />
                    <span v-if="stakeError" class="absolute -top-6 left-1/2 -translate-x-1/2 text-red-500 bg-[#1e293b] border border-red-500 px-2 py-0.5 rounded text-[10px] font-bold whitespace-nowrap shadow-lg z-10">Inválido</span>
                </div>
            </div>

            <button @click="() => handlePlaceBet(false)" :disabled="isLoading || store.count === 0 || !!oddConflict || isProcessingLive" class="w-full py-2.5 rounded bg-gradient-to-r from-purple-600 to-purple-500 hover:from-purple-500 hover:to-purple-400 text-white font-bold text-xs uppercase tracking-widest shadow-lg shadow-purple-900/20 transition-all flex justify-center items-center gap-2 border border-purple-400/20 disabled:opacity-50">
                <Loader2 v-if="isLoading" class="w-4 h-4 animate-spin" />
                <span v-else>CONFIRMAR (TORNEIO)</span>
            </button>
        </div>

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