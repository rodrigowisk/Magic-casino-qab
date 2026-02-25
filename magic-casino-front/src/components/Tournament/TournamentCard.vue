<script setup lang="ts">
import { computed, ref, nextTick, onMounted, onUnmounted, watch } from 'vue';
import { 
    Info, Users, LogIn, 
    Heart, Share2, Trophy, X, Calendar, Coins, Lock, Check, Play 
} from 'lucide-vue-next';
import Swal from 'sweetalert2';
import TournamentService from '../../services/Tournament/TournamentService';
import { useAuthStore } from '../../stores/useAuthStore'; 
import { useTournamentModal } from '../../composables/useTournamentModal';

const props = defineProps<{
    tournament: any; 
    processingId: number | null;
}>();

const emit = defineEmits(['join', 'enter', 'share', 'favorite']);

const authStore = useAuthStore(); 
const { confirmPurchase } = useTournamentModal();

const localIsFavorite = ref(!!props.tournament.isFavorite);
watch(() => props.tournament.isFavorite, (newVal) => {
    localIsFavorite.value = !!newVal;
});

const localIsJoined = ref(!!props.tournament.isJoined);
watch(() => props.tournament.isJoined, (newVal) => {
    localIsJoined.value = !!newVal;
});

const isFull = computed(() => {
    const t = props.tournament;
    return t.maxParticipants > 0 && t.participantsCount >= t.maxParticipants;
});

// 🔥 TIMER REATIVO PARA ATUALIZAR O BOTÃO AUTOMATICAMENTE 🔥
const currentTime = ref(new Date().getTime());
let timeCheckerInterval: any = null;

// 🔥 CHECAGEM SE O TORNEIO É NO FUTURO (AGORA É REATIVA) 🔥
const isFutureTournament = computed(() => {
    if (!props.tournament?.startDate) return false;
    const startTime = new Date(props.tournament.startDate).getTime();
    return startTime > currentTime.value;
});

const showSuccessMsg = ref(false);
let successMsgTimeout: any = null;

const showInfo = ref(false);
const popoverRef = ref<HTMLElement | null>(null);
const cardRef = ref<HTMLElement | null>(null);
const popoverStyle = ref({});
const arrowStyle = ref({});
const arrowClass = ref('');

// 🔥 SISTEMA DE IMAGENS CORRIGIDO PARA TS ESTRITO (Evita Erro TS2532) 🔥
const rawCoversModules = import.meta.glob<{ default: string }>('/src/assets/tournament_covers/**/*.{png,jpg,jpeg,svg,webp}', { eager: true });

const coversArray = Object.entries(rawCoversModules).map(([key, mod]) => ({
    path: key,
    url: mod ? mod.default : ''
}));

const getDefaultImage = (): string => {
    return coversArray[0]?.url || '';
};

const cardImage = computed((): string => {
    const t = props.tournament;
    if (!t) return getDefaultImage();
    
    const rawName = t?.coverImage || t?.CoverImage;
    if (!rawName) return getDefaultImage();

    const query = String(rawName).trim().toLowerCase().replace(/\\/g, '/');

    const exactMatch = coversArray.find(item => item?.path?.toLowerCase().endsWith(query));
    if (exactMatch) return exactMatch.url;

    const justName = query.split('/').pop()?.toLowerCase();
    if (justName) {
        const fallbackMatch = coversArray.find(item => item?.path?.toLowerCase().endsWith('/' + justName));
        if (fallbackMatch) return fallbackMatch.url;
    }

    return getDefaultImage();
});

