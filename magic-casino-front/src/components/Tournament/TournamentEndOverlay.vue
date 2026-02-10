<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import { Clock, Lock, CheckCircle, Loader2 } from 'lucide-vue-next';

const props = defineProps<{
    endDate: string | number;
    isFinishedBackend: boolean;
}>();

const emit = defineEmits<{
    (e: 'time-up'): void;
    (e: 'redirect'): void;
}>();

const route = useRoute();
const now = ref(Date.now());
let timerInterval: any = null;

onMounted(() => {
    timerInterval = setInterval(() => { now.value = Date.now(); }, 1000);
});

onUnmounted(() => {
    if (timerInterval) clearInterval(timerInterval);
});

const timeLeft = computed(() => {
    if (!props.endDate) return 9999;
    const end = new Date(props.endDate).getTime();
    const diff = Math.floor((end - now.value) / 1000);
    return diff > 0 ? diff : 0;
});

const showCountdown = computed(() => timeLeft.value <= 60 && timeLeft.value > 0);
const isTimeUp = computed(() => timeLeft.value <= 0);

// ✅ CORREÇÃO: Bloqueia APENAS se estiver na rota de jogar (/play)
const shouldBlockScreen = computed(() => {
    // Verifica se a URL contém '/play'. Isso garante que Ranking/Bets fiquem livres.
    return isTimeUp.value && String(route.path).includes('/play');
});

const showFinalSuccess = ref(false);

watch(isTimeUp, (val) => {
    if (val) emit('time-up');
}, { immediate: true });

watch(() => props.isFinishedBackend, (val) => {
    if (val && isTimeUp.value) {
        setTimeout(() => {
            showFinalSuccess.value = true;
            setTimeout(() => { emit('redirect'); }, 3000);
        }, 1000);
    }
});

const formattedTime = computed(() => {
    const s = timeLeft.value;
    return s < 10 ? `0${s}` : s;
});
</script>

<template>
    <div class="pointer-events-none absolute inset-0 z-[50] overflow-hidden">
        
        <transition name="slide-down">
            <div v-if="showCountdown && !showFinalSuccess" 
                 class="pointer-events-auto absolute top-6 right-4 z-[60] bg-red-600/90 backdrop-blur text-white px-4 py-2 rounded-lg shadow-xl border border-red-400/50 flex items-center gap-3 animate-pulse">
                <div class="bg-white/20 p-1.5 rounded-full">
                    <Clock class="w-4 h-4 text-white" />
                </div>
                <div class="flex flex-col">
                    <span class="text-[8px] font-bold uppercase tracking-widest opacity-90 leading-none mb-0.5">Encerrando</span>
                    <span class="text-lg font-black tabular-nums leading-none">{{ formattedTime }}s</span>
                </div>
            </div>
        </transition>

        <transition name="fade">
            <div v-if="shouldBlockScreen && !showFinalSuccess" 
                 class="pointer-events-auto absolute top-[170px] left-0 right-0 bottom-0 z-[100] bg-[#0f172a]/95 backdrop-blur-sm flex flex-col items-center justify-center text-center p-6 border-t border-white/10">
                
                <div class="relative mb-6">
                    <div class="absolute inset-0 bg-red-500 blur-3xl opacity-20 animate-pulse rounded-full"></div>
                    <Lock class="w-16 h-16 text-red-500 relative z-10" />
                </div>

                <h2 class="text-3xl font-black text-white mb-2 tracking-tight">
                    APOSTAS ENCERRADAS
                </h2>
                <p class="text-slate-400 text-sm font-medium max-w-md animate-pulse">
                    Aguardando resultado oficial...
                </p>
                <p class="text-slate-500 text-xs mt-4 bg-black/20 px-3 py-1 rounded-full border border-white/5">
                    Você pode navegar para <strong>Ranking</strong> ou <strong>Minhas Apostas</strong>
                </p>

                <div class="mt-8 flex items-center gap-3 bg-blue-500/10 px-5 py-2.5 rounded-full border border-blue-500/20">
                    <Loader2 class="w-4 h-4 text-blue-400 animate-spin" />
                    <span class="text-xs text-blue-200 font-bold uppercase tracking-wider">Processando</span>
                </div>
            </div>
        </transition>

        <transition name="zoom">
            <div v-if="showFinalSuccess" 
                 class="pointer-events-auto absolute top-[170px] left-0 right-0 bottom-0 z-[100] bg-green-900/95 backdrop-blur-xl flex flex-col items-center justify-center text-center p-6 border-t border-white/10">
                
                <div class="bg-green-500 rounded-full p-4 mb-6 shadow-[0_0_50px_rgba(34,197,94,0.6)] animate-bounce-in">
                    <CheckCircle class="w-16 h-16 text-white" />
                </div>

                <h2 class="text-3xl font-black text-white mb-2 tracking-tight">
                    FINALIZADO!
                </h2>
                <p class="text-green-200 text-sm font-bold uppercase tracking-widest">
                    Voltando para o lobby...
                </p>
            </div>
        </transition>
    </div>
</template>

<style scoped>
.slide-down-enter-active, .slide-down-leave-active { transition: all 0.5s cubic-bezier(0.16, 1, 0.3, 1); }
.slide-down-enter-from, .slide-down-leave-to { transform: translateY(-20px); opacity: 0; }

.fade-enter-active, .fade-leave-active { transition: opacity 0.5s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

.zoom-enter-active { animation: zoom-in 0.5s cubic-bezier(0.34, 1.56, 0.64, 1); }
@keyframes zoom-in {
    0% { transform: scale(0.8); opacity: 0; }
    100% { transform: scale(1); opacity: 1; }
}
</style>