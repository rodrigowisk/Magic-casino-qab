<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch, computed } from 'vue'; 
import { useRoute, useRouter } from 'vue-router';
import { Trophy, ChevronLeft } from 'lucide-vue-next'; 

// Componentes
import TournamentHeaderCarousel from '../../components/Tournament/TournamentHeaderCarousel.vue';
import TournamentEndOverlay from '../../components/Tournament/TournamentEndOverlay.vue'; 
import TournamentBetSlip from '../../components/Tournament/TournamentBetSlip.vue'; 
import TopMenu from '../../components/Tournament/TopMenu.vue';               
import TournamentMenu from '../../components/Tournament/SidebarTournament.vue'; 
import PageLoader from '../../components/PageLoader.vue';

// Services & Stores
import tournamentService from '../../services/Tournament/TournamentService';
import tournamentSignal from '../../services/Tournament/TournamentSignalService'; 
import { useBetStore } from '../../stores/useBetStore'; 
import { useLoaderStore } from '../../stores/useLoaderStore'; 

const route = useRoute();
const router = useRouter();
const store = useBetStore();
const loaderStore = useLoaderStore(); 

// --- TRAVA DE RENDERIZAÇÃO (CORREÇÃO DO F5) ---
const isLayoutReady = ref(false); 

const tournamentId = ref(Number(route.params.id));
const myTournaments = ref<any[]>([]);
const fantasyBalance = ref(0);
const userRank = ref(0);
const userPrize = ref(0);
const currentUser = ref('');

const showBetSlip = ref(false);
const isMobile = ref(false);
const isSidebarOpen = ref(true); 

const currentTournamentEndDate = ref<string | number | null>(null);
const isTournamentFinished = ref(false); 
const isLocked = ref(false); 

let pollingInterval: any = null;
let heartbeatInterval: any = null; 

const isHistoryPage = computed(() => route.name === 'TournamentHistory');

const isGamePage = computed(() => {
  const allowedRoutes = [
    'TournamentPlay', 
    'TournamentLive', 
    'TournamentMatchDetail', 
    'TournamentLiveMatchDetail'
  ];
  return allowedRoutes.includes(route.name as string);
});

// --- LÓGICA DE DADOS ---
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
            // 1. Carrega dados do torneio atual
            if (tournamentId.value > 0) {
                const res = await tournamentService.getTournament(tournamentId.value, currentUser.value);
                if (res.data) {
                    const data = res.data as any;
                    fantasyBalance.value = data.currentFantasyBalance ?? data.initialFantasyBalance ?? 0;
                    userRank.value = data.rank || 0;
                    const rawPrize = data.myPrize || data.prize || data.winnings || data.amount || 0;
                    userPrize.value = Number(rawPrize); 
                    currentTournamentEndDate.value = data.endDate;
                    isTournamentFinished.value = data.isFinished || false; 
                    if (isTournamentFinished.value) isLocked.value = true;
                }
            }
            // 2. Carrega lista de torneios (para o carrossel e verificação de bloqueio)
            if (myTournaments.value.length === 0 || isLocked.value) {
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
                        participants: t.participantsCount,
                        isFinished: t.isFinished,
                        isActive: t.isActive
                    }));
                }
            }
        }
    } catch (e) { console.error("Erro header:", e); }
};

const handleCarouselSelect = (newId: number) => {
    if (newId === tournamentId.value) return;
    router.push(`/tournament/${newId}/play`);
};

const handleTimeUp = () => {
    isLocked.value = true;
    const index = myTournaments.value.findIndex(t => t.id === tournamentId.value);
    if (index !== -1) {
        const updated = { ...myTournaments.value[index] };
        updated.isFinished = true;
        updated.isActive = false;
        myTournaments.value[index] = updated;
        myTournaments.value = [...myTournaments.value];
    }
    if (pollingInterval) clearInterval(pollingInterval);
    pollingInterval = setInterval(() => { loadHeaderData(); }, 3000);
};

const handleRedirect = async () => {
    if (pollingInterval) clearInterval(pollingInterval);
    if (heartbeatInterval) clearInterval(heartbeatInterval);
    myTournaments.value = myTournaments.value.filter(t => t.id !== tournamentId.value);
    await router.replace({ name: 'TournamentLobby' }).catch(() => window.location.href = '/tournaments');
};

const checkScreenSize = () => {
    const width = window.innerWidth;
    isMobile.value = width < 768;
    if (isMobile.value) isSidebarOpen.value = false;
    else isSidebarOpen.value = true;
    if (!isMobile.value && store.count > 0 && !isHistoryPage.value) showBetSlip.value = true;
};

