<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'; 
import { useRouter } from 'vue-router';
import Swal from 'sweetalert2';
import { 
    Info, 
    Users, 
    Trophy,
    Gamepad2,
    Dribbble,
    Gem,
    ChevronRight,
    ChevronLeft,
    Play,
    LogIn
} from 'lucide-vue-next'; 

import PageLoader from '../../components/PageLoader.vue';
import { usePageLoader } from '../../composables/usePageLoader';
import tournamentService from '../../services/Tournament/TournamentService';
import { useAuthStore } from '../../stores/useAuthStore';

// ✅ CARREGAMENTO DAS IMAGENS
const coversModules = import.meta.glob('/src/assets/tournament_covers/*.{png,jpg,jpeg,svg,webp}', { eager: true });
const coversMap: Record<string, string> = {};
for (const path in coversModules) {
    const mod = coversModules[path] as any;
    const fileName = path.split('/').pop() || 'unknown';
    coversMap[fileName] = mod.default;
}

const router = useRouter();
const authStore = useAuthStore();
const { isLoading, loadingProgress, isContentReady, startLoader, finishLoader } = usePageLoader();

// --- ESTADOS ---
const processingId = ref<number | null>(null);
const activeFilter = ref('all');
const currentUserId = ref('');
const tournaments = ref<any[]>([]);

// ✅ ESTADOS DO CARROSSEL INFINITO
const carouselIndex = ref(0); 
const itemsPerPage = ref(6);  // Padrão Desktop (6 itens)

// --- ESTADOS DO BANNER MOBILE ---
const currentBannerIndex = ref(0);
let bannerInterval: any = null;

// ✅ CONFIGURAÇÃO DOS BANNERS
const banners = [
    {
        title: 'Copa Elite',
        tag: 'Ao Vivo',
        tagColor: 'text-red-500',
        desc: 'Os melhores jogadores disputando o prêmio.',
        style: 'bg-gradient-to-r from-red-900 to-black border-l-4 border-red-600',
        icon: Gamepad2,
        iconClass: 'rotate-12'
    },
    {
        title: 'Freeroll',
        tag: 'Em Breve',
        tagColor: 'text-blue-500',
        desc: 'Entrada gratuita com premiação real.',
        style: 'bg-gradient-to-r from-blue-900 to-black border-l-4 border-blue-600',
        icon: Trophy,
        iconClass: '-rotate-6'
    },
    {
        title: 'VIP Club',
        tag: 'High Roller',
        tagColor: 'text-yellow-500',
        desc: 'Mesas exclusivas para grandes apostadores.',
        style: 'bg-gradient-to-r from-yellow-700 to-black border-l-4 border-yellow-500',
        icon: Gem,
        iconClass: ''
    }
];

const filters = [
  { id: 'all', label: 'Todos', icon: Trophy },
  { id: 'soccer', label: 'Futebol', icon: Gamepad2 },
  { id: 'nba', label: 'Basquete', icon: Dribbble },
  { id: 'high', label: 'High Roller', icon: Gem }
];

// --- COMPUTED ---
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

// ✅ LÓGICA DO CARROSSEL INFINITO (ARRAY CIRCULAR)
const visibleTournaments = computed(() => {
    const list = filteredTournaments.value;
    const len = list.length;
    if (len === 0) return [];

    // Se a lista for menor que a quantidade por página, mostra tudo sem loop
    if (len <= itemsPerPage.value) return list;

    const result = [];
    // Renderiza itemsPerPage + 1 (buffer) para garantir a continuidade
    for (let i = 0; i < itemsPerPage.value + 1; i++) { 
        const index = (carouselIndex.value + i) % len;
        result.push(list[index]); 
    }
    return result;
});

// --- MÉTODOS ---

const navigateCarousel = (direction: 'prev' | 'next') => {
    const len = filteredTournaments.value.length;
    if (len === 0) return;

    if (direction === 'next') {
        carouselIndex.value = (carouselIndex.value + 1) % len;
    } else {
        carouselIndex.value = (carouselIndex.value - 1 + len) % len;
    }
};

