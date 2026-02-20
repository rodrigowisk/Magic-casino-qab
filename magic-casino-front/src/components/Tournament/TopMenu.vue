<script setup lang="ts">
import { useRouter, useRoute } from 'vue-router';
import { 
    Trophy, ArrowLeftCircle, Swords 
} from 'lucide-vue-next';

const router = useRouter();
const route = useRoute();

// --- NAVEGAÇÃO ---
const goBackToLobby = () => router.push('/tournaments');

const goToMyTournaments = () => {
    const currentId = route.params.id || '0';
    router.push(`/tournament/${currentId}/history`);
};
</script>

<template>
    <div class="px-4 flex flex-col justify-center gap-3 shrink-0 h-full relative bg-[#050505] z-50 -mr-[1px]">
            
        <div class="flex items-center gap-2 mb-1 pb-2 border-b border-white/5">
            <div class="p-1.5 rounded-full bg-blue-500/10 border border-blue-500/20">
                <Trophy class="w-4 h-4 text-blue-400" />
            </div>
            <span class="text-[11px] font-black uppercase text-blue-400 tracking-widest">
                Menu Torneio
            </span>
        </div>
        
        <button 
            @click="goToMyTournaments" 
            class="flex items-center gap-3 text-[11px] font-bold text-slate-300 hover:text-white transition-all bg-[#121214] hover:bg-blue-600/20 py-2.5 px-3 rounded-lg w-full text-left border border-white/5 hover:border-blue-500/30 group"
        >
            <Swords class="w-4 h-4 text-yellow-500 group-hover:scale-110 transition-transform" /> 
            <span>Meus Torneios</span>
        </button>

        <div class="h-px bg-gradient-to-r from-transparent via-white/5 to-transparent w-full"></div>

        <button 
            @click="goBackToLobby" 
            class="flex items-center gap-2 text-[11px] font-bold text-red-400 hover:text-red-300 transition-colors bg-red-500/5 hover:bg-red-500/10 py-2.5 px-3 rounded-lg w-full justify-center border border-red-500/10 hover:border-red-500/20"
        >
            <ArrowLeftCircle class="w-4 h-4" /> 
            <span>Sair para o Lobby</span>
        </button>

    </div>
</template>