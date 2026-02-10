<script setup lang="ts">
import Header from '../components/Header.vue'; 
</script>

<template>
  <div class="h-screen bg-[#0f172a] text-slate-300 font-sans flex flex-col overflow-hidden">
    
    <Header />

    <div class="flex flex-1 overflow-hidden relative">
      
      <main class="flex-1 overflow-y-auto bg-[#0f172a] custom-scrollbar relative transition-all duration-300 !p-0">
        <div class="w-full h-full pb-20 md:pb-0">
            <router-view />
        </div>
      </main>
      
    </div>
  </div>
</template>

<style>
@import url('https://fonts.googleapis.com/css2?family=Montserrat:wght@400;700;900&display=swap');

.custom-scrollbar::-webkit-scrollbar { width: 6px; height: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #0f172a; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 3px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #475569; }
</style>