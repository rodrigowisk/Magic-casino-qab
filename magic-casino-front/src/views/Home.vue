<script setup lang="ts">
import { useRouter } from 'vue-router';
import HeroBanner from '../components/HeroBanner.vue';
import GameHighlights from '../components/GameHighlights.vue';
import LiveCarousel from '../components/LiveCarousel.vue'; // ✅ Importado

const router = useRouter();

const handleGameSelect = (gameId: number) => {
  router.push(`/event/${gameId}`);
};
</script>

<template>
  <div class="p-4 md:p-6 space-y-6 max-w-[1400px] mx-auto">
    
    <HeroBanner />

    <GameHighlights @select-game="handleGameSelect" />

    <LiveCarousel />

  </div>
</template>