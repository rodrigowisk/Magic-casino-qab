<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { Trophy, Eye, X } from 'lucide-vue-next'; 
import tournamentService, { type TournamentRankingDto } from '../../services/Tournament/TournamentService';
import { useAuthStore } from '../../stores/useAuthStore';
import TournamentBetCard from '../../components/Tournament/TournamentBetCard.vue';

const route = useRoute();
const router = useRouter();
const tournamentId = Number(route.params.id);
const authStore = useAuthStore();
const ranking = ref<TournamentRankingDto[]>([]);
const isLoading = ref(true);

// --- MODAL ---
const showBetsModal = ref(false);
const isLoadingBets = ref(false);
const selectedPlayerName = ref('');
const selectedPlayerBets = ref<any[]>([]); 
const isViewingOwnBets = ref(false);

const openPlayerBets = async (player: TournamentRankingDto) => {
    selectedPlayerName.value = getProp(player, 'userName');
    isViewingOwnBets.value = isCurrentUser(player);
    showBetsModal.value = true;
    isLoadingBets.value = true;
    selectedPlayerBets.value = []; 

    try {
        const targetId = getProp(player, 'userId');
        if (targetId) {
            const res = await tournamentService.getPlayerBets(tournamentId, targetId);
            selectedPlayerBets.value = res.data.map((b: any) => ({
                id: b.id || b.Id,
                amount: b.amount || b.Amount,
                totalOdds: b.totalOdds || b.TotalOdds,
                potentialWin: b.potentialWin || b.PotentialWin,
                status: b.status || b.Status,
                placedAt: b.placedAt || b.PlacedAt,
                selections: (b.selections || b.Selections || []).map((s: any) => ({
                    homeTeam: s.homeTeam || s.HomeTeam,
                    awayTeam: s.awayTeam || s.AwayTeam,
                    marketName: s.marketName || s.MarketName,
                    selectionName: s.selectionName || s.SelectionName,
                    odds: s.odds || s.Odds,
                    status: s.status || s.Status,
                    finalScore: s.finalScore || s.FinalScore
                }))
            }));
        }
    } catch (error) {
        console.error("Erro ao buscar apostas", error);
    } finally {
        isLoadingBets.value = false;
    }
};

const closeBetsModal = () => {
    showBetsModal.value = false;
};

// ... Lógica de Polling ...
let pollingInterval: ReturnType<typeof setInterval> | null = null;
onMounted(async () => {
  await loadRanking(true);
  pollingInterval = setInterval(() => { loadRanking(false); }, 5000);
});
onUnmounted(() => {
  if (pollingInterval) clearInterval(pollingInterval);
});
const loadRanking = async (showLoading = false) => {
  if (showLoading) isLoading.value = true;
  try {
    const res = await tournamentService.getRanking(tournamentId);
    ranking.value = Array.isArray(res.data) ? res.data : [];
  } catch (error) {
    if (showLoading) ranking.value = [];
  } finally {
    if (showLoading) isLoading.value = false;
  }
};
const getRankIcon = (pos: number) => {
    if (pos === 1) return '🥇'; if (pos === 2) return '🥈'; if (pos === 3) return '🥉'; return `#${pos}`;
};
const getProp = (obj: any, key: string) => {
    if (!obj) return null;
    return obj[key] ?? obj[key.charAt(0).toUpperCase() + key.slice(1)] ?? obj[key.charAt(0).toLowerCase() + key.slice(1)] ?? null;
};
const isCurrentUser = (player: TournamentRankingDto) => {
    const playerUser = getProp(player, 'userName');
    const myUser = authStore.user?.name || authStore.user?.Name;
    return playerUser && myUser && playerUser === myUser;
};
</script>

