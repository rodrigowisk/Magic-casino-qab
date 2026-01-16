<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import { getTeamLogo } from '../utils/imageUtils'; 

const props = defineProps<{
  teamName: string;
  remoteUrl?: string | null; // ✅ AGORA ELE ACEITA A URL
  size?: string;
}>();

// Estado da imagem
const hasError = ref(false);
const isLoading = ref(true);

// Se a URL mudar, resetamos o erro
watch(() => props.remoteUrl, () => {
    hasError.value = false;
    isLoading.value = true;
});

// Lógica de Decisão da URL
const finalSrc = computed(() => {
    // 1. Se tiver URL do banco e não deu erro ainda, usa ela
    if (props.remoteUrl && !hasError.value) {
        return props.remoteUrl;
    }
    // 2. Se não, tenta a pasta local
    return getTeamLogo(props.teamName);
});

const onLoad = () => isLoading.value = false;
const onError = () => {
    // Se a URL da API der 404, marcamos erro para ele tentar a local
    if (!hasError.value && props.remoteUrl) {
        hasError.value = true;
        // O computed 'finalSrc' vai mudar automaticamente para local
    } else {
        // Se já era local e deu erro, paramos de carregar (vai mostrar a letra)
        isLoading.value = false;
    }
};
</script>

<template>
  <div 
    class="relative overflow-hidden rounded-full shrink-0 flex items-center justify-center bg-gray-800 border border-white/10" 
    :class="size || 'w-6 h-6'"
  >
    <div v-if="isLoading" class="absolute inset-0 bg-white/10 animate-pulse z-10"></div>

    <img 
      v-if="!hasError || (hasError && getTeamLogo(teamName))" 
      :src="finalSrc" 
      @load="onLoad"
      @error="onError"
      loading="lazy" 
      class="w-full h-full object-contain p-0.5"
      :class="{ 'opacity-0': isLoading, 'opacity-100': !isLoading }"
      :alt="teamName"
    />

    <div 
        v-if="!isLoading && hasError" 
        class="absolute inset-0 flex items-center justify-center font-bold text-white/50 text-[10px] select-none"
    >
        {{ teamName.charAt(0).toUpperCase() }}
    </div>
  </div>
</template>