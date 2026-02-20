<script setup lang="ts">
import { computed, ref, nextTick, onMounted, onUnmounted, watch } from 'vue';
import { 
    Info, Users, LogIn, 
    Heart, Share2, Trophy, X, Calendar, Coins, Lock, Check 
} from 'lucide-vue-next';
import Swal from 'sweetalert2';
import TournamentService from '../../services/Tournament/TournamentService';
import { useAuthStore } from '../../stores/useAuthStore'; 

// --- PROPS ---
const props = defineProps<{
    tournament: any; 
    processingId: number | null;
}>();

// --- EMITS ---
const emit = defineEmits(['join', 'enter', 'share', 'favorite']);

const authStore = useAuthStore(); 

// ========================================================================
// REATIVIDADE
// ========================================================================

// 1. Estado Local para Favorito
const localIsFavorite = ref(!!props.tournament.isFavorite);
watch(() => props.tournament.isFavorite, (newVal) => {
    localIsFavorite.value = !!newVal;
});

// 2. Estado Local para Inscrito 
const localIsJoined = ref(!!props.tournament.isJoined);
watch(() => props.tournament.isJoined, (newVal) => {
    localIsJoined.value = !!newVal;
});

// 3. Estado Computado para LOTADO
const isFull = computed(() => {
    const t = props.tournament;
    return t.maxParticipants > 0 && t.participantsCount >= t.maxParticipants;
});

// 4. Controle visual do alerta "Já inscrito"
const showAlreadyJoinedMsg = ref(false);
let joinedMsgTimeout: any = null;

// ========================================================================

// --- ESTADOS INTERNOS ---
const showInfo = ref(false);
const popoverRef = ref<HTMLElement | null>(null);
const cardRef = ref<HTMLElement | null>(null);
const popoverStyle = ref({});
const arrowStyle = ref({});
const arrowClass = ref('');

// --- IMAGENS ---
const coversModules = import.meta.glob('/src/assets/tournament_covers/*.{png,jpg,jpeg,svg,webp}', { eager: true });
const coversMap: Record<string, string> = {};
for (const path in coversModules) {
    const fileName = path.split('/').pop() ?? 'unknown';
    coversMap[fileName] = (coversModules[path] as any).default;
}

const cardImage = computed(() => {
    const t = props.tournament;
    if (!t) return '';
    const rawName = t?.coverImage || t?.CoverImage;
    const imgName = typeof rawName === 'string' ? rawName.trim() : '';
    if (imgName && coversMap[imgName]) return coversMap[imgName];
    return Object.values(coversMap)[0] || '';
});

// --- FORMATADORES ---
const formatCurrency = (val: number) => val?.toFixed(2).replace('.', ',') || '0,00';
const formatDateSimple = (dateStr: string) => (!dateStr ? '--/--' : new Date(dateStr).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' }));
const formatTimeSimple = (dateStr: string) => (!dateStr ? '--:--' : new Date(dateStr).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }));

// --- ESTILOS DINÂMICOS (FONTES DE CINEMA + NEON DOURADO) ---
const titleStyle = computed(() => {
    const id = props.tournament?.id || 0;
    
    // Lista expandida com 15 fontes de cinema de alta legibilidade
    const fonts = [
        "'Bebas Neue', sans-serif",         // Ação Clássica (Condensada)
        "'Cinzel', serif",                  // Épico / Fantasia
        "'Oswald', sans-serif",             // Thriller / Moderno
        "'Anton', sans-serif",              // Impacto Puro
        "'Montserrat', sans-serif",         // Sci-Fi Moderno
        "'Fjalla One', sans-serif",         // Ação / Bloco Forte
        "'Righteous', cursive",             // Retro Futurista
        "'Orbitron', sans-serif",           // Sci-Fi / Espaço
        "'Teko', sans-serif",               // Tecnológico (Muito condensada)
        "'Russo One', sans-serif",          // Russo / Bloco Pesado
        "'Passion One', cursive",           // Impacto Arredondado
        "'Staatliches', cursive",           // Moderno Limpo Caps
        "'Archivo Black', sans-serif",      // Grotesco Pesado
        "'Kanit', sans-serif",              // Esportes / Moderno
        "'Barlow Condensed', sans-serif"    // Pôster Versátil
    ];
    
    const selectedFont: string = fonts[Math.abs(id) % fonts.length] || "'Bebas Neue', sans-serif";
    
    // Ajuste fino de tamanho e espaçamento baseado na fonte escolhida
    let fontSize = '14px';
    let letterSpacing = 'normal';

    // Grupo das fontes muito condensadas
    if (['Bebas', 'Anton', 'Teko', 'Fjalla', 'Barlow', 'Staatliches'].some(f => selectedFont.includes(f))) {
        fontSize = '17px'; 
        letterSpacing = '0.7px';
    } 
    // Grupo das fontes largas
    else if (['Cinzel', 'Orbitron'].some(f => selectedFont.includes(f))) {
        fontSize = '13px';
        letterSpacing = '0.5px';
    }
    // Grupo intermediário forte
    else if (['Oswald', 'Russo', 'Archivo', 'Kanit', 'Passion', 'Righteous'].some(f => selectedFont.includes(f))) {
        fontSize = '15px';
    }

    return {
        fontFamily: selectedFont,
        fontSize: fontSize,
        letterSpacing: letterSpacing,
        // 👇 EFEITO NEON DOURADO ADICIONADO AQUI 👇
        // Camadas: Brilho interno amarelo, brilho médio âmbar, aura externa laranja/dourada
        textShadow: '0 0 5px #ffeb3b, 0 0 15px #ffc107, 0 0 30px #ff9800'
    };
});

