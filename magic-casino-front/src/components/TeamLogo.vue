<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import { getTeamLogo } from '../utils/imageUtils';

const props = defineProps<{
  teamName: string;
  remoteUrl?: string | null;
  size?: string;
}>();

type ImageSource = 'local' | 'remote' | 'text';

// Valida URL
const isValidRemoteUrl = (url?: string | null): boolean => {
    if (!url) return false;
    if (url.includes('/0.png') || url === '0.png') return false;
    return true;
};

const currentSource = ref<ImageSource>('local');
const isLoading = ref(true);

// Reinicia quando muda o time
watch(() => props.teamName, () => {
    currentSource.value = 'local';
    isLoading.value = true;
});

const finalSrc = computed(() => {
    if (currentSource.value === 'local') return getTeamLogo(props.teamName);
    if (currentSource.value === 'remote') return props.remoteUrl ?? undefined;
    return undefined;
});

// Helper para saber se estamos no modo "Texto/Bolinha"
const isFallbackMode = computed(() => currentSource.value === 'text');

const onLoad = () => { isLoading.value = false; };

const onError = () => {
    if (currentSource.value === 'local') {
        if (isValidRemoteUrl(props.remoteUrl)) {
            currentSource.value = 'remote';
        } else {
            currentSource.value = 'text';
            isLoading.value = false;
        }
    } else if (currentSource.value === 'remote') {
        currentSource.value = 'text';
        isLoading.value = false; 
    }
};
</script>

<template>
  <div 
    class="relative shrink-0 flex items-center justify-center transition-all duration-300"
    :class="[
      size || 'w-8 h-8', 
      isFallbackMode ? 'rounded-full bg-gray-800 border border-white/10 overflow-hidden' : ''
    ]"
  >
    <div v-if="isLoading && !isFallbackMode" class="absolute inset-0 bg-white/5 animate-pulse rounded-md"></div>

    <img 
      v-if="!isFallbackMode"
      :src="finalSrc" 
      @load="onLoad"
      @error="onError" 
      loading="lazy" 
      class="w-full h-full object-contain filter drop-shadow-sm transition-opacity duration-300"
      :class="{ 'opacity-0': isLoading, 'opacity-100': !isLoading }"
      :alt="teamName"
    />

    <div 
        v-else 
        class="w-full h-full flex items-center justify-center font-bold text-white/50 select-none"
        :class="parseInt(size?.replace(/\D/g,'') || '32') > 30 ? 'text-xs' : 'text-[9px]'"
    >
        {{ teamName ? teamName.charAt(0).toUpperCase() : '?' }}
    </div>

  </div>
</template>