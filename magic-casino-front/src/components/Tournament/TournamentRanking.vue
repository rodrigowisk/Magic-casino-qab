<script setup lang="ts">
import { ref, watch } from 'vue'; // removeu onMounted
import { Trophy } from 'lucide-vue-next'; // removeu X e Medal
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
  isLoading.value = true;
  try {
    const res = await tournamentService.getRanking(props.tournamentId);
    ranking.value = res.data;
  } catch (error) {
    console.error("Erro ao carregar ranking", error);
  } finally {
    isLoading.value = false;
  }
};

watch(() => props.isOpen, (isOpen) => {
  if (isOpen) loadRanking();
});

const getRankIcon = (index: number) => {
    if (index === 0) return '🥇';
    if (index === 1) return '🥈';
    if (index === 2) return '🥉';
    return `#${index + 1}`;
};
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 bg-black/80 z-50 flex items-center justify-center backdrop-blur-sm animate-fade-in" @click.self="emit('close')">
    <div class="bg-[#1a2c38] w-full max-w-md rounded-xl border border-gray-700 shadow-2xl flex flex-col max-h-[80vh]">
      
      <div class="p-4 border-b border-gray-700 flex justify-between items-center bg-[#15222e] rounded-t-xl">
        <div class="flex items-center gap-2">
            <Trophy class="w-5 h-5 text-yellow-500" />
            <h3 class="text-white font-bold uppercase tracking-wide">Ranking</h3>
        </div>
        <button @click="emit('close')" class="text-gray-400 hover:text-white transition text-xl font-bold">×</button>
      </div>

      <div class="flex-1 overflow-y-auto p-2 custom-scrollbar">
         
         <div v-if="isLoading" class="text-center py-10 text-gray-500">
             <div class="loader mx-auto mb-2"></div>
             Carregando líderes...
         </div>

         <div v-else-if="ranking.length === 0" class="text-center py-10 text-gray-500">
             Ninguém pontuou ainda. Seja o primeiro!
         </div>

         <div v-else class="space-y-2">
            <div 
                v-for="(player, index) in ranking" 
                :key="player.id"
                class="flex items-center justify-between p-3 rounded-lg border transition-all"
                :class="player.userId === currentUserId ? 'bg-blue-600/20 border-blue-500/50' : 'bg-[#111]' + ' border-gray-800'"
            >
                <div class="flex items-center gap-3">
                    <span class="font-bold text-lg w-8 text-center" :class="index < 3 ? 'text-2xl' : 'text-gray-500'">
                        {{ getRankIcon(index) }}
                    </span>
                    <div class="flex flex-col">
                        <span class="text-white font-bold text-sm">
                            {{ player.userId === currentUserId ? 'Você' : `Jogador #${player.id}` }}
                        </span>
                        <span class="text-[10px] text-gray-500">ID: {{ player.userId.substring(0, 8) }}...</span>
                    </div>
                </div>
                
                <div class="text-right">
                    <span class="block text-green-400 font-mono font-bold">{{ player.fantasyBalance.toFixed(2) }}</span>
                    <span class="text-[10px] text-gray-500 uppercase">Fichas</span>
                </div>
            </div>
         </div>

      </div>

      <div class="p-4 border-t border-gray-700 bg-[#15222e] rounded-b-xl text-center">
          <p class="text-xs text-gray-400">O ranking é atualizado a cada minuto.</p>
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

.loader { border: 3px solid #333; border-top: 3px solid #eab308; border-radius: 50%; width: 24px; height: 24px; animation: spin 1s linear infinite; }
@keyframes spin { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }
</style>