// 🔥 LEITURA ESTRITA DO JSON (SEM GAMBIARRA) 🔥
const displaySport = computed(() => {
    const t = props.tournament;
    if (!t) return 'ESPORTE';

    // 1. Extrai a string do JSON exatamente como vem do C#
    const rawRules = t.filterRules || t.FilterRules || t.filter_rules;

    if (rawRules && rawRules !== '[]' && rawRules !== '') {
        try {
            const parsed1 = typeof rawRules === 'string' ? JSON.parse(rawRules) : rawRules;
            const rules = typeof parsed1 === 'string' ? JSON.parse(parsed1) : parsed1;

            if (rules?.sports && Array.isArray(rules.sports)) {
                if (rules.sports.length > 1) return 'MISTO';
                if (rules.sports.length === 1) {
                    const key = String(rules.sports[0]?.key || '').toLowerCase();
                    if (key === 'soccer') return 'FUTEBOL';
                    if (key === 'basketball') return 'BASQUETE';
                    if (key === 'tennis') return 'TÊNIS';
                    return key ? key.toUpperCase() : 'ESPORTE';
                }
            }
        } catch (e) {
            console.error("Erro no Parse do JSON:", e);
        }
    }

    // 2. Fallback caso o JSON venha nulo do banco
    const fallback = String(t.sport || t.Sport || 'ESPORTE').toUpperCase();
    if (fallback === 'SOCCER') return 'FUTEBOL';
    if (fallback === 'BASKETBALL') return 'BASQUETE';
    if (fallback === 'TENNIS') return 'TÊNIS';
    
    return fallback;
});


const formatCurrency = (val: number) => val?.toFixed(2).replace('.', ',') || '0,00';
const formatDateSimple = (dateStr: string) => (!dateStr ? '--/--' : new Date(dateStr).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' }));
const formatTimeSimple = (dateStr: string) => (!dateStr ? '--:--' : new Date(dateStr).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }));

const titleStyle = computed(() => {
    const id = props.tournament?.id || 0;
    
    const fonts = [
        "'Bebas Neue', sans-serif", "'Cinzel', serif", "'Oswald', sans-serif", "'Anton', sans-serif", "'Montserrat', sans-serif",
        "'Fjalla One', sans-serif", "'Righteous', cursive", "'Orbitron', sans-serif", "'Teko', sans-serif", "'Russo One', sans-serif",
        "'Passion One', cursive", "'Staatliches', cursive", "'Archivo Black', sans-serif", "'Kanit', sans-serif", "'Barlow Condensed', sans-serif"
    ];
    
    const selectedFont: string = fonts[Math.abs(id) % fonts.length] || "'Bebas Neue', sans-serif";
    
    let fontSize = '14px';
    let letterSpacing = 'normal';

    if (['Bebas', 'Anton', 'Teko', 'Fjalla', 'Barlow', 'Staatliches'].some(f => selectedFont.includes(f))) {
        fontSize = '17px'; 
        letterSpacing = '0.7px';
    } 
    else if (['Cinzel', 'Orbitron'].some(f => selectedFont.includes(f))) {
        fontSize = '13px';
        letterSpacing = '0.5px';
    }
    else if (['Oswald', 'Russo', 'Archivo', 'Kanit', 'Passion', 'Righteous'].some(f => selectedFont.includes(f))) {
        fontSize = '15px';
    }

    return {
        fontFamily: selectedFont,
        fontSize: fontSize,
        letterSpacing: letterSpacing,
        // 🔥 COR DOURADA PREMIUM DO TÍTULO 🔥
        color: '#FFD700', 
        textShadow: '1px 2px 3px rgba(0,0,0,0.9), 0 0 5px rgba(255, 193, 7, 0.8), 0 0 10px rgba(255, 152, 0, 0.5)'
    };
});

