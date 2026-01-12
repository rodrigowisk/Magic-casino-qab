<script setup lang="ts">
// A home agora é simples, só tem os banners.
// O menu de esportes está no Layout.
</script>

<template>
  <div class="space-y-6">
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <div class="bg-stake-card rounded-lg p-6 relative overflow-hidden group cursor-pointer hover:-translate-y-1 transition-transform">
        <div class="relative z-10">
          <span class="bg-white text-stake-dark text-xs font-bold px-2 py-0.5 rounded">Promoção</span>
          <h3 class="text-white font-bold text-xl mt-2">Múltipla Turbinada</h3>
          <p class="text-stake-text text-sm mb-4">Ganhe até 100% extra.</p>
          <button class="bg-stake-blue text-white px-4 py-2 rounded font-bold text-sm">Apostar</button>
        </div>
        <div class="absolute right-[-20px] bottom-[-20px] text-8xl opacity-20 rotate-12">🚀</div>
      </div>
      
      <div class="bg-stake-card rounded-lg p-6 relative overflow-hidden group cursor-pointer hover:-translate-y-1 transition-transform">
        <div class="relative z-10">
          <span class="bg-white text-stake-dark text-xs font-bold px-2 py-0.5 rounded">Ao Vivo</span>
          <h3 class="text-white font-bold text-xl mt-2">NBA Finals</h3>
          <p class="text-stake-text text-sm mb-4">Assista e aposte agora.</p>
          <button class="border border-white/20 text-white px-4 py-2 rounded font-bold text-sm hover:bg-white/10">Ver Jogos</button>
        </div>
        <div class="absolute right-[-20px] bottom-[-20px] text-8xl opacity-20 rotate-12">🏀</div>
      </div>
    </div>
  </div>
</template>