<template>
  <div class="min-h-screen bg-[#0f172a] text-slate-200 font-sans pb-10 flex flex-col relative">
    
    <div class="max-w-5xl mx-auto px-4 mt-2 w-full flex-1">
      <div class="flex items-center justify-between mb-3 border-b border-slate-800 pb-2">
          <div class="flex items-center gap-2">
              <button @click="router.push(`/tournament/${tournamentId}/play`)" class="p-1.5 rounded-full bg-slate-800 hover:bg-slate-700 transition-colors text-slate-400 hover:text-white">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
                  </svg>
              </button>
              <h1 class="text-sm md:text-xl font-bold text-white tracking-wide uppercase">Classificação</h1>
          </div>
          <div class="text-[10px] md:text-xs text-yellow-500 uppercase font-bold tracking-widest hidden md:block">Top Jogadores</div>
      </div>

      <div v-if="isLoading" class="flex flex-col items-center justify-center py-20 opacity-50">
        <div class="w-8 h-8 border-2 border-t-transparent border-yellow-500 rounded-full animate-spin mb-3"></div>
      </div>

      <div v-else class="space-y-4">
        <div v-if="ranking.length > 0">
              <div class="grid grid-cols-[45px_1fr_75px_80px] md:grid-cols-[50px_1fr_100px_120px_100px] px-2 md:px-4 py-2 text-[9px] md:text-[10px] text-slate-500 font-bold uppercase tracking-wider bg-slate-800/50 rounded-t-lg items-center">
                  <div class="text-center">#</div>
                  <div>Jogador</div>
                  <div class="flex flex-col items-center justify-center">
                      <span>Bilhetes</span>
                      <span class="hidden sm:block text-[8px] text-slate-600 font-normal lowercase tracking-normal -mt-0.5">finalizados/total</span>
                  </div>
                  <div class="hidden md:block text-right">Potencial</div>
                  <div class="text-right">Saldo</div>
              </div>

              <div class="space-y-1 mt-1">
                  <div v-for="(player, index) in ranking" :key="index"
                      class="relative grid grid-cols-[45px_1fr_75px_80px] md:grid-cols-[50px_1fr_100px_120px_100px] items-center p-2 md:p-3 rounded-lg border transition-all group"
                      :class="isCurrentUser(player) ? 'bg-blue-600/10 border-blue-500/40' : 'bg-[#1e293b] border-slate-700/50 hover:bg-[#263345]'">
                      
                      <div class="text-center font-black text-xl md:text-lg" 
                           :class="{'text-yellow-400': getProp(player, 'posicao') === 1, 'text-slate-300': getProp(player, 'posicao') === 2, 'text-amber-700': getProp(player, 'posicao') === 3, 'text-slate-600 text-sm': getProp(player, 'posicao') > 3}">
                          {{ getRankIcon(getProp(player, 'posicao')) }}
                      </div>
                      
                      <div class="flex items-center gap-3 overflow-hidden pr-2">
                          <img :src="getProp(player, 'avatar') || '/images/avatars/1.svg'" 
                               class="w-9 h-9 md:w-10 md:h-10 rounded-full object-cover border-2 border-slate-700 bg-slate-800 shrink-0"/>
                          
                          <div class="flex flex-col min-w-0">
                              <span class="text-white font-bold text-[11px] md:text-sm truncate">{{ getProp(player, 'userName') || 'Jogador' }}</span>
                              <span v-if="getProp(player, 'posicao') === 1" class="text-[8px] md:text-[9px] text-yellow-500 font-mono">LÍDER</span>
                          </div>
                      </div>

                      <div class="flex items-center justify-center gap-1 md:gap-2 text-[10px] md:text-xs text-slate-400 font-mono">
                          <span class="px-1.5 py-0.5 bg-slate-800 rounded border border-slate-700 min-w-[35px] md:min-w-[50px] text-center">{{ getProp(player, 'progressoBilhetes') }}</span>
                          
                          <button @click="openPlayerBets(player)" 
                                  class="p-2 hover:text-white hover:bg-slate-700 rounded transition-colors group-hover:text-blue-400 shrink-0">
                              <Eye class="w-5 h-5 md:w-4 md:h-4" />
                          </button>
                      </div>

                      <div class="hidden md:block text-right">
                          <span class="text-xs text-blue-400 font-mono font-bold block">{{ Number(getProp(player, 'saldoPossivel') || 0).toFixed(2) }}</span>
                      </div>
                      <div class="text-right">
                          <span class="block text-green-400 font-mono font-bold text-xs md:text-base tracking-tight">{{ Number(getProp(player, 'saldoAtual') || 0).toFixed(2) }}</span>
                      </div>
                  </div>
              </div>
        </div>
      </div>
    </div>

    <div v-if="showBetsModal" class="fixed inset-0 z-[100] flex items-center justify-center p-2 md:p-4">
        <div class="absolute inset-0 bg-black/80 backdrop-blur-sm transition-opacity" @click="closeBetsModal"></div>

        <div class="relative w-full max-w-2xl bg-[#0f172a] border border-slate-700 rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
            
            <div class="flex items-center justify-between px-3 py-3 border-b border-slate-700 bg-[#1e293b] shrink-0">
                <div class="flex items-center gap-2 min-w-0 flex-1">
                    <Trophy class="w-4 h-4 text-yellow-500 shrink-0" />
                    <span class="text-xs md:text-sm font-bold text-white uppercase tracking-wider truncate">
                        Bilhetes de <span class="text-blue-400">{{ selectedPlayerName }}</span>
                    </span>
                </div>
                <button @click="closeBetsModal" class="text-slate-400 hover:text-white transition-colors ml-2 shrink-0">
                    <X class="w-5 h-5" />
                </button>
            </div>

            <div class="flex-1 overflow-y-auto p-2 md:p-4 bg-[#0f172a]">
                <div v-if="isLoadingBets" class="flex flex-col items-center justify-center py-10 space-y-3">
                    <div class="w-8 h-8 border-2 border-t-transparent border-blue-500 rounded-full animate-spin"></div>
                    <span class="text-xs text-slate-500 uppercase">Carregando bilhetes...</span>
                </div>

                <div v-else-if="selectedPlayerBets.length === 0" class="text-center py-10 text-slate-500">
                    <p class="text-sm">Nenhum bilhete encontrado.</p>
                </div>

                <div v-else class="space-y-3">
                    <div v-for="bet in selectedPlayerBets" :key="bet.id">
                        <TournamentBetCard :bet="bet" :is-privacy-mode="!isViewingOwnBets" />
                    </div>
                </div>
            </div>
        </div>
    </div>

  </div>
</template>