<script setup lang="ts">
import { onMounted, computed } from 'vue';
import { useAuthStore } from './stores/useAuthStore';
import { useRoute } from 'vue-router';

// ✅ 1. Importe os Layouts que você tem na pasta /layouts
import MainLayout from './layouts/MainLayout.vue';       // Sportbook (Padrão)
import AdminLayout from './layouts/AdminLayout.vue';     // Painel Admin
import TournamentLayout from './layouts/TournamentLayout.vue'; // Novo Layout Limpo

const authStore = useAuthStore();
const route = useRoute();

// ✅ 2. Mapeamento de nomes para componentes
const layouts: Record<string, any> = {
  default: MainLayout,
  admin: AdminLayout,
  tournament: TournamentLayout,
};

// ✅ 3. Lógica para decidir qual layout usar
// Ele olha o campo "meta: { layout: '...' }" que definimos no router/index.ts
const currentLayout = computed(() => {
  const layoutName = (route.meta.layout as string) || 'default';
  return layouts[layoutName] || MainLayout;
});

// ✅ MANTIDO: O SEGREDINHO DO SALDO
onMounted(async () => {
  if (authStore.token) {
    await authStore.fetchBalance();
  }
});
</script>

<template>
  <component :is="currentLayout" />
</template>

<style>
/* Estilos globais básicos */
body {
  background-color: #121212;
  margin: 0;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
}
</style>