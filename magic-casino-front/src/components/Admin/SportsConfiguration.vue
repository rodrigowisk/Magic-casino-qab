<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import Swal from 'sweetalert2';
import apiSports from '../../services/apiSports'; 

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
    const response = await apiSports.get('/admin/configuration');
    
    // CORREÇÃO: Verificação segura
    const data = (response && response.data) ? response.data : [];
    
    sportsData.value = data;

    if (sportsData.value.length > 0 && !selectedSportKey.value) {
      selectedSportKey.value = sportsData.value[0].key;
    }
  } catch (error) {
    console.error('Erro ao carregar:', error);
    Swal.fire({
      icon: 'error',
      title: 'Erro de Conexão',
      text: 'Não foi possível carregar as configurações.',
      background: '#1e293b',
      color: '#fff'
    });
  } finally {
    isLoading.value = false;
  }
};

const saveChanges = async () => {
  try {
    isLoading.value = true;
    const response = await apiSports.post('/admin/configuration', sportsData.value);
    
    // CORREÇÃO: Verificação segura para mensagem
    const msg = (response && response.data && response.data.message) ? response.data.message : 'Configurações atualizadas.';

    Swal.fire({
      icon: 'success',
      title: 'Salvo!',
      text: msg,
      background: '#1e293b',
      color: '#fff',
      timer: 1500,
      showConfirmButton: false
    });
  } catch (error) {
    console.error('Erro ao salvar:', error);
    Swal.fire({
      icon: 'error',
      title: 'Erro',
      text: 'Falha ao salvar no backend.',
      background: '#1e293b',
      color: '#fff'
    });
  } finally {
    isLoading.value = false;
  }
};

// Funções Visuais
const selectSport = (key: string) => { selectedSportKey.value = key; };
const toggleLeagueExpansion = (league: League) => { league.isExpanded = !league.isExpanded; };

const syncLeagueTeams = (league: League) => {
  if (league.teams && league.teams.length > 0) {
    league.teams.forEach(team => {
      team.isActive = league.isActive;
    });
  }
};

onMounted(() => { loadConfiguration(); });
</script>