const toggleInfo = async (event: MouseEvent) => {
    event.stopPropagation();
    
    if (showInfo.value) {
        showInfo.value = false;
        return;
    }

    showInfo.value = true;
    await nextTick();

    if (!cardRef.value) return;

    const rect = cardRef.value.getBoundingClientRect();
    const windowWidth = window.innerWidth;
    const isMobile = windowWidth < 768;

    if (isMobile) {
        popoverStyle.value = {
            position: 'fixed', top: '50%', left: '50%', transform: 'translate(-50%, -50%)',
            width: '90vw', maxWidth: '400px', maxHeight: '90vh', overflowY: 'auto', zIndex: 99999
        };
        arrowStyle.value = { display: 'none' };
    } else {
        const DESKTOP_MODAL_WIDTH = 500; 
        const MARGIN = 20;
        const ARROW_SIZE = 10;
        const HEADER_HEIGHT = 90; 
        const GAP_BETWEEN_MODAL_AND_CARD = 15; 

        const cardCenter = rect.left + (rect.width / 2);
        let leftPos = cardCenter - (DESKTOP_MODAL_WIDTH / 2);

        if (leftPos < MARGIN) leftPos = MARGIN;
        const maxLeft = windowWidth - DESKTOP_MODAL_WIDTH - MARGIN;
        if (leftPos > maxLeft) leftPos = maxLeft;

        const arrowLeftPos = cardCenter - leftPos - ARROW_SIZE;

        popoverStyle.value = {
            position: 'fixed', top: `${HEADER_HEIGHT}px`, left: `${leftPos}px`,
            width: `${DESKTOP_MODAL_WIDTH}px`, zIndex: 99999
        };
        arrowStyle.value = { left: `${arrowLeftPos}px`, bottom: '-7px', top: 'auto' };
        arrowClass.value = 'border-b border-r border-white/20 bg-[#111]';
        
        if (popoverRef.value) {
            const modalHeight = popoverRef.value.offsetHeight;
            const absoluteCardTop = rect.top + window.scrollY;
            const desiredVisibleSpace = HEADER_HEIGHT + modalHeight + GAP_BETWEEN_MODAL_AND_CARD;
            const targetScrollY = absoluteCardTop - desiredVisibleSpace;
            if (targetScrollY < window.scrollY) window.scrollTo({ top: targetScrollY, behavior: 'smooth' });
        }
    }
};

const closeInfo = () => { showInfo.value = false; };

const handleClickOutside = (event: MouseEvent) => {
    if (showInfo.value && popoverRef.value && !popoverRef.value.contains(event.target as Node)) {
        const target = event.target as HTMLElement;
        const infoBtn = cardRef.value?.querySelector('button[title="Detalhes"]');
        if (infoBtn && infoBtn.contains(target)) return;
        closeInfo();
    }
};

const handleFavorite = async (event: Event) => {
    if (event) {
        event.preventDefault();
        event.stopPropagation();
    }

    if (!authStore.isAuthenticated) {
        Swal.fire({ toast: true, position: 'top-end', icon: 'warning', title: 'Faça login para favoritar!', timer: 2500, showConfirmButton: false, background: '#121212', color: '#fff' });
        return;
    }

    const previousState = localIsFavorite.value;
    const newState = !previousState;

    localIsFavorite.value = newState;
    props.tournament.isFavorite = newState;
    emit('favorite', { id: props.tournament.id, isFavorite: newState });

    try {
        await TournamentService.toggleFavorite(props.tournament.id);

        const Toast = Swal.mixin({
            toast: true, position: 'top-end', showConfirmButton: false, timer: 1500, background: '#121212', color: '#fff'
        });
        Toast.fire({ icon: 'success', title: newState ? 'Favoritado!' : 'Removido!' });

    } catch (error: any) {
        console.error("Erro ao favoritar:", error);
        
        if (error?.response?.status === 401 || error?.response?.status === 403) {
            localIsFavorite.value = previousState;
            props.tournament.isFavorite = previousState;
            emit('favorite', { id: props.tournament.id, isFavorite: previousState });
            Swal.fire({ toast: true, position: 'top-end', icon: 'error', title: 'Sessão expirada!', timer: 2500, showConfirmButton: false, background: '#121212', color: '#fff' });
        }
    }
};

const handleShare = async (event: Event) => {
    if (event) {
        event.preventDefault();
        event.stopPropagation();
    }

    const t = props.tournament;
    // 🔥 URL ATUALIZADA PARA REDIRECIONAR AO LOBBY COM O PARÂMETRO DO MODAL 🔥
    const url = `${window.location.origin}/tournaments?showInfo=${t.id}`;
    const shareData = { title: t.name, text: 'Da uma olhada nesse Torneio que estou jogando! Esta Imperdível, CONFIRA!!!', url };

    if (navigator.share) {
        try { await navigator.share(shareData); emit('share', t.id); } catch {}
    } else {
        try {
            await navigator.clipboard.writeText(url);
            Swal.fire({ toast: true, position: 'top-end', icon: 'info', title: 'Link copiado!', timer: 1500, showConfirmButton: false, background: '#121212', color: '#fff'});
            emit('share', t.id);
        } catch {}
    }
};

