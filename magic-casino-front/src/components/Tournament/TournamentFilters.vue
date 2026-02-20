<script setup lang="ts">
import { Trophy, Star, Heart, Gift } from 'lucide-vue-next';

// Recebe o valor atual do filtro via v-model
defineProps<{
    modelValue: string;
}>();

// Emite a mudança para atualizar o pai
const emit = defineEmits<{
    (e: 'update:modelValue', value: string): void;
}>();

// Lista centralizada de filtros
const filters = [
  { id: 'all', label: 'Todos', icon: Trophy },
  { id: 'featured', label: 'Destaques', icon: Star },
  { id: 'favorites', label: 'Favoritos', icon: Heart },
  { id: 'free', label: 'Grátis', icon: Gift }
];
</script>

<template>
    <div class="flex items-center w-full gap-2 mb-6 mt-4 px-2">
        <button 
            v-for="filter in filters" 
            :key="filter.id"
            @click="emit('update:modelValue', filter.id)"
            class="
                /* MOBILE: Estica para preencher espaço (flex-1) */
                flex-1 
                
                /* DESKTOP: Tamanho natural (flex-none) e largura automática */
                md:flex-none md:w-auto md:px-8

                flex items-center justify-center 
                gap-1 md:gap-1.5 
                py-2 md:py-1.5 
                rounded 
                text-[9px] md:text-[10px] 
                font-bold uppercase 
                tracking-normal md:tracking-widest 
                transition-all border
                whitespace-nowrap
            "
            :class="modelValue === filter.id 
                ? 'bg-white text-black border-white shadow-[0_0_10px_rgba(255,255,255,0.2)]' 
                : 'bg-transparent text-gray-400 border-gray-800 hover:border-gray-500 hover:text-white'"
        >
            <component :is="filter.icon" class="w-3 h-3 md:w-3.5 md:h-3.5 shrink-0" />
            
            {{ filter.label }}
        </button>
    </div>
</template>

<style scoped>
/* Estilos locais se necessário */
</style>