// ✅ RESPONSIVIDADE AJUSTADA
const updateItemsPerPage = () => {
    const w = window.innerWidth;
    if (w < 640) itemsPerPage.value = 2;      // Mobile
    else if (w < 768) itemsPerPage.value = 3; // Tablet
    else if (w < 1280) itemsPerPage.value = 4;
    else itemsPerPage.value = 6;              // Desktop
};

const startBannerRotation = () => {
    bannerInterval = setInterval(() => {
        currentBannerIndex.value = (currentBannerIndex.value + 1) % banners.length;
    }, 5000); 
};

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
        } catch (e) {
            console.error("Erro ao ler usuário:", e);
        }
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

const showInfo = (t: any) => {
    Swal.fire({
        title: `<span style="color:#fff; font-weight:900; text-transform:uppercase;">${t.name}</span>`,
        html: `
            <div style="text-align: left; font-size: 0.85rem; color: #cbd5e1; line-height: 1.6; background: #1e293b; padding: 15px; border-radius: 8px;">
                <p><strong>🏆 Modalidade:</strong> <span style="color:#fbbf24">${t.sport || 'Geral'}</span></p>
                <p><strong>📂 Categoria:</strong> ${t.category || t.Category || 'Geral'}</p>
                <p><strong>📝 Descrição:</strong> ${t.description || 'Sem descrição extra.'}</p>
                <hr style="border-color: #334155; margin: 10px 0;">
                <p><strong>📜 Regras Básicas:</strong></p>
                <p>${t.rules || 'Regras padrão da plataforma aplicáveis.'}</p>
            </div>
        `,
        background: '#0f172a',
        confirmButtonColor: '#e50914', 
        confirmButtonText: 'FECHAR',
        width: '350px'
    });
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

const handleAction = (t: any) => {
    if (t.isJoined) {
        enterTournament(t.id);
    } else {
        Swal.fire({
            title: 'Confirmar Entrada?',
            html: `
                <div class="flex flex-col items-center gap-2">
                    <span class="text-xs text-gray-400 uppercase tracking-widest">Valor da Inscrição</span>
                    <span class="text-3xl font-black text-white" style="text-shadow: 0 0 20px rgba(255,255,255,0.2)">R$ ${formatCurrency(t.entryFee)}</span>
                    <span class="text-[10px] text-gray-500 mt-1 font-mono">ID: #${t.id}</span>
                </div>
            `,
            showCancelButton: true,
            confirmButtonText: 'CONFIRMAR',
            cancelButtonText: 'CANCELAR',
            background: '#000', color: '#fff', 
            confirmButtonColor: '#e50914', cancelButtonColor: '#334155',
            width: '320px'
        }).then((result) => {
            if (result.isConfirmed) processJoin(t.id);
        });
    }
};

const formatCurrency = (val: number) => val.toFixed(2).replace('.', ',');
const formatDateSimple = (dateStr: string) => {
    if(!dateStr) return '--/--';
    const d = new Date(dateStr);
    return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' }) + ' ' + d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
};

const getCardImage = (t: any) => {
    const rawImageName = t?.coverImage || t?.CoverImage;
    const imageName = typeof rawImageName === 'string' ? rawImageName : '';

    if (imageName && coversMap[imageName]) {
        return coversMap[imageName];
    }
    
    const keys = Object.keys(coversMap);
    if (keys.length > 0) {
        const firstKey = keys[0] as string; 
        return coversMap[firstKey];
    }
    return ''; 
};

onMounted(async () => {
    loadCurrentUser();
    await loadTournaments();
    startBannerRotation();
    updateItemsPerPage();
    window.addEventListener('resize', updateItemsPerPage);
});

onUnmounted(() => {
    if (bannerInterval) clearInterval(bannerInterval);
    window.removeEventListener('resize', updateItemsPerPage);
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
        
        <div class="block md:hidden mb-6 relative h-[140px] overflow-hidden rounded-lg">
            <transition-group name="fade-banner">
                <div 
                    v-for="(banner, index) in banners" 
                    :key="index"
                    v-show="currentBannerIndex === index"
                    class="promo-banner absolute inset-0 w-full"
                    :class="banner.style"
                >
                    <div class="relative z-10 p-4 flex flex-col justify-center h-full">
                        <span class="text-[9px] font-black uppercase tracking-widest mb-0.5" :class="banner.tagColor">{{ banner.tag }}</span>
                        <h3 class="text-lg font-black italic uppercase leading-none text-white mb-1">{{ banner.title }}</h3>
                        <p class="text-[10px] text-gray-400 max-w-[200px]">{{ banner.desc }}</p>
                    </div>
                    <component :is="banner.icon" class="absolute right-3 top-1/2 -translate-y-1/2 w-16 h-16 text-white/5" :class="banner.iconClass" />
                </div>
            </transition-group>

            <div class="absolute bottom-2 left-1/2 -translate-x-1/2 flex gap-1 z-20">
                <span 
                    v-for="(_, idx) in banners" 
                    :key="idx"
                    class="h-1.5 rounded-full transition-all duration-300"
                    :class="currentBannerIndex === idx ? 'bg-white w-4' : 'bg-white/30 w-1.5'"
                ></span>
            </div>
        </div>

        <div class="hidden md:grid grid-cols-3 gap-3 mb-6">
            <div v-for="(banner, index) in banners" :key="index" class="promo-banner" :class="banner.style">
                <div class="relative z-10 p-4 flex flex-col justify-center h-full">
                    <span class="text-[9px] font-black uppercase tracking-widest mb-0.5" :class="banner.tagColor">{{ banner.tag }}</span>
                    <h3 class="text-lg font-black italic uppercase leading-none text-white mb-1">{{ banner.title }}</h3>
                    <p class="text-[10px] text-gray-400 max-w-[120px]">{{ banner.desc }}</p>
                </div>
                <component :is="banner.icon" class="absolute right-3 top-1/2 -translate-y-1/2 w-16 h-16 text-white/5" :class="banner.iconClass" />
            </div>
        </div>

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

        <div class="mb-3 flex items-center gap-2 pl-1">
            <h2 class="text-base font-bold text-white tracking-wide">Torneios Disponíveis</h2>
            <ChevronRight class="w-3.5 h-3.5 text-gray-600" />
        </div>

        <div class="carousel-container relative group/carousel min-h-[350px]">
            
            <button 
                v-if="filteredTournaments.length > itemsPerPage" 
                @click="navigateCarousel('prev')" 
                class="handle handle-prev z-50 md:opacity-0 md:group-hover:opacity-100"
            >
                <ChevronLeft class="w-8 h-8 text-white transition-transform transform md:group-hover:scale-125" />
            </button>

            <div class="slider flex gap-4 overflow-hidden px-2 pb-4 pt-4">
                
                <div v-if="!isLoading && filteredTournaments.length === 0" class="h-40 w-full flex items-center justify-center text-xs text-gray-500 border border-dashed border-gray-800 rounded-lg">
                    <p>Nenhum torneio encontrado.</p>
                </div>

                <transition-group name="list-anim">
                    <div 
                        v-for="t in visibleTournaments" 
                        :key="t.id" 
                        class="movie-card shrink-0"
                        @click="handleAction(t)"
                    >
                        <div class="card-cover">
                            <img :src="getCardImage(t)" alt="Tournament Cover" 
                                class="w-full h-full object-cover transition-transform duration-700 ease-out group-hover:scale-110 opacity-90">
                            <div class="absolute inset-0 bg-gradient-to-t from-[#050505] via-[#050505]/60 to-transparent"></div>
                        </div>

                        <div class="card-body">
                            
                            <div class="flex justify-between items-start w-full relative z-20">
                                <div class="flex flex-col max-w-[75%]">
                                    <h3 class="text-[13px] font-black text-white leading-tight uppercase drop-shadow-md line-clamp-2 min-h-[2.4em] overflow-hidden text-ellipsis">
                                        {{ t.name }}
                                    </h3>
                                    <span class="text-[8px] font-mono text-gray-300 mt-0.5 opacity-80">ID: #{{ t.id }}</span>
                                </div>

                                <div class="flex items-center gap-1 bg-black/60 border border-white/10 px-1.5 py-0.5 rounded backdrop-blur-md shadow-sm shrink-0 h-fit">
                                    <Users class="w-2.5 h-2.5 text-gray-300" />
                                    <span class="text-[8px] font-bold text-white">
                                        {{ t.participantsCount }}
                                        <span class="text-gray-500" v-if="t.maxParticipants > 0">/{{ t.maxParticipants }}</span>
                                        <span class="text-gray-500" v-else> (∞)</span>
                                    </span>
                                </div>
                            </div>

                            <div class="flex flex-col gap-1 mt-auto mb-2 relative z-20">
                                <div class="flex items-center gap-1.5 mb-1">
                                    <span class="w-0.5 h-3 bg-red-600 rounded-full shadow-[0_0_8px_red]"></span>
                                    <span class="text-[8px] font-bold uppercase tracking-widest text-gray-300">
                                        {{ t.sport || 'Esporte' }}
                                    </span>
                                </div>

                                <div class="flex flex-col">
                                    <span class="text-[7px] text-gray-500 uppercase font-bold tracking-wider">Premiação Atual</span>
                                    <span class="text-lg font-black text-emerald-400 drop-shadow-sm tracking-tight leading-none">
                                        <span class="text-xs font-normal text-emerald-600 mr-0.5">R$</span>
                                        {{ formatCurrency(t.prizePool || 0).replace('R$', '').trim() }}
                                    </span>
                                </div>

                                <div class="flex items-center gap-3 text-[8px] text-gray-400 border-t border-white/5 pt-1.5 mt-1">
                                    <div>
                                        <span class="block text-gray-600 uppercase text-[7px] mb-0.5">Início</span>
                                        <div class="flex items-center gap-1">
                                            <span class="w-1 h-1 bg-green-500 rounded-full"></span>
                                            <span class="font-medium text-gray-300">{{ formatDateSimple(t.startDate).split(' ')[0] }}</span>
                                        </div>
                                    </div>
                                    <div>
                                        <span class="block text-gray-600 uppercase text-[7px] mb-0.5">Encerramento</span>
                                        <div class="flex items-center gap-1">
                                            <span class="w-1 h-1 bg-red-500 rounded-full"></span>
                                            <span class="font-medium text-gray-300">{{ formatDateSimple(t.endDate).split(' ')[0] }}</span>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <div class="relative z-30 w-full">
                                <div class="flex justify-end items-end mb-2">
                                    <div v-if="t.entryFee == 0" class="flex flex-col items-end">
                                        <span class="bg-green-500 text-black text-[10px] font-black uppercase px-2 py-0.5 rounded shadow-[0_0_10px_rgba(34,197,94,0.4)] animate-pulse tracking-wide">
                                            Grátis
                                        </span>
                                    </div>
                                    <div v-else class="flex flex-col items-end">
                                        <span class="text-[7px] text-gray-500 uppercase font-bold tracking-wider">Entrada</span>
                                        <span class="text-xs font-bold text-white leading-none">R$ {{ formatCurrency(t.entryFee).replace('R$', '') }}</span>
                                    </div>
                                </div>

                                <div class="flex items-center gap-2">
                                    <button @click.stop="showInfo(t)" class="p-1.5 rounded border border-gray-600 text-gray-400 hover:text-white hover:border-white transition-colors bg-black/60 hover:bg-white/10">
                                        <Info class="w-3.5 h-3.5" />
                                    </button>

                                    <button 
                                        @click.stop="t.isJoined ? enterTournament(t.id) : handleAction(t)"
                                        class="flex-1 flex items-center justify-center gap-1.5 py-1.5 px-3 rounded font-bold text-[9px] uppercase tracking-wide shadow-lg transition-all transform active:scale-95 group-btn border border-transparent"
                                        :class="t.isJoined 
                                            ? 'bg-white text-black hover:bg-gray-200' 
                                            : 'bg-red-600 text-white hover:bg-red-700 hover:border-red-500/50'"
                                        :disabled="processingId === t.id"
                                    >
                                        <span v-if="processingId === t.id" class="loader-spin border-current w-3 h-3"></span>
                                        <template v-else>
                                            <component :is="t.isJoined ? Play : LogIn" class="w-3 h-3 fill-current" />
                                            {{ t.isJoined ? 'Entrar' : 'Inscrever-se' }}
                                        </template>
                                    </button>
                                </div>
                            </div>

                        </div>
                    </div>
                </transition-group>

            </div>

            <button 
                v-if="filteredTournaments.length > itemsPerPage"
                @click="navigateCarousel('next')" 
                class="handle handle-next z-50 md:opacity-0 md:group-hover:opacity-100"
            >
                <ChevronRight class="w-8 h-8 text-white transition-transform transform md:group-hover:scale-125" />
            </button>

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

/* --- BANNERS --- */
.promo-banner {
    height: 120px;
    border-radius: 4px;
    position: relative;
    overflow: hidden;
    background-color: #1f1f1f;
    box-shadow: 0 4px 10px rgba(0,0,0,0.5);
    transition: transform 0.3s;
}
.promo-banner:hover { transform: scale(1.02); z-index: 5; }
.fade-banner-enter-active, .fade-banner-leave-active { transition: opacity 0.5s ease; }
.fade-banner-enter-from, .fade-banner-leave-to { opacity: 0; }
.scrollbar-hide::-webkit-scrollbar { display: none; }
.scrollbar-hide { -ms-overflow-style: none; scrollbar-width: none; }

/* --- CARD ESTILO NETFLIX (COMPACTO & MODERNO) --- */
.movie-card {
    /* DESKTOP: 190px de largura x 300px altura (Cabe 6 + sobra) */
    min-width: 190px;
    max-width: 190px;
    height: 300px;
    
    position: relative;
    border-radius: 8px; 
    overflow: hidden;
    background-color: #121212;
    box-shadow: 0 4px 10px rgba(0,0,0,0.6);
    border: 1px solid #27272a;
    transition: transform 0.3s cubic-bezier(0.2, 0, 0, 1), box-shadow 0.3s ease, border-color 0.3s ease;
    cursor: pointer;
    z-index: 10;
}

.movie-card:hover {
    transform: scale(1.03); 
    box-shadow: 0 15px 40px rgba(0,0,0,0.8);
    border-color: #52525b;
    z-index: 20;
}

.card-cover {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    z-index: 0;
}

.card-body {
    position: relative;
    z-index: 1;
    height: 100%;
    display: flex;
    flex-direction: column;
    padding: 0.75rem; 
    background: linear-gradient(to bottom, 
        rgba(0,0,0,0.2) 0%, 
        rgba(0,0,0,0.0) 20%, 
        rgba(0,0,0,0.6) 50%, 
        rgba(5,5,5,0.95) 85%, 
        #050505 100%
    );
}

.loader-spin {
    width: 12px; height: 12px;
    border: 2px solid rgba(255,255,255,0.3);
    border-top-color: currentColor;
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
}

/* --- ESTILO DAS SETAS (HANDLES) --- */
.carousel-container { position: relative; }

.handle {
    position: absolute;
    top: 1rem; /* Alinha com o pt-4 do slider */
    height: 300px; /* Mesma altura do card Desktop */
    z-index: 50;
    width: 3.5rem; 
    /* Desktop: Fundo degradê */
    background: linear-gradient(90deg, rgba(0,0,0,0.8) 0%, rgba(0,0,0,0) 100%);
    display: flex; align-items: center; justify-content: center;
    cursor: pointer; border: none;
    transition: opacity 0.3s ease;
    /* Opacity controlada via Vue (md:opacity-0) */
    opacity: 1; 
    border-radius: 8px; /* Arredonda igual ao card */
}

.handle-next {
    right: 0;
    background: linear-gradient(-90deg, rgba(0,0,0,0.8) 0%, rgba(0,0,0,0) 100%);
}

.handle:hover {
    background: rgba(0,0,0,0.6);
}

.list-anim-move,
.list-anim-enter-active,
.list-anim-leave-active {
  transition: all 0.5s ease;
}
.list-anim-enter-from,
.list-anim-leave-to {
  opacity: 0;
  transform: translateX(30px);
}
.list-anim-leave-active {
  position: absolute;
}

@media (max-width: 768px) {
    /* MOBILE: Seta Transparente e colada nas bordas */
    .handle {
        background: transparent !important; /* Sem fundo */
        width: 30px; 
        height: 230px; /* Mesma altura do card Mobile */
        top: 1rem;
    }
    /* Força posição 0 */
    .handle-prev { left: 0 !important; }
    .handle-next { right: 0 !important; }

    /* MOBILE: Card 150px de largura x 230px altura */
    .movie-card {
        min-width: 150px;
        max-width: 150px;
        height: 230px;
    }
    .card-body { padding: 0.6rem; }
}

@keyframes spin { to { transform: rotate(360deg); } }
</style>