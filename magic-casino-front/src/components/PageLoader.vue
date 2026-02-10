<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  isLoading: boolean;
  progress: number;
  loadingText?: string;
  isAbsolute?: boolean; 
}>();

const text = computed(() => props.loadingText || 'Carregando...');
</script>

<template>
  <transition name="fade">
    <div 
      v-if="isLoading" 
      class="inset-0 flex flex-col items-center justify-center bg-[#0f172a]"
      :class="[
        isAbsolute ? 'absolute z-50' : 'fixed z-[9999]' 
      ]"
    >
        
        <div class="mb-8 animate-pulse-slow">
            <img src="/logo.png?v=4" alt="Loading" class="h-16 md:h-24 object-contain drop-shadow-[0_0_15px_rgba(34,197,94,0.3)]" />
        </div>

        <div class="w-64 h-1.5 bg-[#1e293b] rounded-full overflow-hidden relative border border-slate-800">
            <div 
                class="h-full bg-gradient-to-r from-green-500 to-green-400 shadow-[0_0_10px_#22c55e] transition-all duration-300 ease-out"
                :style="{ width: `${progress}%` }"
            ></div>
        </div>

        <p class="mt-4 text-xs font-bold text-slate-500 uppercase tracking-widest animate-pulse">
            {{ text }} {{ Math.floor(progress) }}%
        </p>
    </div>
  </transition>
</template>

<style scoped>
/* === LÓGICA DO FADE OUT === 
*/

/* Define a velocidade da transição de SAÍDA (Quando isLoading vira false) */
.fade-leave-active {
  transition: all 0.3s ease-in-out; /* 0.8 segundos para sumir suavemente */
}

/* Define a velocidade da transição de ENTRADA (Opcional) */
.fade-enter-active {
  transition: all 0.3s ease-out;
}

/* O estado final da saída (e inicial da entrada) */
.fade-enter-from,
.fade-leave-to {
  opacity: 0;          /* Fica transparente */
  filter: blur(10px);  /* Adiciona um desfoque suave enquanto some */
  transform: scale(1.05); /* Um leve zoom para dar sensação de expansão */
}

/* Animação do Logo (Pulso lento) */
@keyframes pulse-slow {
    0%, 100% { transform: scale(1); opacity: 1; }
    50% { transform: scale(1.05); opacity: 0.8; }
}
.animate-pulse-slow {
    animation: pulse-slow 2s infinite ease-in-out;
}
</style>