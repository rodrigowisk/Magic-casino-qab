<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { 
    Info, Users, ChevronRight, ChevronLeft, Play, LogIn 
} from 'lucide-vue-next';
import Swal from 'sweetalert2';

// PROPS: O que o componente recebe do Pai
const props = defineProps<{
    title: string;
    tournaments: any[];
    processingId: number | null;
}>();

// EMITS: Ações que o componente manda de volta para o Pai
const emit = defineEmits(['join', 'enter']);

// --- LÓGICA DE IMAGENS (Encapsulada no componente) ---
const coversModules = import.meta.glob('/src/assets/tournament_covers/*.{png,jpg,jpeg,svg,webp}', { eager: true });
const coversMap: Record<string, string> = {};
for (const path in coversModules) {
    const mod = coversModules[path] as any;
    const fileName = path.split('/').pop() || 'unknown';
    coversMap[fileName] = mod.default;
}

// ✅ CORREÇÃO DO ERRO DE BUILD AQUI
const getCardImage = (t: any) => {
    const rawImageName = t?.coverImage || t?.CoverImage;
    const imageName = typeof rawImageName === 'string' ? rawImageName : '';

    // 1. Tenta pegar a imagem específica
    if (imageName && coversMap[imageName]) {
        return coversMap[imageName];
    }
    
    // 2. Se não achar, pega a primeira disponível (Fallback)
    const keys = Object.keys(coversMap);
    if (keys.length > 0) {
        const firstKey = keys[0];
        // Validação extra para o TypeScript não reclamar de 'undefined'
        if (typeof firstKey === 'string') {
            return coversMap[firstKey];
        }
    }
    return ''; 
};

// --- FORMATADORES VISUAIS ---
const formatCurrency = (val: number) => val.toFixed(2).replace('.', ',');
const formatDateSimple = (dateStr: string) => {
    if(!dateStr) return '--/--';
    const d = new Date(dateStr);
    return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
};
const formatTimeSimple = (dateStr: string) => {
    if(!dateStr) return '--:--';
    const d = new Date(dateStr);
    return d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
};

// --- LÓGICA DO CARROSSEL (MATEMÁTICA PRESERVADA) ---
const carouselIndex = ref(0); 
const itemsPerPage = ref(6);

const visibleTournaments = computed(() => {
    const list = props.tournaments; // Usa a prop recebida
    const len = list.length;
    if (len === 0) return [];
    if (len <= itemsPerPage.value) return list;

    const result = [];
    for (let i = 0; i < itemsPerPage.value + 1; i++) { 
        const index = (carouselIndex.value + i) % len;
        result.push(list[index]); 
    }
    return result;
});

const navigateCarousel = (direction: 'prev' | 'next') => {
    const len = props.tournaments.length;
    if (len === 0) return;
    if (direction === 'next') {
        carouselIndex.value = (carouselIndex.value + 1) % len;
    } else {
        carouselIndex.value = (carouselIndex.value - 1 + len) % len;
    }
};

const updateItemsPerPage = () => {
    const w = window.innerWidth;
    if (w < 640) itemsPerPage.value = 2;
    else if (w < 768) itemsPerPage.value = 3;
    else if (w < 1280) itemsPerPage.value = 4;
    else itemsPerPage.value = 6;
};

// --- AÇÕES INTERNAS ---
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

// Lida com o clique no card ou botão
const handleAction = (t: any) => {
    if (t.isJoined) {
        emit('enter', t.id);
    } else {
        // Confirmação visual antes de emitir o join
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
            if (result.isConfirmed) emit('join', t.id);
        });
    }
};

onMounted(() => {
    updateItemsPerPage();
    window.addEventListener('resize', updateItemsPerPage);
});

onUnmounted(() => {
    window.removeEventListener('resize', updateItemsPerPage);
});
</script>

