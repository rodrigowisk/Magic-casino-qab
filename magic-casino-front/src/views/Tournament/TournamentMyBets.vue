<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import TournamentService from '../../services/Tournament/TournamentService';
import tournamentSignal from '../../services/Tournament/TournamentSignalService';
import { useAuthStore } from '../../stores/useAuthStore';
import TournamentBetCard from '../../components/Tournament/TournamentBetCard.vue';

// 👇 1. Adicionamos propriedades para receber dados quando usado dentro de um Modal
const props = defineProps<{ tournamentIdProp?: number; isEmbedded?: boolean }>();

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

// 👇 2. Agora ele aceita o ID vindo do Modal ou da URL
const tournamentId = props.tournamentIdProp || Number(route.params.id);

const bets = ref<any[]>([]);
const loading = ref(true);

const getUserId = () => {
    const u = authStore.user;
    if (!u) return null;
    return u.id || u.Id || u.userId || u.UserId || u.sub || u.Sub || u.username || u.Username || u.cpf;
}

const connectToUserChannel = async () => {
    const userId = getUserId();
    
    if (userId) {
        console.log("✅ [VUE] ID encontrado:", userId);
        console.log("🔑 Entrando no canal privado...");
        await tournamentSignal.joinUserChannel(userId);
    } else {
        console.warn("⚠️ Usuário não identificado. Conteúdo do Store:", authStore.user);
    }
};

onMounted(async () => {
  await loadMyBetsHistory();

  tournamentSignal.setMyBetsListener(() => {
      console.log("⚡ [SIGNALR] Recebi comando RefreshMyBets. Atualizando lista...");
      loadMyBetsHistory(); 
  });

  await tournamentSignal.start();
  await connectToUserChannel();
});

watch(() => authStore.user, (newUser) => {
    if (newUser) {
        console.log("👤 Usuário detectado tardiamente. Conectando...");
        connectToUserChannel();
    }
}, { deep: true });

const loadMyBetsHistory = async () => {
  try {
    const response = await TournamentService.getMyBets(tournamentId);
    bets.value = response.data.map((b: any) => ({
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
  } catch (error) {
    console.error("Erro bets:", error);
  } finally {
    loading.value = false;
  }
};
</script>

<template>
  <div :class="isEmbedded ? 'h-full flex flex-col' : 'h-[calc(100dvh-160px)] bg-[#0f172a] text-slate-200 font-sans flex flex-col overflow-hidden'">
    
    <div :class="isEmbedded ? 'w-full flex flex-col h-full pt-4' : 'max-w-4xl mx-auto px-4 mt-2 w-full flex flex-col h-full'">
      
      <div v-if="!isEmbedded" class="flex-none flex items-center justify-between mb-3 border-b border-slate-800 pb-2">
          <div class="flex items-center gap-2"> 
              <button @click="router.push(`/tournament/${tournamentId}/play`)" class="p-1.5 rounded-full bg-slate-800 hover:bg-slate-700 transition-colors text-slate-400 hover:text-white">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
                  </svg>
              </button>
              <h1 class="text-sm md:text-xl font-bold text-white tracking-wide uppercase">Minhas Apostas</h1>
          </div>
          <div class="text-[10px] md:text-xs text-slate-500 uppercase font-bold tracking-widest hidden md:block">
              Histórico Completo
          </div>
      </div>

      <div class="flex-1 overflow-y-auto custom-scrollbar pr-1" :class="isEmbedded ? 'pb-10' : 'pb-32'">

        <div v-if="loading" class="flex flex-col items-center justify-center py-20 opacity-50">
          <div class="w-8 h-8 border-2 border-t-transparent border-blue-500 rounded-full animate-spin mb-3"></div>
          <span class="text-xs uppercase tracking-widest text-slate-500">Sincronizando...</span>
        </div>

        <div v-else class="space-y-4">
          
          <div v-if="bets.length === 0" class="text-center py-16 bg-[#1e293b]/30 rounded-lg border border-slate-700/30 border-dashed">
            <p class="text-sm text-slate-400 font-medium">Você ainda não apostou neste torneio.</p>
            <button v-if="!isEmbedded" @click="router.push(`/tournament/${tournamentId}/play`)" class="mt-4 px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold uppercase rounded shadow-lg transition-all">
                Fazer uma aposta
            </button>
          </div>

          <div v-for="bet in bets" :key="bet.id">
              <TournamentBetCard :bet="bet" />
          </div>

        </div>
      </div>
    </div>
  </div>
</template>