<template>
  <div class="p-4 h-full flex flex-col font-sans bg-gray-50">
    
    <div class="flex justify-between items-center mb-3 flex-shrink-0">
      <div>
        <h2 class="text-xl font-bold text-gray-800 leading-tight">Configurar Esportes</h2>
        <p class="text-xs text-gray-500">Gerencie a visibilidade do Sportbook.</p>
      </div>
      
      <div class="flex gap-2">
          <button 
          @click="loadConfiguration"
          :disabled="isLoading"
          class="bg-white border border-gray-300 hover:bg-gray-100 text-gray-700 px-3 py-1.5 rounded text-sm font-bold transition shadow-sm disabled:opacity-50">
          Recarregar
        </button>
        <button 
          @click="saveChanges"
          :disabled="isLoading"
          class="bg-green-600 hover:bg-green-700 text-white px-4 py-1.5 rounded text-sm font-bold transition shadow-sm flex items-center gap-2 disabled:opacity-50">
          <span v-if="isLoading" class="animate-spin text-xs">⏳</span>
          Salvar
        </button>
      </div>
    </div>

    <div v-if="isLoading && sportsData.length === 0" class="flex-1 flex flex-col justify-center items-center text-gray-400">
      <div class="animate-spin text-3xl mb-2">⟳</div>
      <p class="text-sm">Carregando...</p>
    </div>

    <div v-else class="flex-1 overflow-hidden flex flex-col">
      
      <section class="mb-3 flex-shrink-0">
        <div v-if="sportsData.length === 0" class="text-gray-400 text-sm italic p-2 border border-dashed rounded text-center">
          Sem dados.
        </div>

        <div class="flex gap-2 overflow-x-auto pb-2 custom-scrollbar">
          <div 
            v-for="sport in sportsData" 
            :key="sport.key"
            @click="selectSport(sport.key)"
            class="cursor-pointer relative min-w-[110px] p-2 rounded-lg border transition-all duration-150 bg-white shadow-sm hover:shadow-md select-none group"
            :class="selectedSportKey === sport.key ? 'border-blue-500 ring-1 ring-blue-500 bg-blue-50/50' : 'border-gray-200 hover:border-blue-300'"
          >
            <div class="flex justify-between items-start mb-1">
              <span class="text-xl">{{ sport.icon }}</span>
              <div @click.stop class="flex items-center transform scale-75 origin-top-right">
                  <label class="relative inline-flex items-center cursor-pointer">
                  <input type="checkbox" v-model="sport.isActive" class="sr-only peer">
                  <div class="w-9 h-5 bg-gray-200 peer-focus:outline-none rounded-full peer peer-checked:bg-blue-600 peer-checked:after:translate-x-full after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-4 after:w-4 after:transition-all"></div>
                </label>
              </div>
            </div>
            <p class="font-bold text-gray-800 text-xs truncate" :title="sport.name">{{ sport.name }}</p>
            <p class="text-[9px] uppercase font-bold mt-0.5" :class="sport.isActive ? 'text-green-600' : 'text-red-400'">
                {{ sport.isActive ? 'ON' : 'OFF' }}
            </p>
          </div>
        </div>
      </section>

      <section v-if="currentSport" class="bg-white rounded-lg shadow-sm border border-gray-200 flex-1 overflow-hidden flex flex-col">
        <div class="flex justify-between items-center px-4 py-2 bg-gray-100 border-b border-gray-200">
          <h3 class="font-bold text-gray-700 flex items-center gap-2 text-sm">
            <span class="text-lg">{{ currentSport.icon }}</span>
            Ligas de {{ currentSport.name }}
          </h3>
          <span class="text-[10px] text-gray-400 bg-white px-2 py-0.5 rounded border">
            {{ currentSport.leagues.length }} ligas encontradas
          </span>
        </div>

        <div class="flex-1 overflow-y-auto custom-scrollbar p-2">
          <div class="space-y-1"> <div 
              v-for="league in currentSport.leagues" 
              :key="league.id"
              class="border rounded overflow-hidden transition-all duration-150"
              :class="league.isActive ? 'border-gray-200' : 'border-red-100 bg-red-50/50'"
            >
              <div class="flex items-center justify-between px-3 py-1.5 bg-gray-50 hover:bg-gray-100 transition-colors h-9">
                <div class="flex items-center gap-2 flex-1 cursor-pointer select-none overflow-hidden" @click="toggleLeagueExpansion(league)">
                  <button class="text-gray-400 transition-transform duration-200 hover:text-blue-600" :class="{'rotate-90': league.isExpanded}">
                      ▶
                  </button>
                  <span class="font-semibold text-gray-700 text-xs truncate">{{ league.name }}</span>
                  <span class="text-[10px] bg-blue-100 text-blue-800 px-1.5 rounded-full font-bold min-w-[20px] text-center">
                      {{ league.teams.length }}
                  </span>
                </div>
                
                <div class="flex items-center gap-2">
                    <span class="text-[10px] font-bold w-6 text-right" :class="league.isActive ? 'text-green-600' : 'text-red-400'">
                        {{ league.isActive ? 'ON' : 'OFF' }}
                    </span>
                    <label class="relative inline-flex items-center cursor-pointer transform scale-75 origin-right">
                      <input type="checkbox" v-model="league.isActive" @change="syncLeagueTeams(league)" class="sr-only peer">
                      <div class="w-9 h-5 bg-gray-300 rounded-full peer peer-checked:bg-green-500 peer-checked:after:translate-x-full after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-4 after:w-4 after:transition-all"></div>
                    </label>
                </div>
              </div>

              <div v-if="league.isExpanded" class="p-2 bg-white border-t border-gray-100">
                <div v-if="league.teams.length === 0" class="text-xs text-gray-400 italic text-center py-1">
                    Vazio.
                </div>
                <div v-else class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-2">
                  <div v-for="team in league.teams" :key="team.id" 
                       class="flex items-center justify-between px-2 py-1 rounded border text-xs transition-all hover:border-blue-300 group" 
                       :class="team.isActive ? 'bg-white border-gray-200' : 'bg-gray-50 border-dashed border-gray-300 opacity-60'">
                    
                    <span class="truncate pr-1 font-medium text-gray-600 group-hover:text-blue-700" :title="team.name">
                        {{ team.name }}
                    </span>
                    
                    <input type="checkbox" v-model="team.isActive" 
                           class="w-3.5 h-3.5 text-blue-600 rounded border-gray-300 focus:ring-blue-500 cursor-pointer">
                  </div>
                </div>
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
/* Scrollbar fina e elegante */
.custom-scrollbar::-webkit-scrollbar { height: 6px; width: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #cbd5e1; border-radius: 3px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #94a3b8; }
</style>