<template>
    <div class="w-full">
        <div class="mb-0 flex items-center gap-2 pl-1">
            <h2 class="text-base font-bold text-white tracking-wide">{{ title }}</h2>
            <ChevronRight class="w-3.5 h-3.5 text-gray-600" />
        </div>

        <div class="carousel-container relative group/carousel min-h-[350px]">
            
            <button 
                v-if="tournaments.length > itemsPerPage" 
                @click="navigateCarousel('prev')" 
                class="handle handle-prev z-50 md:opacity-0 md:group-hover:opacity-100"
            >
                <ChevronLeft class="w-8 h-8 text-white transition-transform transform md:group-hover:scale-125" />
            </button>

            <div v-if="tournaments.length === 0" class="h-[330px] w-full flex items-center justify-center text-xs text-gray-500 border border-dashed border-gray-800 rounded-lg mt-1">
                <p>Nenhum torneio nesta categoria.</p>
            </div>

            <div v-else class="slider flex gap-4 overflow-hidden px-2 pb-4 pt-1">
                <transition-group name="list-anim">
                    <div 
                        v-for="t in visibleTournaments" 
                        :key="t.id" 
                        class="movie-card shrink-0 group"
                        @click="handleAction(t)"
                    >
                        
                        <div class="absolute top-3 left-3 z-30 pointer-events-none drop-shadow-md">
                             <span class="text-[9px] font-mono font-bold text-white/50 tracking-tighter">#{{ t.id }}</span>
                        </div>

                        <div class="absolute top-3 right-3 z-30 flex items-center gap-1 bg-black/60 border border-white/10 px-2 py-0.5 rounded-full backdrop-blur-sm pointer-events-none">
                            <Users class="w-2.5 h-2.5 text-gray-300" />
                            <span class="text-[9px] font-bold text-white">
                                {{ t.participantsCount }}
                                <span class="text-white/60" v-if="t.maxParticipants > 0">/{{ t.maxParticipants }}</span>
                            </span>
                        </div>

                        <div class="card-cover">
                            <img :src="getCardImage(t)" alt="Tournament Cover" 
                                class="w-full h-full object-cover transition-transform duration-700 ease-out group-hover:scale-110 opacity-80 group-hover:opacity-60">
                            <div class="absolute inset-0 bg-gradient-to-t from-[#050505] via-[#050505]/40 to-transparent"></div>
                        </div>

                        <div class="card-body relative z-20 flex flex-col h-full pt-10 px-3 pb-3">
                            
                            <div class="w-full text-center mt-6 mb-2">
                                <h3 class="text-[13px] font-black text-white italic uppercase leading-tight drop-shadow-[0_2px_4px_rgba(0,0,0,0.8)] line-clamp-2 min-h-[2.5em] flex items-center justify-center tracking-wide">
                                    {{ t.name }}
                                </h3>
                            </div>

                            <div class="flex-1 flex flex-col items-center justify-center w-full">
                                <div class="flex items-center gap-1.5 mb-1 opacity-90 drop-shadow-md">
                                    <span class="w-0.5 h-3 bg-red-500 rounded-full box-shadow-[0_0_5px_red]"></span>
                                    <span class="text-[8px] font-bold uppercase tracking-widest text-white">
                                        {{ t.sport || 'Esporte' }}
                                    </span>
                                </div>
                                
                                <span class="text-[7px] text-white/70 uppercase font-black tracking-widest mb-0.5 drop-shadow-sm">Premiação</span>
                                
                                <span class="text-2xl font-black text-emerald-400 drop-shadow-[0_2px_8px_rgba(0,0,0,0.9)] tracking-tighter leading-none flex items-start">
                                    <span class="text-[10px] font-bold text-emerald-500 mt-1 mr-0.5">R$</span>
                                    {{ formatCurrency(t.prizePool || 0).replace('R$', '').trim() }}
                                </span>
                            </div>

                            <div class="flex items-center justify-center w-full gap-3 mt-1 mb-4 border-t border-white/10 pt-2 drop-shadow-md">
                                <div class="flex flex-col items-end">
                                    <span class="text-[7px] font-black uppercase text-green-400 mb-0.5 tracking-tighter">Início</span>
                                    <span class="text-[9px] font-bold text-white leading-none">{{ formatDateSimple(t.startDate) }}</span>
                                    <span class="text-[8px] font-medium text-white/60 leading-none mt-0.5">{{ formatTimeSimple(t.startDate) }}</span>
                                </div>
                                <div class="w-px h-5 bg-white/20"></div>
                                <div class="flex flex-col items-start">
                                    <span class="text-[7px] font-black uppercase text-red-400 mb-0.5 tracking-tighter">Encerra</span>
                                    <span class="text-[9px] font-bold text-white leading-none">{{ formatDateSimple(t.endDate) }}</span>
                                    <span class="text-[8px] font-medium text-white/60 leading-none mt-0.5">{{ formatTimeSimple(t.endDate) }}</span>
                                </div>
                            </div>

                            <div class="flex justify-between items-end w-full mt-auto">
                                <button @click.stop="showInfo(t)" class="p-2 rounded-lg border border-white/20 text-white/60 hover:text-white hover:bg-white/10 transition-all bg-black/30 backdrop-blur-sm">
                                    <Info class="w-4 h-4" />
                                </button>

                                <div class="flex flex-col items-end gap-1.5">
                                    <div v-if="t.entryFee == 0">
                                        <span class="text-[9px] font-black uppercase text-green-400 tracking-widest drop-shadow-lg">Grátis</span>
                                    </div>
                                    <div v-else class="flex flex-col items-end leading-none drop-shadow-md">
                                        <span class="text-[6px] text-white/60 uppercase font-black mb-0.5">Entrada</span>
                                        <span class="text-[11px] font-black text-white">R$ {{ formatCurrency(t.entryFee).replace('R$', '') }}</span>
                                    </div>

                                    <button 
                                        @click.stop="t.isJoined ? emit('enter', t.id) : handleAction(t)"
                                        class="h-8 px-4 flex items-center justify-center gap-1.5 rounded font-black text-[10px] uppercase tracking-wider shadow-xl transition-all transform active:scale-95 border border-transparent min-w-[100px]"
                                        :class="t.isJoined 
                                            ? 'bg-white text-black hover:bg-gray-200' 
                                            : 'bg-red-600 text-white hover:bg-red-700'"
                                        :disabled="processingId === t.id"
                                    >
                                        <span v-if="processingId === t.id" class="loader-spin border-current w-3 h-3"></span>
                                        <template v-else>
                                            <component :is="t.isJoined ? Play : LogIn" class="w-3.5 h-3.5 fill-current" />
                                            {{ t.isJoined ? 'Jogar' : 'Entrar' }}
                                        </template>
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </transition-group>
            </div>

            <button 
                v-if="tournaments.length > itemsPerPage"
                @click="navigateCarousel('next')" 
                class="handle handle-next z-50 md:opacity-0 md:group-hover:opacity-100"
            >
                <ChevronRight class="w-8 h-8 text-white transition-transform transform md:group-hover:scale-125" />
            </button>
        </div>
    </div>