// --- LÓGICA DO POPOVER (INFO) ---
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

// --- AÇÕES DO USUÁRIO ---

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
    const url = `${window.location.origin}/tournament/${t.id}`;
    const shareData = { title: t.name, text: 'Venha jogar!', url };

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

// Ação Principal (Botão ou Clique no Card)
const handleAction = () => {
    const t = props.tournament;
    
    if (localIsJoined.value) {
        showAlreadyJoinedMsg.value = true;
        if (joinedMsgTimeout) clearTimeout(joinedMsgTimeout);
        joinedMsgTimeout = setTimeout(() => {
            showAlreadyJoinedMsg.value = false;
        }, 2500); 
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

    Swal.fire({
        title: '', padding: 0, background: 'transparent', showConfirmButton: false, showCancelButton: false, allowOutsideClick: true,
        html: `
            <div class="relative w-full overflow-hidden rounded-xl border border-white/20 shadow-[0_0_50px_rgba(0,0,0,0.8)] bg-[#0f172a]">
                <div class="absolute inset-0 z-0 bg-cover bg-center opacity-40 mix-blend-overlay" style="background-image: url('/images/util/caixa.png'); filter: contrast(1.2);"></div>
                <div class="absolute inset-0 z-0 bg-gradient-to-b from-[#0f172a]/90 via-[#0f172a]/95 to-[#020617]"></div>
                <div class="relative z-10 p-6 flex flex-col items-center w-full text-center">
                    <span class="text-[10px] font-bold text-blue-400 uppercase tracking-[0.2em] mb-2 drop-shadow-md">CONFIRMAR COMPRA</span>
                    <h2 class="text-lg font-black text-white leading-tight mb-6 px-4 drop-shadow-lg">${t.name}</h2>
                    <div class="w-full bg-black/30 border border-white/10 rounded-lg p-4 mb-5 backdrop-blur-sm">
                        <div class="flex justify-between items-center mb-2">
                            <span class="text-xs text-slate-400 font-medium">Entrada</span>
                            <span class="text-xl font-black text-white tracking-wide">R$ ${formatCurrency(t.entryFee).replace('R$', '').trim()}</span>
                        </div>
                        <div class="w-full h-px bg-white/10 mb-2"></div>
                        <div class="flex justify-between items-center">
                            <span class="text-xs text-slate-400 font-medium">Prêmio</span>
                            <span class="text-sm font-bold text-emerald-400 tracking-wide">R$ ${formatCurrency(t.prizePool).replace('R$', '').trim()}</span>
                        </div>
                    </div>
                    <div class="flex flex-col gap-3 w-full">
                        <button id="btn-pay" class="w-full bg-emerald-600 hover:bg-emerald-500 text-white font-black uppercase text-xs tracking-widest py-3.5 rounded-lg shadow-lg shadow-emerald-500/20 transition-transform active:scale-[0.98] border-t border-white/20">PAGAR E ENTRAR</button>
                        <button id="btn-cancel" class="text-xs font-bold text-slate-500 hover:text-white transition-colors uppercase tracking-wider py-2">CANCELAR</button>
                    </div>
                </div>
            </div>
        `,
        width: '320px', customClass: { popup: '!overflow-visible !bg-transparent !p-0 !border-0' },
        didOpen: (popup) => {
            popup.querySelector('#btn-pay')?.addEventListener('click', () => Swal.clickConfirm());
            popup.querySelector('#btn-cancel')?.addEventListener('click', () => Swal.close());
        }
    }).then((result) => {
        if (result.isConfirmed) {
            emit('join', t.id);
        }
    });
};

const handlePopoverAction = () => {
    closeInfo();
    handleAction();
};

onMounted(() => document.addEventListener('click', handleClickOutside));
onUnmounted(() => document.removeEventListener('click', handleClickOutside));
</script>

<template>
    <div ref="cardRef" 
         class="movie-card shrink-0 group relative cursor-pointer 
                w-full aspect-[200/330] h-auto
                md:w-[200px] md:min-w-[200px] md:max-w-[200px] md:h-[330px] md:aspect-auto
                rounded-xl overflow-hidden bg-[#121212] border border-[#27272a] shadow-[0_4px_10px_rgba(0,0,0,0.6)] transition-all duration-300 hover:scale-103 hover:shadow-[0_20px_50px_rgba(0,0,0,0.9)] hover:border-[#52525b] hover:z-20"
         @click="handleAction">
        
        <transition name="pop-alert">
            <div v-if="showAlreadyJoinedMsg" 
                 class="absolute inset-0 z-50 flex flex-col items-center justify-center bg-black/80 backdrop-blur-sm p-4 text-center pointer-events-none border border-blue-500/30">
                <Check class="w-10 h-10 text-blue-400 mb-2 drop-shadow-[0_0_8px_rgba(96,165,250,0.8)]" />
                <h4 class="text-white font-black text-[12px] uppercase tracking-widest mb-1 leading-tight drop-shadow-md">Você já participa</h4>
                <p class="text-blue-300 font-bold text-[10px] uppercase tracking-widest">deste torneio!</p>
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
            <span class="text-[9px] font-mono font-bold text-white/50 tracking-tighter drop-shadow-md select-none">#{{ tournament.id }} - {{ tournament.coverImage || tournament.CoverImage || 'sem_imagem' }}</span>
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
                    (!localIsJoined && isFull) ? 'grayscale opacity-50' : 'opacity-80 group-hover:opacity-60'
                 ]">
            
            <div class="absolute top-0 left-0 right-0 h-24 bg-gradient-to-b from-black/90 via-black/50 to-transparent z-10 pointer-events-none"></div>
            
            <div class="absolute inset-0 bg-gradient-to-t from-[#050505] via-[#050505]/40 to-transparent z-0"></div>
        </div>

        <div class="card-body relative z-20 flex flex-col h-full pt-8 px-2 pb-2">
            
            <div class="flex-1"></div>

            <div class="w-full flex flex-col justify-end">
                
                <div class="w-full flex justify-center items-end text-center mb-2 px-1">
                    <h3 :style="titleStyle" 
                        class="font-black text-white italic uppercase leading-tight line-clamp-2 min-h-[2.5em] flex items-end justify-center w-full">
                        {{ tournament.name }}
                    </h3>
                </div>
                
                <div class="w-full border-t border-white/10 pt-2 mb-2"></div>

                <div class="flex justify-between items-end w-full mb-2 px-0.5">
                    
                    <div class="flex items-center gap-1.5 opacity-90 drop-shadow-md pointer-events-none mb-0.5">
                        <span class="w-0.5 h-3 bg-red-500 rounded-full shadow-[0_0_5px_red]"></span>
                        <span class="text-[9px] font-bold uppercase tracking-widest text-white">{{ tournament.sport || 'Esporte' }}</span>
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
                                class="h-8 w-8 shrink-0 flex items-center justify-center rounded transition-all shadow-lg active:scale-95 border border-transparent shadow-white/20 cursor-pointer"
                                :class="(!localIsJoined && isFull) ? 'bg-gray-400 text-gray-900 hover:bg-gray-300' : 'bg-white text-black hover:bg-gray-200'"
                                title="Detalhes">
                            <Info class="w-5 h-5" />
                        </button>
                    </div>

                    <div class="flex flex-col items-end gap-1.5 flex-1 min-w-0">
                        
                        <div v-if="tournament.entryFee == 0" class="mb-0.5">
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
                                    'h-8 px-3 flex items-center justify-center gap-1.5 rounded font-black text-[10px] uppercase tracking-wider shadow-xl transition-all transform border min-w-[75px]',
                                    (!localIsJoined && isFull) ? 'bg-gray-400 text-gray-900 border-gray-500 cursor-not-allowed opacity-90 shadow-none' : 'bg-white text-black hover:bg-gray-200 shadow-white/10 active:scale-95 border-transparent cursor-pointer'
                                ]">
                            
                            <span v-if="processingId === tournament.id" class="loader-spin border-current w-3 h-3"></span>
                            
                            <template v-else>
                                <component :is="(!localIsJoined && isFull) ? Lock : LogIn" class="w-3.5 h-3.5 fill-current shrink-0" />
                                <span class="truncate">{{ (!localIsJoined && isFull) ? 'LOTADO' : 'Comprar' }}</span>
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
                                <span class="text-[10px] font-bold text-emerald-400 uppercase flex items-center gap-1 drop-shadow-md"><Trophy class="w-3.5 h-3.5" /> {{ tournament.sport }}</span>
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
                            <div class="flex flex-col items-end mr-1"><span class="text-[9px] uppercase text-slate-400 font-bold mb-0.5">Entrada</span><span class="text-sm font-black text-white drop-shadow-md">{{ tournament.entryFee == 0 ? 'GRÁTIS' : `R$ ${formatCurrency(tournament.entryFee)}` }}</span></div>
                            
                            <button type="button" @click="handlePopoverAction" 
                                    :disabled="processingId === tournament.id || (!localIsJoined && isFull)"
                                    :class="[
                                        'px-6 py-2.5 rounded font-black uppercase text-xs tracking-wider transition-all shadow-lg border',
                                        (!localIsJoined && isFull)
                                            ? 'bg-gray-400 text-gray-900 border-gray-500 cursor-not-allowed opacity-90 shadow-none'
                                            : 'bg-white text-black hover:bg-gray-200 hover:shadow-emerald-500/20 active:scale-95 border-transparent cursor-pointer'
                                    ]">
                                {{ (!localIsJoined && isFull) ? 'LOTADO' : 'COMPRAR' }}
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </Teleport>
    </div>
