<script setup lang="ts">
import Header from '../components/Header.vue'; 
import Footer from '../components/Footer.vue'; 
</script>

<template>
  <div class="h-screen w-full bg-[#020617] text-slate-300 font-sans flex flex-col overflow-hidden m-0 p-0 relative selection:bg-blue-500/30 selection:text-blue-200">
    
    <div class="absolute inset-0 z-0 pointer-events-none overflow-hidden">
        
        <div class="absolute inset-0 bg-gradient-to-br from-[#0f172a] via-[#050a14] to-black"></div>

        <div class="absolute -top-[300px] -left-[200px] w-[800px] h-[800px] bg-blue-600/10 rounded-full blur-[180px] opacity-50 mix-blend-screen"></div>

        <div class="absolute top-[10%] -right-[300px] w-[600px] h-[600px] bg-indigo-900/10 rounded-full blur-[150px] opacity-40 mix-blend-screen"></div>

        <div class="absolute -bottom-[200px] left-[20%] w-[1000px] h-[500px] bg-blue-950/5 rounded-full blur-[120px]"></div>
    
    </div>

    <div class="relative z-10 flex flex-col h-full w-full">
        
        <Header class="shrink-0 z-50" />

        <div class="flex flex-1 overflow-hidden relative w-full">
          
          <main class="flex-1 overflow-y-auto bg-transparent custom-scrollbar relative transition-all duration-300 w-full flex flex-col">
            
            <div class="w-full min-h-full flex flex-col relative">
                
                <div class="flex-1 w-full flex flex-col relative z-0">
                    <router-view />
                </div>

                <Footer class="shrink-0 mt-auto relative z-10" />
            </div>

          </main>
          
        </div>
    </div>

  </div>
</template>

<style>
@import url('https://fonts.googleapis.com/css2?family=Montserrat:wght@400;700;900&display=swap');

/* Barra de rolagem sutil para combinar com o tema escuro */
.custom-scrollbar::-webkit-scrollbar { width: 6px; height: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #1e293b; border-radius: 3px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #334155; }
</style>