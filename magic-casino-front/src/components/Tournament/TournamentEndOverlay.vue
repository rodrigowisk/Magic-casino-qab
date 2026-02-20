<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { 
    Clock, Lock, Trophy, Medal, ArrowRight, 
    Hourglass, AlertTriangle, CheckCircle2 
} from 'lucide-vue-next';
import tournamentSignal from '../../services/Tournament/TournamentSignalService';

const props = withDefaults(defineProps<{
    endDate: string | number;
    isFinishedBackend: boolean;
    userRank?: number;
    userPrize?: number;
    // ✅ Recebe o estado da sidebar para ajustar o centro visual
    isSidebarOpen?: boolean; 
}>(), {
    userRank: 0,
    userPrize: 0,
    isSidebarOpen: true
});

const emit = defineEmits<{
    (e: 'time-up'): void;
    (e: 'redirect'): void; 
}>();

const now = ref(tournamentSignal.getCorrectedNow());
let timerInterval: any = null;

const timeLeft = computed(() => {
    if (!props.endDate) return 9999;
    const end = new Date(props.endDate).getTime();
    const diff = Math.floor((end - now.value) / 1000);
    return diff; 
});

const formattedTime = computed(() => {
    const s = timeLeft.value > 0 ? timeLeft.value : 0;
    return s < 10 ? `0${s}` : s;
});

type OverlayState = 'IDLE' | 'COUNTDOWN' | 'PROCESSING' | 'FINISHED';

const currentState = computed<OverlayState>(() => {
    if (props.isFinishedBackend) return 'FINISHED';
    if (timeLeft.value <= 0) return 'PROCESSING';
    if (timeLeft.value <= 60) return 'COUNTDOWN';
    return 'IDLE';
});

const rankMeta = computed(() => {
    const rank = props.userRank;
    if (rank === 1) return { color: 'text-yellow-400', bg: 'bg-yellow-500/20', border: 'border-yellow-500', icon: 'text-yellow-400', label: 'OURO' };
    if (rank === 2) return { color: 'text-slate-300', bg: 'bg-slate-400/20', border: 'border-slate-400', icon: 'text-slate-300', label: 'PRATA' };
    if (rank === 3) return { color: 'text-orange-400', bg: 'bg-orange-500/20', border: 'border-orange-500', icon: 'text-orange-400', label: 'BRONZE' };
    return { color: 'text-blue-400', bg: 'bg-blue-500/20', border: 'border-blue-500', icon: 'text-blue-400', label: 'TOP' };
});

const formatCurrency = (val: number) => {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(val);
};

onMounted(() => {
    timerInterval = setInterval(() => { now.value = tournamentSignal.getCorrectedNow(); }, 1000);
});

onUnmounted(() => {
    if (timerInterval) clearInterval(timerInterval);
});

watch(currentState, (newState) => {
    if (newState === 'PROCESSING') emit('time-up');
});
</script>

