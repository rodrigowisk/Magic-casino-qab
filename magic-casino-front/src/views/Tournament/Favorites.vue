<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { Trophy, ArrowLeft } from 'lucide-vue-next';
import Swal from 'sweetalert2';

import PageLoader from '../../components/PageLoader.vue';
import { usePageLoader } from '../../composables/usePageLoader';
import TournamentService from '../../services/Tournament/TournamentService';
import { useAuthStore } from '../../stores/useAuthStore';

// 👇 Importando os Componentes Reutilizáveis
import BannerCarousel from '../../components/Tournament/BannerCarousel.vue';
import TournamentCard from '../../components/Tournament/TournamentCard.vue';

const router = useRouter();
const authStore = useAuthStore();
const { isLoading, loadingProgress, isContentReady, startLoader, finishLoader } = usePageLoader();

// --- ESTADOS ---
const favorites = ref<any[]>([]);
const processingId = ref<number | null>(null);
const currentUserId = ref('');

// --- MÉTODOS DE NEGÓCIO ---
const loadCurrentUser = () => {
    if (authStore.user) {
        const u = authStore.user;
        const rawId = u.cpf || u.Cpf || u.code || u.Code || u.id || u.Id || '';
        currentUserId.value = String(rawId).replace(/\D/g, ''); 
    } else {
        try {
            const storedString = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
            if (storedString) {
                const userData = JSON.parse(storedString);
                const rawId = userData.cpf || userData.Cpf || userData.code || userData.Code || userData.id || userData.Id || '';
                currentUserId.value = String(rawId).replace(/\D/g, ''); 
            }
        } catch (e) { console.error("Erro user:", e); }
    }
};

const loadFavorites = async () => {
    startLoader(); 
    try {
        if (!currentUserId.value) loadCurrentUser();

        const idToUse = currentUserId.value;
        
        if (!idToUse) {
            console.warn("Usuário não identificado.");
            favorites.value = [];
            return;
        }

        const response = await TournamentService.listTournaments(idToUse);
        
        // Filtra apenas os que são favoritos e estão ativos
        favorites.value = response.data.filter((t: any) => 
            (!!t.isFavorite) && 
            t.isActive && 
            !t.isFinished
        );

    } catch (error) {
        console.error("Erro ao carregar favoritos", error);
        favorites.value = [];
    } finally {
        finishLoader();
    }
};

// --- AÇÕES DO COMPONENTE FILHO (TournamentCard) ---

const handleFavoriteEmit = (payload: { id: number, isFavorite: boolean }) => {
    // Se o card emitir que NÃO é mais favorito, removemos ele da lista na hora, com reatividade!
    if (!payload.isFavorite) {
        favorites.value = favorites.value.filter(t => t.id !== payload.id);
    }
};

const enterTournament = (id: number) => {
    router.push(`/tournament/${id}/play`);
};

const processJoin = async (id: number) => {
    if (!authStore.isAuthenticated && !currentUserId.value) {
        Swal.fire({ title: 'Atenção', text: 'Por favor, faça login para participar.', icon: 'warning', background: '#0f172a', color: '#fff' });
        return;
    }

    processingId.value = id;
    
    try {
        const userName = authStore.user?.name || authStore.user?.Name || 'Jogador';
        const userAvatar = authStore.user?.avatar || authStore.user?.Avatar || ''; 

        // Tenta inscrever via API
        await TournamentService.joinTournament(id, currentUserId.value, userName, userAvatar);
        
        // Sucesso: Atualiza o card localmente
        const t = favorites.value.find(x => x.id === id);
        if(t) t.isJoined = true;
        
        Swal.mixin({ toast: true, position: 'top-end', showConfirmButton: false, timer: 1500, background: '#0f172a', color: '#fff' })
            .fire({ icon: 'success', title: 'Inscrição Confirmada!' });

        enterTournament(id);
    } catch (error: any) {
        const errorMsg = error.response?.data?.message || error.response?.data?.error || '';
        
        // Trata erro de "Já inscrito" como Sucesso para evitar bloqueio
        if (errorMsg.toLowerCase().includes('já inscrito') || errorMsg.toLowerCase().includes('already joined')) {
            const t = favorites.value.find(x => x.id === id);
            if(t) t.isJoined = true;
            enterTournament(id);
        } else {
            Swal.fire({ title: 'Atenção', text: errorMsg || 'Não foi possível entrar no torneio.', icon: 'error', background: '#0f172a', color: '#fff' });
        }
    } finally {
        processingId.value = null;
    }
};

onMounted(async () => {
    loadCurrentUser();
    await loadFavorites();
});
</script>

<template>
    <div class="lobby-wrapper"> 
        
        <div class="content-container transition-opacity duration-700 opacity-100">
            
            <BannerCarousel />

            <div class="flex items-center gap-4 mb-8 pl-1">
                <button 
                    @click="router.back()" 
                    class="w-10 h-10 flex items-center justify-center rounded-xl bg-slate-800/50 hover:bg-slate-700 transition-colors border border-slate-700 hover:border-slate-600 group shadow-md"
                    title="Voltar"
                >
                    <ArrowLeft class="w-5 h-5 text-slate-400 group-hover:text-white transition-colors" />
                </button>

                <div class="flex flex-col">
                    <div class="flex items-center gap-2">
                        <h1 class="text-xl font-bold text-white tracking-tight">Meus Favoritos</h1>
                    </div>
                    <p class="text-sm text-slate-400 font-medium">Torneios ativos que você acompanha.</p>
                </div>
            </div>

            <div class="relative min-h-[400px]">
                
                <PageLoader 
                    :is-loading="isLoading" 
                    :progress="loadingProgress" 
                    loading-text="Carregando Favoritos..." 
                    class="!absolute !inset-0 !h-full !z-50 rounded-xl"
                />

                <div class="transition-opacity duration-700" :class="isContentReady ? 'opacity-100' : 'opacity-0'">
                    
                    <div v-if="favorites.length === 0" class="flex flex-col items-center justify-center py-20 text-center border border-dashed border-slate-700 rounded-xl bg-slate-800/30 mx-1">
                        <Trophy class="w-12 h-12 text-slate-600 mb-4" />
                        <h3 class="text-lg font-bold text-slate-300">Nenhum favorito encontrado</h3>
                        <p class="text-sm text-slate-500 max-w-xs mt-2">
                            Você ainda não tem favoritos ativos.
                        </p>
                        <div class="flex gap-3 mt-6">
                            <button @click="loadFavorites()" class="px-6 py-2 border border-slate-600 hover:bg-slate-700 text-white rounded-lg font-bold transition-all text-xs uppercase">
                                Recarregar
                            </button>
                            <button @click="router.push('/tournaments')" class="px-6 py-2 bg-blue-600 hover:bg-blue-500 text-white rounded-lg font-bold transition-all text-xs uppercase">
                                Ir para o Lobby
                            </button>
                        </div>
                    </div>

                    <div v-else class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 px-1">
                        <TournamentCard 
                            v-for="t in favorites" 
                            :key="t.id"
                            :tournament="t"
                            :processing-id="processingId"
                            @join="processJoin"
                            @enter="enterTournament"
                            @favorite="handleFavoriteEmit"
                        />
                    </div>

                </div>
            </div>
        </div>
    </div>
</template>

<style scoped>
.lobby-wrapper {
    padding: 1.5rem;
    background-color: transparent;
    min-height: 100vh;
    font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif;
    color: #fff;
    overflow-x: hidden;
}

.scrollbar-hide::-webkit-scrollbar { display: none; }
.scrollbar-hide { -ms-overflow-style: none; scrollbar-width: none; }
</style>