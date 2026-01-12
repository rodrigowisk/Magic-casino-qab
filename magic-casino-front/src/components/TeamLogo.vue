<script setup lang="ts">
import { ref, computed } from 'vue';
import { getTeamLogo } from '../utils/imageUtils'; // Importe sua função aqui

const props = defineProps<{
  teamName: string;
  size?: string; // Ex: 'w-6 h-6'
}>();

const isLoading = ref(true);
const hasError = ref(false);

// Monta a URL (ou usa uma padrão se der erro)
const src = computed(() => getTeamLogo(props.teamName));

const onJsonLoad = () => {
  isLoading.value = false;
};

const onError = () => {
  isLoading.value = false;
  hasError.value = true;
};
</script>

<template>
  <div class="relative overflow-hidden rounded-full shrink-0" :class="size || 'w-6 h-6'">
    
    <div 
      v-if="isLoading" 
      class="absolute inset-0 bg-white/10 animate-pulse rounded-full"
    ></div>

    <img 
      :src="src" 
      @load="onJsonLoad"
      @error="onError"
      loading="lazy" 
      class="w-full h-full object-contain transition-opacity duration-500 ease-in-out"
      :class="{ 'opacity-0': isLoading, 'opacity-100': !isLoading }"
      alt="Logo"
    />

    <div 
        v-if="hasError" 
        class="absolute inset-0 bg-gray-700 flex items-center justify-center text-[10px] font-bold text-white rounded-full"
    >
        {{ teamName.charAt(0) }}
    </div>

  </div>
</template>