<script setup lang="ts">
import { computed } from 'vue';
import { X, Trophy, Users, Calendar, Coins } from 'lucide-vue-next';

const props = defineProps<{
  show: boolean;
  tournament: any | null;
}>();

const emit = defineEmits(['close', 'join']);

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

// --- FORMATADORES ---
const formatCurrency = (val: number) => val?.toFixed(2).replace('.', ',') || '0,00';
const formatDateSimple = (dateStr: string) => (!dateStr ? '--/--' : new Date(dateStr).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' }));
const formatTimeSimple = (dateStr: string) => (!dateStr ? '--:--' : new Date(dateStr).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }));

const isFull = computed(() => {
    const t = props.tournament;
    if(!t) return false;
    return t.maxParticipants > 0 && (t.participantsCount || 0) >= t.maxParticipants;
});

const isJoined = computed(() => !!props.tournament?.isJoined);

</script>

<template>
  <Teleport to="body">
    <Transition name="pop-alert">
      <div v-if="show" class="fixed inset-0 z-[99999] flex items-center justify-center p-4">
        
        <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" @click="emit('close')"></div>

        <div class="relative w-full max-w-[500px] flex flex-col bg-[#111] border border-white/20 rounded-xl shadow-[0_0_40px_rgba(0,0,0,0.8)] overflow-hidden" @click.stop>
            
            <div class="absolute inset-0 z-0 overflow-hidden pointer-events-none">
                <div class="absolute inset-0 bg-[#111]"></div>
                <div class="absolute top-0 right-0 w-1/2 h-full">
                    <img :src="cardImage"
                         :class="[
                            'w-full h-full object-cover',
                            (!isJoined && isFull) ? 'grayscale opacity-50' : 'opacity-80'
                         ]" />
                    <div class="absolute inset-0 bg-gradient-to-l from-transparent via-[#111]/60 to-[#111]"></div>
                </div>
            </div>

            <div class="relative z-10 p-5 flex flex-col gap-4">
                
                <div class="flex justify-between items-start">
                    <div class="flex flex-col gap-1 w-full pr-8">
                        <div class="flex items-center gap-3">
                            <span class="px-1.5 py-0.5 rounded bg-white/20 backdrop-blur-sm text-[9px] font-mono text-white shadow-sm border border-white/10">ID: #{{ tournament?.id }}</span>
                            <span class="text-[10px] font-bold text-emerald-400 uppercase flex items-center gap-1 drop-shadow-md"><Trophy class="w-3.5 h-3.5" /> {{ displaySport }}</span>
                        </div>
                        <h3 class="text-xl font-black text-white italic uppercase leading-tight drop-shadow-lg mt-1 truncate w-full">{{ tournament?.name }}</h3>
                    </div>
                    <button type="button" @click="emit('close')" class="absolute top-4 right-4 text-white/60 hover:text-white transition-colors bg-black/40 rounded-full p-1.5 backdrop-blur-sm border border-white/10 hover:bg-black/60 cursor-pointer">
                        <X class="w-4 h-4" />
                    </button>
                </div>

                <div class="text-slate-300 text-xs leading-relaxed line-clamp-2 drop-shadow-md font-medium pr-2 w-2/3">
                    {{ tournament?.description || 'Participe deste torneio emocionante e mostre suas habilidades para conquistar grandes prêmios!' }}
                </div>

                <div class="grid grid-cols-3 gap-2 items-center relative z-20 mt-1">
                    <div class="flex flex-col gap-0.5 border-r border-white/10 px-1">
                        <span class="text-[9px] uppercase text-slate-400 font-bold flex items-center gap-1"><Calendar class="w-2.5 h-2.5 text-blue-400" /> Início</span>
                        <span class="text-[10px] text-white font-mono leading-none">{{ formatDateSimple(tournament?.startDate) }} <span class="text-[9px] text-white/40 block md:inline">{{ formatTimeSimple(tournament?.startDate) }}</span></span>
                    </div>
                    <div class="flex flex-col gap-0.5 border-r border-white/10 px-1">
                        <span class="text-[9px] uppercase text-slate-400 font-bold flex items-center gap-1"><Calendar class="w-2.5 h-2.5 text-red-400" /> Fim</span>
                        <span class="text-[10px] text-white font-mono leading-none">{{ formatDateSimple(tournament?.endDate) }} <span class="text-[9px] text-white/40 block md:inline">{{ formatTimeSimple(tournament?.endDate) }}</span></span>
                    </div>
                    <div class="flex flex-col gap-0.5 px-1">
                        <span class="text-[9px] uppercase text-slate-400 font-bold flex items-center gap-1"><Users class="w-2.5 h-2.5 text-yellow-400" /> Inscritos</span>
                        <span class="text-[10px] font-bold text-white leading-none">{{ tournament?.participantsCount || 0 }} <span class="text-white/60" v-if="tournament?.maxParticipants > 0">/ {{ tournament?.maxParticipants }}</span></span>
                    </div>
                </div>

                <div class="flex items-center justify-between mt-2 pt-3 border-t border-white/10 relative z-20">
                    <div class="flex flex-col">
                        <span class="text-[9px] uppercase text-slate-400 font-bold mb-0.5">Premiação</span>
                        <span class="text-xl font-black text-emerald-400 flex items-center gap-1 drop-shadow-md"><Coins class="w-4 h-4" /> R$ {{ formatCurrency(tournament?.prizePool || 0) }}</span>
                    </div>
                    <div class="flex items-center gap-3">
                        <div class="flex flex-col items-end mr-1">
                            <span class="text-[9px] uppercase text-slate-400 font-bold mb-0.5">Entrada</span>
                            <span class="text-sm font-black text-white drop-shadow-md">{{ tournament?.entryFee == 0 ? 'GRÁTIS' : `R$ ${formatCurrency(tournament?.entryFee || 0)}` }}</span>
                        </div>
                        
                        <button type="button" @click="emit('join', tournament?.id)" 
                                :disabled="isJoined || (!isJoined && isFull)"
                                :class="[
                                    'px-6 py-2.5 rounded font-black uppercase text-xs tracking-wider transition-all shadow-lg border',
                                    isJoined 
                                        ? 'bg-emerald-600 text-white border-emerald-500 cursor-not-allowed opacity-90'
                                        : (!isJoined && isFull)
                                            ? 'bg-gray-400 text-gray-900 border-gray-500 cursor-not-allowed opacity-90 shadow-none'
                                            : 'bg-white text-black hover:bg-gray-200 hover:shadow-emerald-500/20 active:scale-95 border-transparent cursor-pointer'
                                ]">
                            {{ isJoined ? 'INSCRITO' : (!isJoined && isFull) ? 'LOTADO' : 'COMPRAR' }}
                        </button>
                    </div>
                </div>
            </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.pop-alert-enter-active { animation: popAlert 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275); }
.pop-alert-leave-active { animation: popAlert 0.2s ease-in reverse; }

@keyframes popAlert {
    0% { opacity: 0; transform: scale(0.8); }
    100% { opacity: 1; transform: scale(1); }
}
</style>