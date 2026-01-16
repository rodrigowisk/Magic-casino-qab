<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import { getTeamLogo } from '../utils/imageUtils'; 

const props = defineProps<{
  teamName: string;
  remoteUrl?: string | null;
  size?: string;
}>();

// Os 3 Níveis de Fonte
type ImageSource = 'remote' | 'local' | 'text';

// Inicia tentando 'remote' se tiver URL, senão tenta 'local'
const currentSource = ref<ImageSource>(props.remoteUrl ? 'remote' : 'local');
const isLoading = ref(true);

// Reinicia a lógica se o time mudar
watch(() => [props.teamName, props.remoteUrl], () => {
    currentSource.value = props.remoteUrl ? 'remote' : 'local';
    isLoading.value = true;
});

// Calcula qual URL usar baseada no estado atual
const finalSrc = computed(() => {
    if (currentSource.value === 'remote') return props.remoteUrl;
    if (currentSource.value === 'local') return getTeamLogo(props.teamName);
    return null;
});

const onLoad = () => {
    isLoading.value = false;
};

// 🚨 A MÁGICA ACONTECE AQUI
const onError = () => {
    // 1. Se a API falhou (404), muda para Local
    if (currentSource.value === 'remote') {
        currentSource.value = 'local';
    } 
    // 2. Se a Local falhou (404), muda para Texto (Letra)
    else if (currentSource.value === 'local') {
        currentSource.value = 'text';
        isLoading.value = false; // Texto carrega na hora
    }
};
</script>

<template>
  <div 
    class="relative overflow-hidden rounded-full shrink-0 flex items-center justify-center bg-gray-800 border border-white/10" 
    :class="size || 'w-6 h-6'"
  >
    <div v-if="isLoading" class="absolute inset-0 bg-white/10 animate-pulse z-10 rounded-full"></div>

    <img 
      v-if="currentSource !== 'text'"
      :src="finalSrc" 
      @load="onLoad"
      @error="onError" 
      loading="lazy" 
      class="w-full h-full object-contain p-0.5 transition-opacity duration-300"
      :class="{ 'opacity-0': isLoading, 'opacity-100': !isLoading }"
      :alt="teamName"
    />

    <div 
        v-else 
        class="absolute inset-0 flex items-center justify-center font-bold text-white/50 select-none"
        :class="parseInt(size?.replace(/\D/g,'') || '24') > 30 ? 'text-xs' : 'text-[9px]'"
    >
        {{ teamName.charAt(0).toUpperCase() }}
    </div>

  </div>
</template>