const handleAction = async () => {
    const t = props.tournament;
    
    if (localIsJoined.value) {
        if (isFutureTournament.value) {
            Swal.fire({
                toast: true, position: 'top-end', icon: 'info', 
                title: 'O torneio ainda não começou!', showConfirmButton: false, timer: 2500, 
                background: '#121212', color: '#fff'
            });
            return;
        }

        emit('enter', t.id);
        return;
    }

    if (isFull.value) {
        Swal.fire({
            toast: true, position: 'top-end', icon: 'warning', 
            title: 'Torneio Lotado!', showConfirmButton: false, timer: 2000, 
            background: '#121212', color: '#fff'
        });
        return;
    }

    const isConfirmed = await confirmPurchase(t);
    
    if (isConfirmed) {
        localIsJoined.value = true;
        
        showSuccessMsg.value = true;
        if (successMsgTimeout) clearTimeout(successMsgTimeout);
        successMsgTimeout = setTimeout(() => {
            showSuccessMsg.value = false;
        }, 3000); 

        emit('join', t.id);
    }
};

const handlePopoverAction = () => {
    closeInfo();
    handleAction();
};

onMounted(() => {
    document.addEventListener('click', handleClickOutside);
    
    timeCheckerInterval = setInterval(() => {
        currentTime.value = new Date().getTime();
    }, 5000); 
});

onUnmounted(() => {
    document.removeEventListener('click', handleClickOutside);
    
    if (timeCheckerInterval) {
        clearInterval(timeCheckerInterval);
    }
});
</script>

<template>
    <div ref="cardRef" 
         class="movie-card shrink-0 group relative cursor-pointer 
                w-full aspect-[200/330] h-auto
                md:w-[200px] md:min-w-[200px] md:max-w-[200px] md:h-[330px] md:aspect-auto
                rounded-xl overflow-hidden bg-[#121212] border border-[#27272a] shadow-[0_4px_10px_rgba(0,0,0,0.6)] transition-all duration-300 hover:scale-103 hover:shadow-[0_20px_50px_rgba(0,0,0,0.9)] hover:border-[#52525b] hover:z-20"
         @click="handleAction">
        
        <transition name="pop-alert">
            <div v-if="showSuccessMsg" 
                 class="absolute inset-0 z-50 flex flex-col items-center justify-center bg-gradient-to-b from-emerald-500 to-emerald-700 backdrop-blur-md p-5 text-center pointer-events-none">
                
                <div class="bg-white rounded-full p-2.5 mb-3 shadow-[0_0_15px_rgba(255,255,255,0.4)]">
                    <Check class="w-8 h-8 text-emerald-600" stroke-width="3" />
                </div>
                
                <h4 class="text-white font-black text-[15px] uppercase tracking-wide leading-tight drop-shadow-md">
                    Compra Aprovada
                </h4>
                <p class="text-emerald-100 font-medium text-[11px] mt-1 drop-shadow-sm">
                    Inscrição confirmada!
                </p>
                
                <div class="w-full border-t border-dashed border-emerald-300/60 mt-4 pt-3 flex flex-col gap-1">
                    <div class="flex justify-between items-center text-[10px] text-emerald-100 font-semibold w-full px-2">
                        <span>STATUS</span>
                        <span class="bg-emerald-800/50 px-1.5 py-0.5 rounded text-white font-mono tracking-wider">PAGO</span>
                    </div>
                </div>
            </div>
        </transition>

        <div class="absolute top-1 left-1.5 z-[40] flex items-center gap-2 pointer-events-auto">
            <button type="button" @click.stop.prevent="handleFavorite" 
                    class="transition-all duration-200 drop-shadow-md hover:scale-110 bg-transparent border-none cursor-pointer" 
                    :class="localIsFavorite ? 'text-red-500' : 'text-white/60 hover:text-white'" 
                    title="Favoritar">
                <Heart class="w-5 h-5 md:w-3.5 md:h-3.5 transition-colors duration-300" :class="localIsFavorite ? 'fill-red-500' : 'fill-transparent'" /> 
            </button>
            <button type="button" @click.stop.prevent="handleShare" class="text-white/40 hover:text-blue-400 hover:scale-110 transition-all duration-200 drop-shadow-md bg-transparent border-none cursor-pointer" title="Compartilhar">
                <Share2 class="w-5 h-5 md:w-3.5 md:h-3.5" />
            </button>
            <span class="text-[9px] font-mono font-bold text-white/50 tracking-tighter drop-shadow-md select-none">
                #{{ tournament.id }}
            </span>
        </div>

        <div class="absolute top-1 right-1.5 z-[40] flex items-center gap-1 bg-black/60 border border-white/10 px-2 py-0.5 rounded-full backdrop-blur-sm pointer-events-none">
            <Users class="w-2.5 h-2.5 text-gray-300" />
            <span class="text-[9px] font-bold text-white">
                {{ tournament.participantsCount }}
                <span class="text-white/60" v-if="tournament.maxParticipants > 0">/{{ tournament.maxParticipants }}</span>
            </span>
        </div>

    <div class="card-cover absolute inset-0 z-0">
            <img :src="cardImage"
                 :class="[
                    'w-full h-full object-cover transition-all duration-700 ease-out group-hover:scale-110',
                    (!localIsJoined && isFull) ? 'grayscale opacity-50' : 'opacity-100'
                 ]">
            
            <div class="absolute top-0 left-0 right-0 h-16 bg-gradient-to-b from-black/80 to-transparent z-10 pointer-events-none"></div>
            
            <div class="absolute bottom-0 left-0 right-0 h-[60%] bg-gradient-to-t from-[#050505] via-[#050505]/80 to-transparent z-10 pointer-events-none"></div>
        </div>

        <div class="card-body relative z-20 flex flex-col h-full pt-8 px-2 pb-2">
            
            <div class="flex-1"></div>

            <div class="w-full flex flex-col justify-end">
                
