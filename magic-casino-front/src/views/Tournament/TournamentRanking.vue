<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { Trophy, Eye, X, ArrowLeft } from 'lucide-vue-next';
import tournamentService, { type TournamentRankingDto } from '../../services/Tournament/TournamentService';
import tournamentSignal from '../../services/Tournament/TournamentSignalService';
import { useAuthStore } from '../../stores/useAuthStore';
import TournamentBetCard from '../../components/Tournament/TournamentBetCard.vue';

// 👇 1. Adicionamos a prop para o Modal
const props = defineProps<{ tournamentIdProp?: number; isEmbedded?: boolean }>();

const route = useRoute();
const router = useRouter();
// 👇 2. Aceita o ID do Modal ou URL
const tournamentId = props.tournamentIdProp || Number(route.params.id);
const authStore = useAuthStore();
const ranking = ref<TournamentRankingDto[]>([]);
const isLoading = ref(true);

// --- MODAL ---
const showBetsModal = ref(false);
const isLoadingBets = ref(false);
const selectedPlayerName = ref('');
const selectedPlayerBets = ref<any[]>([]); 
const isViewingOwnBets = ref(false);

// ==============================================================================
// 🔧 UTILS
// ==============================================================================
const getProp = (obj: any, key: string) => {
    if (!obj) return null;
    return obj[key] 
        ?? obj[key.charAt(0).toUpperCase() + key.slice(1)] 
        ?? obj[key.toLowerCase()] 
        ?? obj[key.replace(/([A-Z])/g, "_$1").toLowerCase()] 
        ?? null;
};

const getDisplayName = (player: any) => {
    if (player.user && player.user.userName) return player.user.userName;
    return getProp(player, 'user_name') || getProp(player, 'userName') || getProp(player, 'username') || 'Jogador';
};

const isCurrentUser = (player: TournamentRankingDto) => {
    if (!authStore.user) return false;

    const normalize = (str: any) => String(str || '').trim().toLowerCase().replace('@', '').replace(/[^a-z0-9]/g, '');

    const rankingId = normalize(player.userId);
    const rankingName = normalize(player.userName);

    const u = authStore.user;
    
    const myIds = [
        normalize(u.id),
        normalize(u.Id),
        normalize(u.userId),
        normalize(u.UserId),
        normalize(u.sub),
        normalize(u.cpf),
        normalize(u.Cpf),
        normalize(u.doc)
    ];

    const myNames = [
        normalize(u.userName),
        normalize(u.username),
        normalize(u.UserName),
        normalize(u.user_name),
        normalize(u.name), 
        normalize(u.Name)
    ];

    if (rankingId && myIds.includes(rankingId)) return true;
    if (rankingName && myNames.includes(rankingName)) return true;

    return false;
};

const getRankIcon = (pos: number) => {
    if (pos === 1) return '🥇'; if (pos === 2) return '🥈'; if (pos === 3) return '🥉'; return `#${pos}`;
};

// ==============================================================================
// 🎮 AÇÕES
// ==============================================================================

