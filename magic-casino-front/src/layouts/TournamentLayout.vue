<script setup lang="ts">
import Sidebar from '../components/Sidebar.vue';

// Não importamos o BetSlip.vue aqui!
// O cupom do torneio é gerenciado internamente pelas páginas (Lobby/Play)
</script>

<template>
  <div class="flex h-screen bg-[#0f172a] text-white overflow-hidden font-sans">
    
    <Sidebar />

    <div class="flex-1 flex flex-col h-full relative overflow-y-auto custom-scrollbar">
      
      <router-view v-slot="{ Component }">
        <transition name="fade" mode="out-in">
          <component :is="Component" />
        </transition>
      </router-view>

    </div>

    </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #0f172a; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #475569; }

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>