watch(() => store.count, (newVal) => {
    if (newVal > 0 && !isHistoryPage.value) showBetSlip.value = true;
});

const setupSignalR = async () => {
    try {
        await tournamentSignal.start();
        await tournamentSignal.joinTournament(tournamentId.value);
        const hubConnection = (tournamentSignal as any).connection;
        if (hubConnection) {
            hubConnection.on("TournamentTimerSync", (remainingSeconds: number) => {
                if (remainingSeconds <= 65) loadHeaderData();
            });
            hubConnection.on("TournamentFinished", () => {
                isTournamentFinished.value = true;
                isLocked.value = true;
                handleTimeUp();
                loadHeaderData(); 
            });
        }
    } catch (e) { console.warn("SignalR warning:", e); }
};

onMounted(async () => {
    store.clearStore(); 
    loadCurrentUser();
    
    // 1. Carregamos os dados ANTES de liberar o layout
    await loadHeaderData();
    
    checkScreenSize();
    window.addEventListener('resize', checkScreenSize);
    
    const currentPath = route.path.replace(/\/$/, ''); 
    const basePath = `/tournament/${tournamentId.value}`;
    if (currentPath === basePath && tournamentId.value !== 0) {
        router.replace({ name: 'TournamentPlay', params: { id: tournamentId.value } });
    }

    setupSignalR();

    heartbeatInterval = setInterval(() => {
        if (!isHistoryPage.value && tournamentId.value > 0) loadHeaderData();
    }, 5000); 

    // 2. Agora que os dados estão prontos, liberamos a interface
    isLayoutReady.value = true;
});

onUnmounted(async () => {
    window.removeEventListener('resize', checkScreenSize);
    if (pollingInterval) clearInterval(pollingInterval);
    if (heartbeatInterval) clearInterval(heartbeatInterval);
    await tournamentSignal.stop(); 
});

watch(() => route.params.id, (newId) => {
    if(newId) {
        tournamentId.value = Number(newId);
        store.clearStore(); 
        isTournamentFinished.value = false;
        isLocked.value = false; 
        userPrize.value = 0; 
        currentTournamentEndDate.value = null; 
        if (pollingInterval) { clearInterval(pollingInterval); pollingInterval = null; }
        
        loadHeaderData().then(() => {
            // isLayoutReady.value = true;
        });
        setupSignalR(); 
    }
});
</script>

