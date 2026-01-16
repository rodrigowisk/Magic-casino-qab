<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';

// --- Interfaces ---
interface Team {
  id: string;
  name: string;
  isActive: boolean;
}

interface League {
  id: string;
  name: string;
  isActive: boolean;
  isExpanded?: boolean;
  teams: Team[];
}

interface Sport {
  key: string;
  name: string;
  icon: string;
  isActive: boolean;
  leagues: League[];
}

// --- Estado ---
const sportsData = ref<Sport[]>([]);
const selectedSportKey = ref<string | null>(null);
const isLoading = ref(false);

const currentSport = computed(() => 
  sportsData.value.find(s => s.key === selectedSportKey.value)
);

// --- Ações ---
const loadConfiguration = async () => {
  isLoading.value = true;
  try {
    const response = await fetch('/api/admin/configuration');
    if (!response.ok) throw new Error('Falha na comunicação');
    const data = await response.json();
    sportsData.value = data;
    if (sportsData.value.length > 0) selectedSportKey.value = sportsData.value[0].key;
  } catch (error) {
    console.error(error);
    alert('Erro ao carregar configurações.');
  } finally {
    isLoading.value = false;
  }
};

const saveChanges = async () => {
  try {
    const response = await fetch('/api/admin/configuration', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(sportsData.value)
    });
    if (!response.ok) throw new Error('Falha ao salvar');
    alert('Sucesso: Configurações salvas!');
  } catch (error) {
    alert('Erro ao salvar.');
  }
};

const selectSport = (key: string) => { selectedSportKey.value = key; };
const toggleLeagueExpansion = (league: League) => { league.isExpanded = !league.isExpanded; };

onMounted(() => { loadConfiguration(); });
</script>

<template>
  <div class="p-6 h-full flex flex-col">
    
    <div class="flex justify-between items-center mb-6">
      <div>
        <h2 class="text-2xl font-bold text-gray-800">Configurar Esportes</h2>
        <p class="text-sm text-gray-500">Defina quais esportes, ligas e times aparecem no site.</p>
      </div>
      
      <div class="flex gap-2">
          <button 
          @click="loadConfiguration"
          class="bg-gray-500 hover:bg-gray-600 text-white px-4 py-2 rounded-lg font-bold transition shadow-sm">
          Recarregar
        </button>
        <button 
          @click="saveChanges"
          class="bg-green-600 hover:bg-green-700 text-white px-6 py-2 rounded-lg font-bold transition shadow-sm">
          Salvar Alterações
        </button>
      </div>
    </div>

    <div v-if="isLoading" class="flex-1 flex justify-center items-center">
      <p class="text-xl text-gray-500 animate-pulse">Carregando dados...</p>
    </div>

    <div v-else class="flex-1 overflow-hidden flex flex-col">
      <section class="mb-6 flex-shrink-0">
        <div v-if="sportsData.length === 0" class="text-gray-400 italic">
          Nenhum dado encontrado.
        </div>

        <div class="flex gap-4 overflow-x-auto pb-4 custom-scrollbar">
          <div 
            v-for="sport in sportsData" 
            :key="sport.key"
            @click="selectSport(sport.key)"
            class="cursor-pointer relative min-w-[140px] p-3 rounded-xl border-2 transition-all duration-200 bg-white shadow-sm hover:shadow-md"
            :class="selectedSportKey === sport.key ? 'border-blue-500 ring-1 ring-blue-500' : 'border-gray-200 hover:border-blue-300'"
          >
            <div class="flex justify-between items-start mb-2">
              <span class="text-2xl">{{ sport.icon }}</span>
              <div @click.stop class="flex items-center">
                  <label class="relative inline-flex items-center cursor-pointer">
                  <input type="checkbox" v-model="sport.isActive" class="sr-only peer">
                  <div class="w-9 h-5 bg-gray-200 peer-focus:outline-none rounded-full peer peer-checked:bg-blue-600 peer-checked:after:translate-x-full after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-4 after:w-4 after:transition-all"></div>
                </label>
              </div>
            </div>
            <p class="font-bold text-gray-800 text-sm truncate">{{ sport.name }}</p>
            <p class="text-[10px] text-gray-500 uppercase">{{ sport.isActive ? 'Online' : 'Offline' }}</p>
          </div>
        </div>
      </section>

      <section v-if="currentSport" class="bg-white rounded-xl shadow p-6 flex-1 overflow-y-auto custom-scrollbar">
        <div class="flex justify-between items-center mb-4 pb-2 border-b">
          <h3 class="text-lg font-bold text-gray-700 flex items-center gap-2">
            Ligas de {{ currentSport.name }}
          </h3>
        </div>

        <div class="space-y-3">
          <div 
            v-for="league in currentSport.leagues" 
            :key="league.id"
            class="border rounded-lg overflow-hidden transition-all duration-200"
            :class="league.isActive ? 'border-gray-200' : 'border-gray-100 bg-gray-50 opacity-60'"
          >
            <div class="flex items-center justify-between p-3 bg-gray-50 hover:bg-gray-100">
              <div class="flex items-center gap-3 flex-1 cursor-pointer" @click="toggleLeagueExpansion(league)">
                <button class="text-gray-400 transition-transform duration-200" :class="{'rotate-90': league.isExpanded}">▶</button>
                <span class="font-semibold text-gray-700 text-sm">{{ league.name }}</span>
                <span class="text-xs bg-blue-100 text-blue-800 px-2 rounded-full">{{ league.teams.length }}</span>
              </div>
              <label class="relative inline-flex items-center cursor-pointer">
                <input type="checkbox" v-model="league.isActive" class="sr-only peer">
                <div class="w-9 h-5 bg-gray-200 rounded-full peer peer-checked:bg-green-500 peer-checked:after:translate-x-full after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-4 after:w-4 after:transition-all"></div>
              </label>
            </div>

            <div v-if="league.isExpanded" class="p-3 bg-white border-t border-gray-100 grid grid-cols-1 md:grid-cols-3 gap-3">
              <div v-for="team in league.teams" :key="team.id" class="flex items-center justify-between p-2 rounded border text-sm" :class="team.isActive ? 'bg-white' : 'bg-gray-50'">
                <span class="truncate pr-2">{{ team.name }}</span>
                <input type="checkbox" v-model="team.isActive" class="text-blue-600 rounded focus:ring-blue-500">
              </div>
            </div>
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
button { outline: none; }
.custom-scrollbar::-webkit-scrollbar { height: 6px; width: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #f1f1f1; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #c1c1c1; border-radius: 4px; }
</style>