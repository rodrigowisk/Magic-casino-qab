<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'; 
import { useRouter } from 'vue-router';
import Swal from 'sweetalert2';
import { Trophy, Gamepad2, Dribbble, Gem } from 'lucide-vue-next'; 

import PageLoader from '../../components/PageLoader.vue';
import { usePageLoader } from '../../composables/usePageLoader';
import tournamentService from '../../services/Tournament/TournamentService';
import { useAuthStore } from '../../stores/useAuthStore';

// ✅ IMPORTANDO OS COMPONENTES REUTILIZÁVEIS
import BannerCarousel from '../../components/Tournament/BannerCarousel.vue';
import TournamentCarousel from '../../components/Tournament/TournamentCarousel.vue';

const router = useRouter();
const authStore = useAuthStore();
const { isLoading, loadingProgress, isContentReady, startLoader, finishLoader } = usePageLoader();

// --- ESTADOS ---
const processingId = ref<number | null>(null);
const activeFilter = ref('all');
const currentUserId = ref('');
const tournaments = ref<any[]>([]);

const filters = [
  { id: 'all', label: 'Todos', icon: Trophy },
  { id: 'soccer', label: 'Futebol', icon: Gamepad2 },
  { id: 'nba', label: 'Basquete', icon: Dribbble },
  { id: 'high', label: 'High Roller', icon: Gem }
];

// --- COMPUTED (FILTROS) ---
// Lista 1: Filtrada pelos botões (Padrão)
const filteredTournaments = computed(() => {
  let list = tournaments.value;
  if (activeFilter.value === 'high') {
      list = list.filter((t: any) => t.entryFee >= 100);
  } else if (activeFilter.value === 'soccer') {
      list = list.filter((t:any) => t.sport?.toLowerCase().includes('soccer') || t.sport?.toLowerCase().includes('futebol'));
  } else if (activeFilter.value === 'nba') {
      list = list.filter((t:any) => t.sport?.toLowerCase().includes('basket'));
  }
  return list; 
});

// Lista 2: Exemplo de "Meus Torneios" ou "Destaques" (Opcional)
const myTournaments = computed(() => {
    return tournaments.value.filter(t => t.isJoined);
});

// --- MÉTODOS DE NEGÓCIO (API / AUTH) ---
// O componente visual apenas "emite" o pedido. O Lobby executa a ação.

const loadCurrentUser = () => {
    if (authStore.user) {
        const u = authStore.user;
        const rawId = u.cpf || u.Cpf || u.code || u.Code || '';
        currentUserId.value = String(rawId).replace(/\D/g, ''); 
    } else {
        try {
            const storedString = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
            if (storedString) {
                const userData = JSON.parse(storedString);
                const rawId = userData.cpf || userData.Cpf || userData.code || userData.Code || '';
                currentUserId.value = String(rawId).replace(/\D/g, ''); 
            }
        } catch (e) { console.error("Erro user:", e); }
    }
};

const loadTournaments = async () => {
    startLoader();
    try {
        const res = await tournamentService.listTournaments(currentUserId.value);
        tournaments.value = res.data || [];
    } catch (error) {
        console.error(error);
        tournaments.value = [];
    } finally {
        finishLoader();
    }
};

const enterTournament = (id: number) => {
    router.push({ name: 'TournamentPlay', params: { id: id } });
};

const processJoin = async (id: number) => {
    if (!authStore.isAuthenticated && !currentUserId.value) {
        Swal.fire({ title: 'Login Necessário', text: 'Por favor, entre na sua conta.', icon: 'warning', background: '#0f172a', color: '#fff' });
        return;
    }

    processingId.value = id;
    try {
        const userName = authStore.user?.name || authStore.user?.Name || 'Jogador';
        const userAvatar = authStore.user?.avatar || authStore.user?.Avatar || ''; 
        const userId = currentUserId.value;

        await tournamentService.joinTournament(id, userId, userName, userAvatar);
        
        // Atualiza estado local
        const localTournament = tournaments.value.find(t => t.id === id);
        if (localTournament) {
            localTournament.participantsCount = (localTournament.participantsCount || 0) + 1;
            localTournament.isJoined = true;
            if (localTournament.entryFee > 0) {
                const feePercent = localTournament.houseFeePercent || 10;
                const houseCut = localTournament.entryFee * (feePercent / 100);
                localTournament.prizePool = (localTournament.prizePool || 0) + (localTournament.entryFee - houseCut);
            }
        }

        Swal.fire({
            toast: true, position: 'top-end', icon: 'success', title: 'Inscrição Confirmada!',
            showConfirmButton: false, timer: 2000, background: '#0f172a', color: '#fff'
        });
        enterTournament(id);

    } catch (error: any) {
        const errorData = error.response?.data;
        const errorMsg = (typeof errorData === 'string' ? errorData : errorData?.error || errorData?.message || '').toLowerCase();
        
        if (errorMsg.includes('já inscrito') || errorMsg.includes('already joined')) {
            const localTournament = tournaments.value.find(t => t.id === id);
            if (localTournament) localTournament.isJoined = true;
            enterTournament(id);
        } else {
            Swal.fire({ title: 'Erro', text: errorMsg || 'Não foi possível entrar.', icon: 'error', background: '#0f172a', color: '#fff' });
        }
    } finally {
        processingId.value = null;
    }
};

onMounted(async () => {
    loadCurrentUser();
    await loadTournaments();
});
</script>

<template>
  <div class="lobby-wrapper">
    
    <PageLoader 
        :is-loading="isLoading" 
        :progress="loadingProgress" 
        loading-text="Carregando Torneios..." 
    />

    <div class="content-container transition-opacity duration-700" :class="isContentReady ? 'opacity-100' : 'opacity-0'">
        
        <BannerCarousel />

        <div class="flex items-center gap-2 mb-4 overflow-x-auto pb-2 scrollbar-hide pl-1">
             <button 
                v-for="filter in filters" 
                :key="filter.id"
                @click="activeFilter = filter.id"
                class="flex items-center gap-1.5 px-3 py-1.5 rounded text-[10px] font-bold uppercase tracking-widest transition-all border"
                :class="activeFilter === filter.id 
                    ? 'bg-white text-black border-white shadow-[0_0_10px_rgba(255,255,255,0.2)]' 
                    : 'bg-transparent text-gray-400 border-gray-800 hover:border-gray-500 hover:text-white'"
            >
                <component :is="filter.icon" class="w-3 h-3" />
                {{ filter.label }}
            </button>
        </div>

        <TournamentCarousel 
            title="Torneios Disponíveis"
            :tournaments="filteredTournaments"
            :processingId="processingId"
            @join="processJoin"
            @enter="enterTournament"
        />

        <div v-if="myTournaments.length > 0" class="mt-8">
            <TournamentCarousel 
                title="Minhas Inscrições"
                :tournaments="myTournaments"
                :processingId="processingId"
                @join="processJoin"
                @enter="enterTournament"
            />
        </div>

    </div>
  </div>
</template>

<style scoped>
.lobby-wrapper {
    padding: 1.5rem;
    background-color: #141414;
    min-height: 100vh;
    font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif;
    color: #fff;
    overflow-x: hidden;
}
.scrollbar-hide::-webkit-scrollbar { display: none; }
.scrollbar-hide { -ms-overflow-style: none; scrollbar-width: none; }
</style>