<template>
    <div class="flex flex-col h-full bg-[#0f172a] text-slate-300 relative overflow-hidden">
        
        <div v-if="!isLayoutReady" class="absolute inset-0 z-[9999] bg-[#0f172a] flex items-center justify-center">
             <PageLoader 
                :is-loading="true" 
                :progress="0" 
                :is-absolute="false"
                loading-text="Preparando Arena..."
             />
        </div>

        <template v-else>
            
            <div class="flex shrink-0 z-[200] bg-[#0f172a] border-b border-white/5" v-if="!isHistoryPage">
                <div class="hidden md:flex w-64 flex-col border-r border-white/5 bg-[#0f172a] relative z-30">
                     <TopMenu />
                </div>
                
                <div class="flex-1 min-w-0 relative">
                    
                    <TournamentHeaderCarousel 
                        :tournaments="myTournaments"
                        :active-tournament-id="tournamentId"
                        :fantasy-balance="fantasyBalance"
                        :user-rank="userRank"
                        @select="handleCarouselSelect"
                        @open-history="router.push(`/tournament/${tournamentId}/my-bets`)"
                        @open-ranking="router.push(`/tournament/${tournamentId}/ranking`)"
                        @open-details="(id) => router.push(`/tournament/${id}/info`)"
                        class="!mb-0" 
                    />

                    <div v-if="isMobile && showBetSlip && !isHistoryPage" 
                         class="absolute top-0 left-0 w-full bg-[#1e293b] z-[300] shadow-2xl border-b border-yellow-500/30 flex flex-col max-h-[80vh] animate-expand-down overflow-hidden">
                        <TournamentBetSlip 
                            :is-open="true" 
                            :tournament-id="tournamentId" 
                            :fantasy-balance="fantasyBalance" 
                            :is-mobile="true" 
                            @close="showBetSlip = false" 
                            @balance-updated="loadHeaderData" 
                        />
                    </div>

                    <button v-if="!showBetSlip && store.count > 0 && !isHistoryPage && isGamePage && !isLocked && isMobile" 
                            @click="showBetSlip = true" 
                            class="absolute bottom-4 right-4 z-[250] bg-[#1e293b] hover:bg-[#253248] text-white border border-yellow-500/40 shadow-xl rounded-full w-10 h-10 flex items-center justify-center transition-all hover:scale-105 active:scale-95 group animate-bounce-in">
                        <div class="relative">
                            <Trophy class="w-5 h-5 text-yellow-500" />
                            <span class="absolute -top-2 -right-2 bg-red-600 text-white text-[9px] w-4 h-4 flex items-center justify-center rounded-full font-bold border border-[#1e293b] shadow-sm">
                                {{ store.count }}
                            </span>
                        </div>
                    </button>

                </div>
            </div>

            <div class="flex-1 overflow-hidden relative flex">
                
                <div class="absolute inset-0 z-[100] pointer-events-none flex flex-col">
                    <TournamentEndOverlay 
                        v-if="currentTournamentEndDate && isGamePage"
                        :end-date="currentTournamentEndDate"
                        :is-finished-backend="isTournamentFinished"
                        :user-rank="userRank"
                        :user-prize="userPrize"
                        :is-sidebar-open="isSidebarOpen"
                        @time-up="handleTimeUp"
                        @redirect="handleRedirect"
                    />
                </div>

                <div v-if="loaderStore.isLoading" class="absolute inset-0 z-[90] bg-[#0f172a] flex items-center justify-center">
                     <PageLoader 
                        :is-loading="loaderStore.isLoading" 
                        :progress="loaderStore.progress"
                        :loading-text="loaderStore.loadingText"
                        :is-absolute="true"
                     />
                </div>

                <aside 
                    v-if="!isHistoryPage"
                    v-show="isSidebarOpen" 
                    class="w-64 flex flex-col flex-shrink-0 transition-all duration-300 border-r border-white/5 bg-[#0f172a] z-40"
                    :class="isMobile ? 'absolute h-full shadow-2xl z-[110]' : 'relative'"
                >
                    <div class="md:hidden">
                        <TopMenu />
                    </div>

                    <div class="flex-1 overflow-hidden relative">
                        <TournamentMenu v-if="isGamePage && !isLocked" class="h-full" />
                        <div v-else class="h-full w-full bg-[#0f172a] flex flex-col items-center justify-center opacity-20">
                        </div>
                    </div>
                </aside>
                
                <div 
                    v-if="isMobile && isSidebarOpen" 
                    @click="isSidebarOpen = false"
                    class="absolute inset-0 bg-black/50 z-[105] backdrop-blur-sm md:hidden"
                ></div>

                <div class="flex-1 relative overflow-hidden flex flex-col">
                    <router-view v-slot="{ Component }">
                        <transition name="fade" mode="out-in">
                            <keep-alive>
                                <component 
                                    :is="Component" 
                                    :key="route.path" 
                                    @update-header="loadHeaderData" 
                                    @loading-start="() => loaderStore.startLoader('Carregando Torneio...')"
                                    @loading-end="loaderStore.finishLoader"
                                />
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

            <button v-if="!showBetSlip && store.count > 0 && !isHistoryPage && isGamePage && !isLocked && !isMobile" 
                    @click="showBetSlip = true" 
                    class="fixed bottom-6 right-6 z-50 bg-[#1e293b]/90 hover:bg-[#1e293b] text-white border border-yellow-500/30 shadow-2xl shadow-black/80 rounded-md px-4 py-3 flex items-center gap-3 transition-all hover:scale-105 group animate-bounce-in">
                <div class="relative">
                    <Trophy class="w-5 h-5 text-yellow-500 group-hover:rotate-12 transition-transform" />
                    <span class="absolute -top-2 -right-2 bg-red-500 text-white text-[10px] w-5 h-5 flex items-center justify-center rounded-full font-bold border-2 border-[#1e293b]">{{ store.count }}</span>
                </div>
                <span class="font-bold text-sm uppercase tracking-wide">Cupom</span>
                <ChevronLeft class="w-4 h-4 text-gray-400 group-hover:text-white" />
            </button>
            
        </template> 

    </div>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.15s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
@keyframes bounce-in { 0% { transform: scale(0.5); opacity: 0; } 50% { transform: scale(1.1); } 100% { transform: scale(1); opacity: 1; } }
.animate-bounce-in { animation: bounce-in 0.3s ease-out; }
.animate-expand-down { animation: expand-down 0.3s ease-out forwards; transform-origin: top; }
@keyframes expand-down { 
    from { transform: scaleY(0); opacity: 0; max-height: 0; } 
    to { transform: scaleY(1); opacity: 1; max-height: 80vh; } 
}
</style>