<div class="relative w-full flex items-center justify-center min-h-[2em] mt-auto mb-0">
    
    <div class="absolute left-0 right-0 top-1/2 -translate-y-1/2 h-[1px] bg-gradient-to-r from-transparent via-amber-500/90 to-transparent z-0"></div>

    <h3 :style="titleStyle" 
        class="relative z-10 w-full font-black italic uppercase text-center leading-tight line-clamp-2 px-2">
        {{ tournament.name }}
    </h3>

</div>

                <div class="flex justify-between items-end w-full mb-2 px-0.5">
                    
                    <div class="flex items-center gap-1.5 opacity-90 drop-shadow-md pointer-events-none mb-0.5">
                        <span class="w-0.5 h-3 bg-red-500 rounded-full shadow-[0_0_5px_red]"></span>
                        <span class="text-[9px] font-bold uppercase tracking-widest text-white">{{ displaySport }}</span>
                    </div>

                    <div class="flex flex-col items-end leading-none drop-shadow-md">
                        <span class="text-[8px] text-white/70 uppercase font-black tracking-widest mb-0.5">Prêmio</span>
                        <span class="text-[16px] font-black text-emerald-400 drop-shadow-[0_2px_8px_rgba(0,0,0,0.9)] tracking-tighter leading-none flex items-baseline gap-0.5">
                            <span class="text-[9px] font-bold text-emerald-500">R$</span>
                            {{ formatCurrency(tournament.prizePool || 0).replace('R$', '').trim() }}
                        </span>
                    </div>
                </div>

                <div class="flex items-end justify-between w-full gap-2">
                    
                    <div class="flex flex-col items-start gap-1.5">
                        
                        <div class="flex items-center gap-2 drop-shadow-md pb-0.5">
                            <div class="flex flex-col items-start">
                                <span class="text-[7px] font-black uppercase text-green-400 mb-0.5 tracking-tighter">Início</span>
                                <span class="text-[9px] font-bold text-white leading-none">{{ formatDateSimple(tournament.startDate) }}</span>
                                <span class="text-[8px] font-medium text-white/60 leading-none mt-0.5">{{ formatTimeSimple(tournament.startDate) }}</span>
                            </div>
                            <div class="w-px h-5 bg-white/20"></div>
                            <div class="flex flex-col items-start">
                                <span class="text-[7px] font-black uppercase text-red-400 mb-0.5 tracking-tighter">Encerra</span>
                                <span class="text-[9px] font-bold text-white leading-none">{{ formatDateSimple(tournament.endDate) }}</span>
                                <span class="text-[8px] font-medium text-white/60 leading-none mt-0.5">{{ formatTimeSimple(tournament.endDate) }}</span>
                            </div>
                        </div>

                        <button type="button" @click.stop="toggleInfo" 
                                class="h-8 w-8 shrink-0 flex items-center justify-center rounded transition-all active:scale-95 border border-transparent cursor-pointer"
                                :class="(!localIsJoined && isFull) ? 'bg-gray-400 text-gray-900 hover:bg-gray-300 shadow-none' : 'bg-[#FFD700] text-black hover:bg-[#e6c200] shadow-[0_0_10px_rgba(255,215,0,0.4)]'"
                                title="Detalhes">
                            <Info class="w-5 h-5" />
                        </button>
                    </div>

                    <div class="flex flex-col items-end gap-1.5 flex-1 min-w-0">
                        
                        <div v-if="localIsJoined" class="flex flex-col items-end leading-none drop-shadow-md mb-0.5">
                            <span class="text-[11px] font-black text-emerald-400 flex items-center gap-1">
                                <Check class="w-3 h-3" /> INSCRITO
                            </span>
                        </div>
                        <div v-else-if="tournament.entryFee == 0" class="mb-0.5">
                            <span class="free-badge-effect text-[11px]">GRÁTIS</span>
                        </div>
                        <div v-else class="flex flex-col items-end leading-none drop-shadow-md">
                            <span class="text-[10px] text-white/60 uppercase font-black mb-0.5">ENTRADA</span>
                            <span class="text-[11px] font-black text-white">R$ {{ formatCurrency(tournament.entryFee).replace('R$', '') }}</span>
                        </div>
                        
                        <button type="button" 
                                @click.stop="handleAction" 
                                :disabled="processingId === tournament.id || (!localIsJoined && isFull)"
                                :class="[
                                    'h-8 px-3 flex items-center justify-center gap-1.5 rounded font-black text-[10px] uppercase tracking-wider transition-all transform border min-w-[75px]',
                                    (!localIsJoined && isFull) 
                                        ? 'bg-gray-400 text-gray-900 border-gray-500 cursor-not-allowed opacity-90 shadow-none' 
                                        : (localIsJoined 
                                            ? (isFutureTournament
                                                ? 'bg-[#5c4d00] text-[#FFD700] border-transparent shadow-none cursor-not-allowed opacity-90'
                                                : 'bg-[#FFD700] text-black hover:bg-[#e6c200] shadow-[0_0_15px_rgba(255,215,0,0.4)] active:scale-95 border-transparent cursor-pointer')
                                            : 'bg-emerald-500 text-white hover:bg-emerald-400 shadow-emerald-500/20 active:scale-95 border-transparent cursor-pointer')
                                ]">
                            
                            <span v-if="processingId === tournament.id" class="loader-spin border-current w-3 h-3"></span>
                            
                            <template v-else>
                                <component :is="(!localIsJoined && isFull) ? Lock : (localIsJoined ? (isFutureTournament ? Calendar : Play) : LogIn)" 
                                           class="w-3.5 h-3.5 fill-current shrink-0" 
                                           :class="{ 'animate-play': localIsJoined && !isFutureTournament }" />
                                           
                                <span class="truncate">
                                    {{ (!localIsJoined && isFull) 
                                        ? 'LOTADO' 
                                        : (localIsJoined 
                                            ? (isFutureTournament ? 'EM BREVE' : 'JOGAR') 
                                            : 'COMPRAR') 
                                    }}
                                </span>
                            </template>

                        </button>
                    </div>
                </div>
            </div>
        </div>

        <Teleport to="body">
            <div v-if="showInfo" ref="popoverRef" class="flex flex-col bg-[#111] border border-white/20 rounded-xl shadow-[0_0_40px_rgba(0,0,0,0.8)] overflow-hidden animate-pop-in transition-all duration-200" :style="popoverStyle" @click.stop>
                <div class="absolute w-4 h-4 z-20 bg-[#111] transition-all duration-200 transform rotate-45" :style="arrowStyle" :class="arrowClass"></div>
                <div class="absolute inset-0 z-0 overflow-hidden rounded-xl pointer-events-none">
                    <div class="absolute inset-0 bg-[#111]"></div>
                    <div class="absolute top-0 right-0 w-1/2 h-full">
                        <img :src="cardImage"
                             :class="[
                                'w-full h-full object-cover',
                                (!localIsJoined && isFull) ? 'grayscale opacity-50' : 'opacity-80'
                             ]" />
                        <div class="absolute inset-0 bg-gradient-to-l from-transparent via-[#111]/60 to-[#111]"></div>
                    </div>
                </div>
                <div class="relative z-10 p-4 md:p-5 flex flex-col gap-3">
                    <div class="flex justify-between items-start">
                        <div class="flex flex-col gap-1 w-full pr-8">
                            <div class="flex items-center gap-3">
                                <span class="px-1.5 py-0.5 rounded bg-white/20 backdrop-blur-sm text-[9px] font-mono text-white shadow-sm border border-white/10">ID: #{{ tournament.id }}</span>
                                <span class="text-[10px] font-bold text-emerald-400 uppercase flex items-center gap-1 drop-shadow-md"><Trophy class="w-3.5 h-3.5" /> {{ displaySport }}</span>
                            </div>
                            <h3 class="text-lg md:text-xl font-black text-white italic uppercase leading-tight drop-shadow-lg mt-1 truncate w-full">{{ tournament.name }}</h3>
                        </div>
                        <button type="button" @click="closeInfo" class="absolute top-4 right-4 text-white/60 hover:text-white transition-colors bg-black/40 rounded-full p-1.5 backdrop-blur-sm border border-white/10 hover:bg-black/60 cursor-pointer"><X class="w-4 h-4" /></button>
                    </div>
                    <div class="text-slate-300 text-xs leading-relaxed line-clamp-2 drop-shadow-md font-medium pr-2 w-2/3">{{ tournament.description || 'Participe deste torneio emocionante e mostre suas habilidades para conquistar grandes prêmios!' }}</div>
                    <div class="grid grid-cols-3 gap-2 items-center relative z-20 mt-1">
                        <div class="flex flex-col gap-0.5 border-r border-white/10 px-1"><span class="text-[9px] uppercase text-slate-400 font-bold flex items-center gap-1"><Calendar class="w-2.5 h-2.5 text-blue-400" /> Início</span><span class="text-[10px] text-white font-mono leading-none">{{ formatDateSimple(tournament.startDate) }} <span class="text-[9px] text-white/40 block md:inline">{{ formatTimeSimple(tournament.startDate) }}</span></span></div>
                        <div class="flex flex-col gap-0.5 border-r border-white/10 px-1"><span class="text-[9px] uppercase text-slate-400 font-bold flex items-center gap-1"><Calendar class="w-2.5 h-2.5 text-red-400" /> Fim</span><span class="text-[10px] text-white font-mono leading-none">{{ formatDateSimple(tournament.endDate) }} <span class="text-[9px] text-white/40 block md:inline">{{ formatTimeSimple(tournament.endDate) }}</span></span></div>
                        <div class="flex flex-col gap-0.5 px-1"><span class="text-[9px] uppercase text-slate-400 font-bold flex items-center gap-1"><Users class="w-2.5 h-2.5 text-yellow-400" /> Inscritos</span><span class="text-[10px] font-bold text-white leading-none">{{ tournament.participantsCount }} <span class="text-white/60" v-if="tournament.maxParticipants > 0">/ {{ tournament.maxParticipants }}</span></span></div>
                    </div>
                    <div class="flex items-center justify-between mt-1 pt-2 border-t border-white/10 relative z-20">
                        <div class="flex flex-col"><span class="text-[9px] uppercase text-slate-400 font-bold mb-0.5">Premiação</span><span class="text-lg font-black text-emerald-400 flex items-center gap-1 drop-shadow-md"><Coins class="w-4 h-4" /> R$ {{ formatCurrency(tournament.prizePool) }}</span></div>
                        <div class="flex items-center gap-3">
                            <div class="flex flex-col items-end mr-1">
                                <template v-if="localIsJoined">
                                    <span class="text-[11px] font-black text-emerald-400 flex items-center gap-1 mt-1">
                                        <Check class="w-3 h-3" /> INSCRITO
                                    </span>
                                </template>
                                <template v-else>
                                    <span class="text-[9px] uppercase text-slate-400 font-bold mb-0.5">Entrada</span>
                                    <span class="text-sm font-black text-white drop-shadow-md">{{ tournament.entryFee == 0 ? 'GRÁTIS' : `R$ ${formatCurrency(tournament.entryFee)}` }}</span>
                                </template>
                            </div>
                            
                            <button type="button" @click="handlePopoverAction" 
                                    :disabled="processingId === tournament.id || (!localIsJoined && isFull)"
                                    :class="[
                                        'px-6 py-2.5 flex justify-center items-center gap-1.5 rounded font-black uppercase text-xs tracking-wider transition-all shadow-lg border',
                                        (!localIsJoined && isFull)
                                            ? 'bg-gray-400 text-gray-900 border-gray-500 cursor-not-allowed opacity-90 shadow-none'
                                            : (localIsJoined 
                                                ? (isFutureTournament
                                                    ? 'bg-[#5c4d00] text-[#FFD700] border-transparent shadow-none cursor-not-allowed opacity-90'
                                                    : 'bg-[#FFD700] text-black hover:bg-[#e6c200] shadow-[0_0_15px_rgba(255,215,0,0.4)] active:scale-95 border-transparent cursor-pointer')
                                                : 'bg-emerald-500 text-white hover:bg-emerald-400 hover:shadow-emerald-500/20 active:scale-95 border-transparent cursor-pointer')
                                    ]">
                                
                                <component :is="localIsJoined ? (isFutureTournament ? Calendar : Play) : null" 
                                           v-if="localIsJoined"
                                           class="w-4 h-4 fill-current" 
                                           :class="{ 'animate-play': !isFutureTournament }" />
                                           
                                {{ (!localIsJoined && isFull) 
                                    ? 'LOTADO' 
                                    : (localIsJoined 
                                        ? (isFutureTournament ? 'EM BREVE' : 'JOGAR') 
                                        : 'COMPRAR') 
                                }}
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </Teleport>
    </div>