<template>
    <div class="absolute inset-0 z-[100] overflow-hidden flex flex-col pointer-events-none justify-center items-center">
        
        <transition name="slide-down">
            <div v-if="currentState === 'COUNTDOWN'" 
                 class="pointer-events-auto absolute top-[175px] right-4 z-[101] bg-red-600/90 backdrop-blur text-white px-3 py-1.5 rounded shadow-lg border border-red-400/50 flex items-center gap-2 animate-pulse"
                 :class="{'md:mr-4': isSidebarOpen}"> 
                 
                <Clock class="w-3.5 h-3.5 text-white" />
                <div class="flex flex-col">
                    <span class="text-[8px] font-bold uppercase tracking-widest opacity-90 leading-none">Fim em</span>
                    <span class="text-sm font-black tabular-nums leading-none">{{ formattedTime }}s</span>
                </div>
            </div>
        </transition>

        <transition name="fade">
            <div v-if="currentState === 'PROCESSING'" 
                 class="pointer-events-auto absolute inset-0 bg-[#020617]/95 backdrop-blur-sm flex flex-col items-center justify-center p-4 transition-all duration-300"
                 :class="{'md:pl-64': isSidebarOpen}">
                
                <div class="bg-[#0f172a] border border-slate-700 rounded-xl p-6 w-full max-w-xs shadow-2xl text-center">
                    <div class="relative inline-block mb-4">
                        <div class="absolute inset-0 bg-blue-500/20 blur-xl rounded-full animate-pulse"></div>
                        <Lock class="w-12 h-12 text-slate-400 relative z-10 mx-auto" />
                        <div class="absolute -bottom-1 -right-1 bg-[#0f172a] rounded-full p-1 border border-blue-500/30 z-20">
                            <Hourglass class="w-5 h-5 text-blue-400 animate-spin-slow" />
                        </div>
                    </div>

                    <h2 class="text-lg font-bold text-white uppercase mb-1">Encerrado</h2>
                    <p class="text-xs text-slate-400 mb-4">Aguardando resultados finais...</p>

                    <div class="bg-yellow-500/10 border border-yellow-500/20 rounded p-2 flex gap-2 items-start text-left">
                        <AlertTriangle class="w-4 h-4 text-yellow-500 shrink-0 mt-0.5" />
                        <p class="text-[10px] text-yellow-100/80 leading-tight">
                            Aguarde a finalização dos jogos pendentes para o cálculo oficial.
                        </p>
                    </div>
                </div>
            </div>
        </transition>

        <transition name="zoom">
            <div v-if="currentState === 'FINISHED'" 
                 class="pointer-events-auto absolute inset-0 bg-emerald-950/95 backdrop-blur-md flex flex-col items-center justify-center p-4 transition-all duration-300"
                 :class="{'md:pl-64': isSidebarOpen}">
                
                <div class="relative w-full max-w-xs bg-[#020617] border border-emerald-500/30 rounded-xl p-5 shadow-2xl overflow-hidden text-center">
                    
                    <div class="absolute inset-0 opacity-10 blur-2xl transition-colors duration-500" :class="rankMeta.bg"></div>

                    <div class="relative z-10 mb-4">
                        <div class="inline-flex items-center gap-1.5 bg-emerald-500/20 border border-emerald-500/30 px-3 py-1 rounded-full mb-3">
                            <CheckCircle2 class="w-3 h-3 text-emerald-400" />
                            <span class="text-[10px] font-bold text-emerald-400 uppercase tracking-wider">Finalizado</span>
                        </div>

                        <div class="flex flex-col items-center">
                            <component :is="userRank <= 3 && userRank > 0 ? Medal : Trophy" class="w-8 h-8 mb-1" :class="rankMeta.icon" />
                            <span class="text-[10px] text-slate-500 uppercase font-bold">Sua Posição</span>
                            <span class="text-3xl font-black text-white tracking-tighter leading-none" :class="rankMeta.color">
                                {{ (!userRank || userRank < 1) ? '--' : `#${userRank}` }}
                            </span>
                        </div>
                    </div>

                    <div class="relative z-10 border-t border-white/10 pt-4 mb-4">
                        <template v-if="Number(userPrize) > 0">
                            <p class="text-emerald-400 font-bold text-xs uppercase mb-1">Você Ganhou</p>
                            <div class="bg-emerald-500/10 border border-emerald-500/20 rounded-lg py-2 px-3">
                                <span class="text-xl font-black text-white tracking-tight">
                                    {{ formatCurrency(Number(userPrize)) }}
                                </span>
                            </div>
                        </template>
                        <template v-else>
                            <p class="text-[10px] text-slate-400 font-medium">
                                Não houve premiação para sua posição.<br>Mais sorte na próxima!
                            </p>
                        </template>
                    </div>

                    <button @click="emit('redirect')" 
                            class="relative z-10 w-full bg-white hover:bg-slate-200 text-[#020617] text-xs font-bold uppercase py-3 rounded-lg shadow-lg flex items-center justify-center gap-2 transition-all active:scale-95">
                        <span>Voltar ao Lobby</span>
                        <ArrowRight class="w-3 h-3" />
                    </button>

                </div>
            </div>
        </transition>

    </div>
</template>

<style scoped>
@keyframes spin-slow { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }
.animate-spin-slow { animation: spin-slow 3s linear infinite; }

.slide-down-enter-active, .slide-down-leave-active { transition: all 0.4s cubic-bezier(0.16, 1, 0.3, 1); }
.slide-down-enter-from, .slide-down-leave-to { transform: translateY(-100%); opacity: 0; }

.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

.zoom-enter-active { animation: zoom-in 0.4s cubic-bezier(0.34, 1.56, 0.64, 1); }
@keyframes zoom-in { 0% { transform: scale(0.9); opacity: 0; } 100% { transform: scale(1); opacity: 1; } }
</style>