<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import { ArrowLeft, AlertCircle } from 'lucide-vue-next';
import Swal from 'sweetalert2';

import tournamentService from '../../services/Tournament/TournamentService';
import { useAuthStore } from '../../stores/useAuthStore';
import { usePageLoader } from '../../composables/usePageLoader';
import PageLoader from '../../components/PageLoader.vue';
import TournamentCard from '../../components/Tournament/TournamentCard.vue';
import BannerCarousel from '../../components/Tournament/BannerCarousel.vue';

// Importação do Serviço SignalR
import tournamentSignal from '../../services/Tournament/TournamentSignalService';

const props = defineProps<{ type: string }>();

const router = useRouter();
const authStore = useAuthStore();
const { isLoading, loadingProgress, startLoader, finishLoader } = usePageLoader();

const tournaments = ref<any[]>([]); 
const currentUserId = ref('');
const processingId = ref<number | null>(null);

// 1. Título da página corrigido
const pageTitle = computed(() => {
    switch (props.type) {
        case 'featured': return 'Torneios em Destaque';
        case 'free': return 'Torneios Grátis';
        case 'mine': return 'Meus Torneios';
        case 'soccer': return 'Futebol';
        case 'basketball': return 'Basquete';
        case 'tennis': return 'Tênis';
        case 'mixed': return 'Mistos';
        case 'all': return 'Todos os Torneios';
        default: return 'Lista de Torneios';
    }
});

const extractUserId = (userData: any): string => {
  if (!userData) return '';
  const rawId = userData.cpf || userData.Cpf || userData.code || userData.Code || userData.id || userData.Id || '';
  return String(rawId).replace(/\D/g, ''); 
};

const loadCurrentUser = () => {
    if (authStore.user) {
        currentUserId.value = extractUserId(authStore.user);
        return;
    } 
    if (!currentUserId.value) {
        try {
            const stored = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
            if (stored) {
                const u = JSON.parse(stored);
                currentUserId.value = extractUserId(u);
            }
        } catch (e) { console.error("Erro ao ler usuário:", e); }
    }
};

// 2. Helper para remover acentos
const normalizeSport = (sport?: string) => {
    if (!sport) return '';
    return String(sport).toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").trim();
};

// 3. Regra de filtro corrigida e ÚNICA
const matchesFilter = (t: any, type: string) => {
    const isFinished = t.isFinished || String(t.status).toUpperCase() === 'FINISHED' || t.isActive === false;
    if (isFinished) return false;

    if (type === 'featured') return (t.category || t.Category || '').toLowerCase().includes('destaque');
    if (type === 'free') return t.entryFee === 0;
    if (type === 'mine') return t.isJoined;
    
    const s = normalizeSport(t.sport);
    if (type === 'soccer') return s.includes('futebol') || s.includes('soccer');
    if (type === 'basketball') return s.includes('basket') || s.includes('basquete');
    if (type === 'tennis') return s.includes('tenis') || s.includes('tennis');
    if (type === 'mixed') return s.includes('misto') || s.includes('mix');
    
    return true; 
};

const loadData = async () => {
    startLoader();
    try {
        const res = await tournamentService.listTournaments(currentUserId.value);
        const all = res.data || [];
        tournaments.value = all.filter((t: any) => matchesFilter(t, props.type));
    } catch (e) { console.error("Erro ao carregar lista:", e); } finally { finishLoader(); }
};

watch(() => authStore.user, async (newUser) => {
    if (newUser) {
        currentUserId.value = extractUserId(newUser);
    } else {
        currentUserId.value = '';
    }
    await loadData();
});

const handleRealTimeUpdate = (data: any) => {
    if (data.status === 'Deleted' || data.deletedId) {
        const idToRemove = data.id || data.deletedId;
        tournaments.value = tournaments.value.filter(t => t.id !== idToRemove);
        return;
    }

    const updatedTournament = data;
    const index = tournaments.value.findIndex(t => t.id === updatedTournament.id);
    const isValidForThisPage = matchesFilter(updatedTournament, props.type);

    if (index !== -1) {
        if (!isValidForThisPage) {
            tournaments.value.splice(index, 1);
        } else {
            tournaments.value[index] = { ...tournaments.value[index], ...updatedTournament };
        }
    } else {
        if (isValidForThisPage) {
            tournaments.value.unshift(updatedTournament);
        }
    }
};

const enterTournament = (id: number) => {
    router.push({ name: 'TournamentPlay', params: { id } });
};

const updateLocalState = (id: number) => {
    const t = tournaments.value.find(x => x.id === id);
    if(t) {
        t.isJoined = true;
    }
};