</template>

<style scoped>
@import url('https://fonts.googleapis.com/css2?family=Anton&family=Archivo+Black&family=Barlow+Condensed:wght@700;900&family=Bebas+Neue&family=Cinzel:wght@700;900&family=Fjalla+One&family=Kanit:wght@700;900&family=Montserrat:wght@900&family=Orbitron:wght@700;900&family=Oswald:wght@700&family=Passion+One:wght@700;900&family=Righteous&family=Russo+One&family=Staatliches&family=Teko:wght@700&display=swap');

.movie-card:hover { transform: scale(1.03); }
.card-body { background: linear-gradient(to bottom, rgba(0,0,0,0.1) 0%, rgba(0,0,0,0.0) 30%, rgba(0,0,0,0.7) 60%, rgba(5,5,5,0.95) 90%, #050505 100%); }
.loader-spin { width: 12px; height: 12px; border: 2px solid rgba(255,255,255,0.3); border-top-color: currentColor; border-radius: 50%; animation: spin 0.8s linear infinite; }
.free-badge-effect { display: inline-block; font-weight: 900; text-transform: uppercase; letter-spacing: 0.1em; animation: neon-breath 2s ease-in-out infinite; }
.animate-pop-in { animation: fadeInScale 0.25s cubic-bezier(0.16, 1, 0.3, 1) forwards; }

.pop-alert-enter-active { animation: popAlert 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275); }
.pop-alert-leave-active { animation: popAlert 0.2s ease-in reverse; }

@keyframes popAlert {
    0% { opacity: 0; transform: scale(0.8); }
    100% { opacity: 1; transform: scale(1); }
}

@keyframes fadeInScale { from { opacity: 0; scale: 0.95; } to { opacity: 1; scale: 1; } }
@keyframes neon-breath { 0%, 100% { transform: scale(1); text-shadow: 0 0 5px rgba(34, 197, 94, 0.7), 0 0 15px rgba(34, 197, 94, 0.4); color: #4ade80; } 50% { transform: scale(1.1); text-shadow: 0 0 10px rgba(34, 197, 94, 1), 0 0 20px rgba(34, 197, 94, 0.8), 0 0 30px rgba(34, 197, 94, 0.6); color: #86efac; } }
@keyframes spin { to { transform: rotate(360deg); } }

/* 🔥 Animação do botão Play 🔥 */
@keyframes playPulse {
    0%, 100% { transform: scale(1); }
    50% { transform: scale(1.15); }
}
.animate-play { animation: playPulse 1.5s infinite ease-in-out; }
</style>