</template>

<style scoped>
/* 👇 IMPORTAÇÃO ATUALIZADA: 15 FONTES DE CINEMA (Playfair removida) 👇 */
@import url('https://fonts.googleapis.com/css2?family=Anton&family=Archivo+Black&family=Barlow+Condensed:wght@700;900&family=Bebas+Neue&family=Cinzel:wght@700;900&family=Fjalla+One&family=Kanit:wght@700;900&family=Montserrat:wght@900&family=Orbitron:wght@700;900&family=Oswald:wght@700&family=Passion+One:wght@700;900&family=Righteous&family=Russo+One&family=Staatliches&family=Teko:wght@700&display=swap');

.movie-card:hover { transform: scale(1.03); }
.card-body { background: linear-gradient(to bottom, rgba(0,0,0,0.1) 0%, rgba(0,0,0,0.0) 30%, rgba(0,0,0,0.7) 60%, rgba(5,5,5,0.95) 90%, #050505 100%); }
.loader-spin { width: 12px; height: 12px; border: 2px solid rgba(255,255,255,0.3); border-top-color: currentColor; border-radius: 50%; animation: spin 0.8s linear infinite; }
.free-badge-effect { display: inline-block; font-weight: 900; text-transform: uppercase; letter-spacing: 0.1em; animation: neon-breath 2s ease-in-out infinite; }
.animate-pop-in { animation: fadeInScale 0.25s cubic-bezier(0.16, 1, 0.3, 1) forwards; }

/* Nova animação do alerta sobreposto */
.pop-alert-enter-active { animation: popAlert 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275); }
.pop-alert-leave-active { animation: popAlert 0.2s ease-in reverse; }

@keyframes popAlert {
    0% { opacity: 0; transform: scale(0.8); }
    100% { opacity: 1; transform: scale(1); }
}

@keyframes fadeInScale { from { opacity: 0; scale: 0.95; } to { opacity: 1; scale: 1; } }
@keyframes neon-breath { 0%, 100% { transform: scale(1); text-shadow: 0 0 5px rgba(34, 197, 94, 0.7), 0 0 15px rgba(34, 197, 94, 0.4); color: #4ade80; } 50% { transform: scale(1.1); text-shadow: 0 0 10px rgba(34, 197, 94, 1), 0 0 20px rgba(34, 197, 94, 0.8), 0 0 30px rgba(34, 197, 94, 0.6); color: #86efac; } }
@keyframes spin { to { transform: rotate(360deg); } }
</style>