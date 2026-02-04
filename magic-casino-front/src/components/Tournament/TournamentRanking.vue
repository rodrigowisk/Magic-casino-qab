<script setup lang="ts">
import { ref, watch, onMounted } from 'vue';
import { Trophy, Loader2 } from 'lucide-vue-next'; // Adicionei Loader2
import tournamentService from '../../services/Tournament/TournamentService';

const props = defineProps<{
  isOpen: boolean;
  tournamentId: number;
  currentUserId?: string;
}>();

const emit = defineEmits(['close']);

const ranking = ref<any[]>([]);
const isLoading = ref(true);

const loadRanking = async () => {
  if (!props.isOpen) return;
  
  isLoading.value = true;
  
  // Timeout de segurança: se a API demorar +5s, libera a tela
  const safetyTimeout = setTimeout(() => {
      if (isLoading.value) {
          isLoading.value = false;
          console.warn("Ranking loading timed out");
      }
  }, 5000);

  try {
    const res = await tournamentService.getRanking(props.tournamentId);
    // Garante que é um array antes de atribuir
    ranking.value = Array.isArray(res.data) ? res.data : [];
  } catch (error) {
    console.error("Erro ao carregar ranking", error);
    ranking.value = [];
  } finally {
    clearTimeout(safetyTimeout);
    isLoading.value = false;
  }
};

watch(() => props.isOpen, (newVal) => {
  if (newVal) loadRanking();
});

onMounted(() => {
    if (props.isOpen) loadRanking();
});

const getRankIcon = (index: number) => {
    if (index === 0) return '🥇';
    if (index === 1) return '🥈';
    if (index === 2) return '🥉';
    return `#${index + 1}`;
};

// Helper blindado para formatar ID
const formatUserId = (rawId: any) => {
    const id = String(rawId || ''); // Força string
    if (!id || id === 'undefined') return 'Anônimo';
    if (id.length <= 4) return id;
    return `${id.substring(0, 3)}***${id.substring(id.length - 2)}`;
};

// Helper para pegar propriedade Case-Insensitive (UserId ou userId)
const getProp = (obj: any, key: string) => {
    if (!obj) return null;
    return obj[key] ?? obj[key.charAt(0).toUpperCase() + key.slice(1)] ?? null;
};
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 bg-black/80 z-[200] flex items-center justify-center backdrop-blur-sm animate-fade-in" @click.self="emit('close')">
    <div class="bg-[#1a2c38] w-full max-w-md rounded-xl border border-gray-700 shadow-2xl flex flex-col max-h-[80vh] m-4 font-sans text-white">
      
      <div class="p-4 border-b border-gray-700 flex justify-between items-center bg-[#15222e] rounded-t-xl">
        <div class="flex items-center gap-2">
            <Trophy class="w-5 h-5 text-yellow-500" />
            <h3 class="text-white font-bold uppercase tracking-wide">Ranking</h3>
        </div>
        <button @click="emit('close')" class="text-gray-400 hover:text-white transition text-xl font-bold bg-transparent border-0 cursor-pointer">×</button>
      </div>

      <div class="flex-1 overflow-y-auto p-2 custom-scrollbar">
          
          <div v-if="isLoading" class="text-center py-10 text-gray-500 flex flex-col items-center">
              <Loader2 class="w-8 h-8 animate-spin mb-3 text-yellow-500" />
              <span class="text-xs uppercase font-bold tracking-wide">Carregando líderes...</span>
          </div>

          <div v-else-if="ranking.length === 0" class="text-center py-10 text-gray-500">
              <Trophy class="w-12 h-12 mx-auto mb-2 opacity-20" />
              <p class="text-sm">Ninguém pontuou ainda.</p>
              <p class="text-xs mt-1">Seja o primeiro a apostar!</p>
          </div>

          <div v-else class="space-y-2">
             <div 
                 v-for="(player, index) in ranking" 
                 :key="getProp(player, 'id') || index"
                 class="flex items-center justify-between p-3 rounded-lg border transition-all"
                 :class="String(getProp(player, 'userId')) === String(currentUserId) ? 'bg-blue-600/20 border-blue-500/50' : 'bg-[#111] border-gray-800'"
             >
                 <div class="flex items-center gap-3">
                     <span class="font-bold text-lg w-8 text-center" :class="index < 3 ? 'text-2xl' : 'text-gray-500'">
                         {{ getRankIcon(index) }}
                     </span>
                     <div class="flex flex-col">
                         <span class="text-white font-bold text-sm">
                             {{ String(getProp(player, 'userId')) === String(currentUserId) ? 'Você' : `Jogador` }}
                         </span>
                         <span class="text-[10px] text-gray-500 font-mono">
                             ID: {{ formatUserId(getProp(player, 'userId')) }}
                         </span>
                     </div>
                 </div>
                 
                 <div class="text-right">
                     <span class="block text-green-400 font-mono font-bold">{{ Number(getProp(player, 'fantasyBalance') || 0).toFixed(2) }}</span>
                     <span class="text-[10px] text-gray-500 uppercase">Fichas</span>
                 </div>
             </div>
          </div>

      </div>

      <div class="p-4 border-t border-gray-700 bg-[#15222e] rounded-b-xl text-center">
          <p class="text-[10px] text-gray-400 uppercase tracking-wider">O ranking é atualizado em tempo real.</p>
      </div>

    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #0f172a; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 4px; }

.animate-fade-in { animation: fadeIn 0.2s ease-out; }
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>