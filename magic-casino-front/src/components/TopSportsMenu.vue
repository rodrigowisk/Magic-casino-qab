<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import SportsService from '../services/SportsService';

const router = useRouter();
const route = useRoute();
const loading = ref(true);
const sports = ref<any[]>([]);

// ✅ Mantém a lógica de quando exibir o menu
const shouldShowMenu = computed(() => {
  const isEventDetails = route.name === 'event-details' || route.path.includes('/event/');
  const isLivePage = route.path.includes('/live');
  return !isEventDetails && !isLivePage;
});

const goToSport = (key: string) => {
    router.push({ name: 'sport-events', params: { id: key } });
};

// Mapeamento de SVGs e Nomes (Estilo Bet365)
const mapSportVisuals = (key: string) => {
    const k = key.toLowerCase();
    
    // SVG Paths desenhados (Limpos e Leves)
    if (k.includes('soccer') || k.includes('futebol')) return { name: 'Futebol', icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z' };
    
    if (k.includes('basket')) return { name: 'Basquete', icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8 0-.46.04-.92.11-1.36.73.19 1.55.36 2.39.36 3.63 0 6.19-2.17 6.19-2.17.86.6 1.77 1.34 2.64 2.21.73-.2 1.48-.34 2.26-.41.27 1.36.41 2.76.41 4.19.03 2.82-2.3 5.18-6 5.18zm6.57-2.31c-.96.06-1.85.22-2.65.48-.96-1.04-1.98-1.91-3-2.64 1.76-1.25 3.32-2.81 4.57-4.57.73 1.02 1.6 2.04 2.64 3 .26.8.42 1.69.48 2.65l-2.04 1.08zM12 4c.67 0 1.32.09 1.94.26-.2.83-.37 1.69-.37 2.61 0 2.25 1.57 4.19 1.57 4.19-1.32 1.83-3.04 3.55-4.87 4.87 0 0-1.94-1.57-4.19-1.57-.92 0-1.78.17-2.61.37C3.59 14.13 3.5 13.48 3.5 12.81c0-3.1 1.75-5.8 4.34-7.14 2.25 2.25 5.25 3.53 8.35 3.53 1.1 0 2.15-.17 3.14-.47C17.8 5.75 15.1 4 12 4z' };
    
    if (k.includes('tennis')) return { name: 'Tênis', icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm-3.5-8c0-1.93 1.57-3.5 3.5-3.5s3.5 1.57 3.5 3.5-1.57 3.5-3.5 3.5-3.5-1.57-3.5-3.5z' };
    
    if (k.includes('mma') || k.includes('ufc')) return { name: 'MMA', icon: 'M18.6 6.62c-1.44 0-2.8.56-3.77 1.53L12 10.94 9.17 8.15c-.97-.97-2.33-1.53-3.77-1.53-2.94 0-5.33 2.39-5.33 5.33v3.7c0 .83.67 1.5 1.5 1.5h10.86c.83 0 1.5-.67 1.5-1.5v-3.7c0-2.94-2.39-5.33-5.33-5.33z' };
    
    if (k.includes('esports') || k.includes('esoccer')) return { name: 'E-Sports', icon: 'M21 16.5c0 .38-.21.71-.53.88l-7.9 4.44c-.16.12-.36.18-.57.18-.21 0-.41-.06-.57-.18l-7.9-4.44A.991.991 0 0 1 3 16.5v-9c0-.38.21-.71.53-.88l7.9-4.44c.16-.12.36-.18.57-.18.21 0 .41.06.57.18l7.9 4.44c.32.17.53.5.53.88v9zM12 4.15 6.04 7.5 12 10.85l5.96-3.35L12 4.15z' };
    
    if (k.includes('american')) return { name: 'Fut. Americano', icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm4 11h-2v2h-2v-2H8v-2h4V9h2v2h2v2z' }; // Ícone genérico para outros
    
    return { name: key, icon: 'M12 2L2 22h20L12 2zm0 3.99L18.53 19H5.47L12 5.99z' }; // Troféu simples
};

onMounted(async () => {
    try {
        const data = await SportsService.getActiveSports();
        
        // Agrupa e Soma (sua lógica original, mas simplificada para o novo visual)
        const grouped = data.reduce((acc: any, item: any) => {
            const visual = mapSportVisuals(item.key);
            const sportName = visual.name;
            if (!acc[sportName]) {
                acc[sportName] = { 
                    name: sportName, 
                    realKey: item.key, 
                    count: 0, 
                    icon: visual.icon 
                };
            }
            acc[sportName].count += item.count;
            return acc;
        }, {});
        
        // Ordena: Futebol primeiro, depois por quantidade
        sports.value = Object.values(grouped).sort((a: any, b: any) => {
            if (a.name === 'Futebol') return -1;
            return b.count - a.count;
        });

    } catch (error) {
        console.error("Erro ao carregar menu:", error);
    } finally {
        loading.value = false;
    }
});
</script>

<template>
  <div v-if="shouldShowMenu" class="w-full bg-[#202327] border-b border-white/5 py-2 mb-2 sticky top-0 z-20 shadow-md">
    
    <div v-if="loading" class="flex gap-6 px-4 animate-pulse">
        <div v-for="i in 5" :key="i" class="flex flex-col items-center gap-2">
            <div class="w-8 h-8 bg-white/10 rounded-full"></div>
            <div class="w-12 h-2 bg-white/10 rounded"></div>
        </div>
    </div>

    <div v-else class="flex items-center gap-6 px-4 overflow-x-auto no-scrollbar">
      
      <div 
        v-for="sport in sports" 
        :key="sport.name"
        @click="goToSport(sport.realKey)"
        class="group flex flex-col items-center cursor-pointer min-w-[60px] transition-all duration-300 relative"
      >
        <div 
          class="w-8 h-8 flex items-center justify-center transition-colors duration-300"
          :class="[
            route.params.id === sport.realKey 
              ? 'text-[#00FF7F]' // Cor Ativa (Verde Neon)
              : 'text-[#8799a6] group-hover:text-white' // Cor Padrão
          ]"
        >
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="w-full h-full">
            <path :d="sport.icon" />
          </svg>
        </div>

        <span 
          class="text-[11px] font-medium mt-1 uppercase tracking-wide transition-colors duration-300 whitespace-nowrap"
          :class="[
            route.params.id === sport.realKey 
              ? 'text-white font-bold' 
              : 'text-[#8799a6] group-hover:text-white'
          ]"
        >
          {{ sport.name }}
        </span>

        <span v-if="sport.count > 0" class="absolute top-0 right-1 bg-[#2c3036] text-[9px] text-white px-1 rounded-full border border-[#202327]">
            {{ sport.count }}
        </span>

        <div 
          class="h-[3px] w-full mt-1.5 rounded-full transition-all duration-300"
          :class="[
            route.params.id === sport.realKey 
              ? 'bg-[#00FF7F] opacity-100' 
              : 'bg-transparent opacity-0 group-hover:bg-white/20 group-hover:opacity-100'
          ]"
        ></div>
      </div>

    </div>
  </div>
</template>

<style scoped>
/* Esconde a barra de rolagem mas mantém o scroll funcional */
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
.no-scrollbar {
  -ms-overflow-style: none;  /* IE and Edge */
  scrollbar-width: none;  /* Firefox */
}
</style>