const processJoin = async (id: number) => {
    if (!authStore.isAuthenticated && !currentUserId.value) {
        loadCurrentUser(); 
        if (!currentUserId.value) {
            Swal.fire({ title: 'Login Necessário', text: 'Entre na sua conta para participar.', icon: 'warning', background: '#0f172a', color: '#fff', confirmButtonColor: '#3b82f6' });
            return;
        }
    }

    processingId.value = id;
    
    try {
        const userName = authStore.user?.name || 'Jogador';
        const userAvatar = authStore.user?.avatar || '';

        const apiCall = tournamentService.joinTournament(id, currentUserId.value, userName, userAvatar);
        const timeout = new Promise((_, reject) => setTimeout(() => reject(new Error('Timeout')), 5000));

        await Promise.race([apiCall, timeout]);
        
        updateLocalState(id);
        
        Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: 'Inscrição Confirmada!', showConfirmButton: false, timer: 2000, background: '#0f172a', color: '#fff' });
        enterTournament(id);

    } catch (error: any) {
        if (error.message === 'Timeout') {
            updateLocalState(id);
            enterTournament(id);
            return;
        }

        const msg = error.response?.data?.message || error.response?.data || 'Erro ao entrar.';
        const msgString = typeof msg === 'string' ? msg : JSON.stringify(msg);
        
        if (msgString.toLowerCase().includes('já inscrito') || msgString.toLowerCase().includes('already joined')) {
            updateLocalState(id);
            enterTournament(id);
        } else {
            Swal.fire({ title: 'Atenção', text: msgString, icon: 'error', background: '#0f172a', color: '#fff' });
        }
    } finally {
        processingId.value = null;
    }
};

onMounted(async () => {
    loadCurrentUser();
    await loadData();

    tournamentSignal.setTournamentListListener((data: any) => {
        handleRealTimeUpdate(data);
    });

    tournamentSignal.setParticipantCountListener((tournamentId: number, count: number) => {
        const tournament = tournaments.value.find(t => t.id === tournamentId);
        if (tournament) {
            const addedPlayers = count - (tournament.participantsCount || 0);
            
            tournament.participantsCount = count;

            if (addedPlayers > 0 && tournament.entryFee > 0 && !tournament.fixedPrize) {
                const feePercent = tournament.houseFeePercent || 10; 
                const houseCut = tournament.entryFee * (feePercent / 100);
                const prizeToAdd = (tournament.entryFee - houseCut) * addedPlayers;
                
                tournament.prizePool = (tournament.prizePool || 0) + prizeToAdd;
            }
        }
    });

    await tournamentSignal.start();
    await tournamentSignal.joinLobby();
});

onUnmounted(async () => {
    await tournamentSignal.leaveLobby();
    await tournamentSignal.stop();
});
</script>

<template>
  <div class="flex flex-col h-full bg-transparent text-slate-300 relative min-h-screen pb-20">
    <PageLoader :is-loading="isLoading" :progress="loadingProgress" loading-text="Carregando Lista..." />
    
    <div class="w-full flex-1">
      <div class="pt-2">
        <BannerCarousel />
      </div>

      <div class="max-w-[1400px] mx-auto px-4 md:px-8 pt-2">
        
        <div class="mb-6 flex items-center gap-3">
          <button @click="router.back()" class="flex items-center justify-center p-1.5 rounded-lg bg-white/5 hover:bg-white/10 transition-colors border border-white/5 group">
            <ArrowLeft class="w-5 h-5 text-white group-hover:-translate-x-0.5 transition-transform" />
          </button>
          <div class="flex items-center gap-2 border-l-4 border-emerald-500 pl-3">
            <h1 class="text-xl md:text-2xl font-black text-white uppercase tracking-wider italic drop-shadow-lg">{{ pageTitle }}</h1>
          </div>
        </div>

        <div v-if="!isLoading && tournaments.length === 0" class="flex flex-col items-center justify-center h-[40vh] text-slate-500">
          <div class="bg-white/5 p-6 rounded-full mb-4 animate-pulse"><AlertCircle class="w-12 h-12 opacity-50"/></div>
          <p class="text-sm font-bold uppercase tracking-wide">Nenhum torneio encontrado.</p>
        </div>

        <div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 md:gap-6 animate-fade-in-up">
          <TournamentCard 
            v-for="t in tournaments" 
            :key="t.id"
            :tournament="t" 
            :processing-id="processingId"
            @join="() => processJoin(t.id)" 
            @enter="() => enterTournament(t.id)"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.animate-fade-in-up { animation: fadeInUp 0.5s ease-out forwards; }
@keyframes fadeInUp { from { opacity: 0; transform: translateY(20px); } to { opacity: 1; transform: translateY(0); } }
</style>