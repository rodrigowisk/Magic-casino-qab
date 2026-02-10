<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch, computed } from 'vue'; // ✅ Adicionado 'computed'
import { useRoute, useRouter } from 'vue-router';
import { Trophy, ChevronLeft } from 'lucide-vue-next'; 
import TournamentHeaderCarousel from '../../components/Tournament/TournamentHeaderCarousel.vue';
import TournamentEndOverlay from '../../components/Tournament/TournamentEndOverlay.vue'; 
import TournamentBetSlip from '../../components/Tournament/TournamentBetSlip.vue'; 
import tournamentService from '../../services/Tournament/TournamentService';
import { useBetStore } from '../../stores/useBetStore'; 

const route = useRoute();
const router = useRouter();
const store = useBetStore();

const tournamentId = ref(Number(route.params.id));
const myTournaments = ref<any[]>([]);
const fantasyBalance = ref(0);
const userRank = ref(0);
const currentUser = ref('');
const loadingHeader = ref(true);

// --- ESTADOS DO CUPOM ---
const showBetSlip = ref(false);
const isMobile = ref(false);

// --- ESTADOS DO ENCERRAMENTO ---
const currentTournamentEndDate = ref<string | number | null>(null);
const isTournamentFinished = ref(false); 
let pollingInterval: any = null;

// ✅ COMPUTED: Verifica se é a página de histórico para esconder o carrossel
const isHistoryPage = computed(() => route.name === 'TournamentHistory');

const loadCurrentUser = () => {
    try {
        const stored = localStorage.getItem('user') || localStorage.getItem('user_data');
        if (stored) {
            const userData = JSON.parse(stored);
            const rawId = userData.cpf || userData.Cpf || userData.id || userData.Id || userData.code || userData.Code || userData.userName || '';
            currentUser.value = String(rawId).trim(); 
        }
    } catch (e) { console.error("Erro user:", e); }
};

const loadHeaderData = async () => {
    try {
        if (!currentUser.value) loadCurrentUser();
        if (currentUser.value) {
            // Se estivermos na página de histórico com ID 0, não tentamos carregar dados de um torneio específico
            if (tournamentId.value > 0) {
                const res = await tournamentService.getTournament(tournamentId.value, currentUser.value);
                if (res.data) {
                    fantasyBalance.value = res.data.currentFantasyBalance ?? res.data.initialFantasyBalance ?? 0;
                    userRank.value = res.data.rank || 0;
                    
                    currentTournamentEndDate.value = res.data.endDate;
                    isTournamentFinished.value = res.data.isFinished || false; 
                }
            }
            
            const listRes = await tournamentService.listTournaments(currentUser.value);
            if (listRes && listRes.data) {
                const joined = listRes.data.filter((t: any) => !t.isFinished && t.isJoined);
                myTournaments.value = joined.map((t: any) => ({
                    id: t.id || t.Id,
                    name: t.name || t.Name,
                    endDate: t.endDate || t.EndDate,
                    isJoined: t.isJoined, 
                    userRank: t.rank || t.Rank || t.userRank,
                    entryFee: t.entryFee,
                    participants: t.participantsCount
                }));
            }
        }
    } catch (e) { console.error("Erro header:", e); } finally {
        loadingHeader.value = false;
    }
};

const handleCarouselSelect = (newId: number) => {
    if (newId === tournamentId.value) return;
    router.push(`/tournament/${newId}/play`);
};

const handleTimeUp = () => {
    if (!pollingInterval) {
        pollingInterval = setInterval(() => { loadHeaderData(); }, 3000);
    }
};

const handleRedirect = async () => {
    if (pollingInterval) clearInterval(pollingInterval);
    myTournaments.value = myTournaments.value.filter(t => t.id !== tournamentId.value);
    await router.replace({ name: 'TournamentLobby' }).catch(() => {
        window.location.href = '/tournaments';
    });
};

// --- LÓGICA DO CUPOM ---
const checkScreenSize = () => {
    isMobile.value = window.innerWidth < 768;
    // Se não for mobile e tiver apostas, abre o cupom (exceto no histórico)
    if (!isMobile.value && store.count > 0 && !isHistoryPage.value) showBetSlip.value = true;
};

// Monitora o store para abrir o cupom automaticamente no desktop
watch(() => store.count, (newVal) => {
    if (newVal > 0 && !isMobile.value && !isHistoryPage.value) showBetSlip.value = true;
});