const openPlayerBets = async (player: TournamentRankingDto) => {
    selectedPlayerName.value = getDisplayName(player);
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
                    finalScore: s.finalScore || s.FinalScore,
                    // 🔥 CORREÇÃO: Adicionada a extração da data do jogo, igualzinho no MyBets.vue
                    gameDate: s.gameDate || s.GameDate || s.commenceTime || s.CommenceTime || s.date || s.Date
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

// --- SIGNALR & LOAD ---
onMounted(async () => {
    await loadRanking(true);
    tournamentSignal.setRankingListener((novoRanking: any[]) => {
        if (Array.isArray(novoRanking)) { ranking.value = novoRanking; }
    });
    await tournamentSignal.start();
    await tournamentSignal.joinTournament(tournamentId);
});

onUnmounted(async () => {
    await tournamentSignal.stop();
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
</script>

<template>
  <div :class="isEmbedded ? 'h-full flex flex-col' : 'h-screen overflow-hidden bg-[#0f172a] text-slate-200 font-sans flex flex-col relative'">
    
    <div :class="isEmbedded ? 'w-full flex flex-col h-full pt-4' : 'max-w-5xl mx-auto px-4 mt-4 w-full flex flex-col h-full pb-4'">
      
      <div v-if="!isEmbedded" class="flex items-center justify-between mb-3 border-b border-slate-800 pb-2 shrink-0">
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

      <div v-else class="flex flex-col flex-1 min-h-0 overflow-hidden bg-slate-800/20 rounded-lg border border-slate-800 relative">
        
        <div class="grid grid-cols-[45px_1fr_75px_80px] md:grid-cols-[50px_1fr_100px_120px_100px] px-2 md:px-4 py-3 text-[9px] md:text-[10px] text-slate-500 font-bold uppercase tracking-wider bg-[#1e293b] border-b border-slate-700 shrink-0">
            <div class="text-center">#</div>
            <div>Jogador</div>
            <div class="flex flex-col items-center justify-center">
                <span>Bilhetes</span>
                <span class="hidden sm:block text-[8px] text-slate-600 font-normal lowercase tracking-normal -mt-0.5">finalizados/total</span>
            </div>
            <div class="hidden md:block text-right">Potencial</div>
            <div class="text-right">Saldo</div>
        </div>

        <div class="overflow-y-auto custom-scrollbar flex-1 p-2 space-y-1">
            <div v-if="ranking.length > 0">
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
                            <span class="text-white font-bold text-[11px] md:text-sm truncate">
                                {{ getDisplayName(player) }}
                            </span>
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
            <div v-else class="text-center py-10 text-slate-500 text-sm">
                Nenhum jogador no ranking ainda.
            </div>
        </div>

        <transition name="slide-up">
            <div v-if="showBetsModal" class="absolute inset-0 z-50 flex flex-col bg-[#0f172a] animate-in fade-in zoom-in-95 duration-200">
                
                <div class="flex items-center justify-between px-4 py-3 border-b border-slate-700 bg-[#1e293b] shrink-0 shadow-md">
                    <div class="flex items-center gap-3 min-w-0 flex-1">
                        <button @click="closeBetsModal" class="p-1 hover:bg-slate-700 rounded-full transition-colors">
                            <ArrowLeft class="w-5 h-5 text-slate-300" />
                        </button>
                        <div class="flex flex-col">
                             <span class="text-xs text-slate-400 uppercase tracking-wider font-bold">Bilhetes de</span>
                             <span class="text-sm md:text-base font-bold text-blue-400 truncate leading-none">{{ selectedPlayerName }}</span>
                        </div>
                    </div>
                    <button @click="closeBetsModal" class="text-slate-500 hover:text-white transition-colors ml-2 shrink-0">
                        <X class="w-6 h-6" />
                    </button>
                </div>

                <div class="flex-1 overflow-y-auto custom-scrollbar p-2 md:p-4 bg-[#0f172a]">
                    <div v-if="isLoadingBets" class="flex flex-col items-center justify-center py-20 space-y-3">
                        <div class="w-8 h-8 border-2 border-t-transparent border-blue-500 rounded-full animate-spin"></div>
                        <span class="text-xs text-slate-500 uppercase tracking-widest">Carregando bilhetes...</span>
                    </div>

                    <div v-else-if="selectedPlayerBets.length === 0" class="flex flex-col items-center justify-center py-20 text-slate-500">
                        <Trophy class="w-10 h-10 text-slate-700 mb-2 opacity-50" />
                        <p class="text-sm font-medium">Nenhum bilhete encontrado.</p>
                    </div>

                    <div v-else class="space-y-3 pb-4">
                        <div v-for="bet in selectedPlayerBets" :key="bet.id">
                            <TournamentBetCard :bet="bet" :is-privacy-mode="!isViewingOwnBets" />
                        </div>
                    </div>
                </div>
            </div>
        </transition>

      </div>
    </div>
  </div>
</template>

<style scoped>
/* Scrollbar */
.custom-scrollbar::-webkit-scrollbar {
  width: 6px;
  height: 6px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: #0f172a; 
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background-color: #334155; 
  border-radius: 4px;
}
.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background-color: #475569; 
}

/* Animação suave de entrada do modal */
.slide-up-enter-active,
.slide-up-leave-active {
  transition: transform 0.2s ease, opacity 0.2s ease;
}

.slide-up-enter-from,
.slide-up-leave-to {
  transform: translateY(10px);
  opacity: 0;
}
</style>