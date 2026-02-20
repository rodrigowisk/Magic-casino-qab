<script setup lang="ts">
import Header from '../components/Header.vue'; 
</script>

<template>
  <div class="h-screen bg-[#0f172a] text-slate-300 font-sans flex flex-col overflow-hidden relative">
    
    <Header />

    <main class="flex-1 bg-[#0f172a] relative transition-all duration-300 !p-0 flex flex-col h-full w-full overflow-hidden">
        <router-view v-slot="{ Component }">
            <transition name="fade" mode="out-in">
                <component :is="Component" class="h-full w-full" />
            </transition>
        </router-view>
    </main>

  </div>
</template>

<style>
@import url('https://fonts.googleapis.com/css2?family=Montserrat:wght@400;700;900&display=swap');

.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>