</template>

<style scoped>
.scrollbar-hide::-webkit-scrollbar { display: none; }
.scrollbar-hide { -ms-overflow-style: none; scrollbar-width: none; }

/* CARD ESTILO NETFLIX */
.movie-card {
    min-width: 200px;
    max-width: 200px;
    height: 330px;
    position: relative;
    border-radius: 12px; 
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
    box-shadow: 0 20px 50px rgba(0,0,0,0.9);
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
        rgba(0,0,0,0.1) 0%, 
        rgba(0,0,0,0.0) 30%, 
        rgba(0,0,0,0.7) 60%, 
        rgba(5,5,5,0.95) 90%, 
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

/* SETAS */
.carousel-container { position: relative; }

.handle {
    position: absolute;
    top: 1rem; 
    height: 330px; 
    z-index: 50;
    width: 3.5rem; 
    background: linear-gradient(90deg, rgba(0,0,0,0.8) 0%, rgba(0,0,0,0) 100%);
    display: flex; align-items: center; justify-content: center;
    cursor: pointer; border: none;
    transition: opacity 0.3s ease;
    opacity: 1; 
    border-radius: 8px; 
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
    .handle {
        background: transparent !important; 
        width: 30px; 
        height: 240px; 
        top: 1rem;
    }
    .handle-prev { left: 0 !important; }
    .handle-next { right: 0 !important; }

    .movie-card {
        min-width: 160px;
        max-width: 160px;
        height: 260px;
    }
    .card-body { padding: 0.6rem; }
}

@keyframes spin { to { transform: rotate(360deg); } }
</style>