onMounted(async () => {
    store.clearStore(); // Limpa store ao entrar no torneio
    loadCurrentUser();
    await loadHeaderData();

    checkScreenSize();
    window.addEventListener('resize', checkScreenSize);

    const currentPath = route.path.replace(/\/$/, ''); 
    const basePath = `/tournament/${tournamentId.value}`;
    
    // Redireciona apenas se for a raiz do ID e não for 0 (histórico genérico)
    if (currentPath === basePath && tournamentId.value !== 0) {
        router.replace({ name: 'TournamentPlay', params: { id: tournamentId.value } });
    }
});

onUnmounted(() => {
    window.removeEventListener('resize', checkScreenSize);
    if (pollingInterval) clearInterval(pollingInterval);
});

watch(() => route.params.id, (newId) => {
    if(newId) {
        tournamentId.value = Number(newId);
        store.clearStore(); // Limpa cupom ao trocar de torneio
        
        isTournamentFinished.value = false;
        if (pollingInterval) {
            clearInterval(pollingInterval);
            pollingInterval = null;
        }
        loadHeaderData();
    }
});
</script>

<template>
    <div class="flex flex-col h-full bg-[#0f172a] text-slate-300 relative overflow-hidden">
        
        <TournamentEndOverlay 
            v-if="currentTournamentEndDate"
            :end-date="currentTournamentEndDate"
            :is-finished-backend="isTournamentFinished"
            @time-up="handleTimeUp"
            @redirect="handleRedirect"
        />

        <div class="flex-shrink-0 z-20 bg-[#0f172a]" v-if="!isHistoryPage">
            <TournamentHeaderCarousel 
                :tournaments="myTournaments"
                :active-tournament-id="tournamentId"
                :fantasy-balance="fantasyBalance"
                :user-rank="userRank"
                @select="handleCarouselSelect"
                @open-history="router.push(`/tournament/${tournamentId}/my-bets`)"
                @open-ranking="router.push(`/tournament/${tournamentId}/ranking`)"
                class="!mb-0" 
            />
        </div>

        <div class="flex-1 overflow-hidden relative flex">
            
            <div class="flex-1 relative overflow-hidden">
                <router-view v-slot="{ Component }">
                    <transition name="fade" mode="out-in">
                        <keep-alive>
                            <component :is="Component" :key="route.path" @update-header="loadHeaderData" />
                        </keep-alive>
                    </transition>
                </router-view>
            </div>

            <div v-show="showBetSlip && !isHistoryPage" class="hidden md:flex w-[320px] bg-[#1e293b] border-l border-slate-800 shadow-2xl flex-col h-full z-30 transition-all duration-300">
                <TournamentBetSlip 
                    :is-open="true" 
                    :tournament-id="tournamentId" 
                    :fantasy-balance="fantasyBalance" 
                    @close="showBetSlip = false" 
                    @balance-updated="loadHeaderData" 
                />
            </div>
        </div>

        <button v-if="!showBetSlip && store.count > 0 && !isHistoryPage" 
                @click="showBetSlip = true" 
                class="fixed bottom-6 right-6 z-50 bg-[#1e293b]/90 hover:bg-[#1e293b] text-white border border-yellow-500/30 shadow-2xl shadow-black/80 rounded-md px-4 py-3 flex items-center gap-3 transition-all hover:scale-105 group animate-bounce-in">
            <div class="relative">
                <Trophy class="w-5 h-5 text-yellow-500 group-hover:rotate-12 transition-transform" />
                <span class="absolute -top-2 -right-2 bg-red-500 text-white text-[10px] w-5 h-5 flex items-center justify-center rounded-full font-bold border-2 border-[#1e293b]">{{ store.count }}</span>
            </div>
            <span class="font-bold text-sm uppercase tracking-wide">Cupom</span>
            <ChevronLeft class="w-4 h-4 text-gray-400 group-hover:text-white" />
        </button>

        <div v-if="isMobile && showBetSlip && !isHistoryPage" class="fixed bottom-0 left-0 w-full z-[150] bg-[#1e293b] shadow-[0_-5px_30px_rgba(0,0,0,0.8)] border-t border-gray-700 rounded-t-2xl h-[50vh] flex flex-col animate-slide-up">
            <TournamentBetSlip 
                :is-open="true" 
                :tournament-id="tournamentId" 
                :fantasy-balance="fantasyBalance" 
                :is-mobile="true" 
                @close="showBetSlip = false" 
                @balance-updated="loadHeaderData" 
            />
        </div>

    </div>
</template>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.15s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@keyframes bounce-in { 0% { transform: scale(0.5); opacity: 0; } 50% { transform: scale(1.1); } 100% { transform: scale(1); opacity: 1; } }
.animate-bounce-in { animation: bounce-in 0.3s ease-out; }

.animate-slide-up { animation: slide-up 0.3s ease-out; }
@keyframes slide-up { from { transform: translateY(100%); } to { transform: translateY(0); } }
</style>