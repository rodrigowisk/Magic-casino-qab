<script setup lang="ts">
import { onMounted } from 'vue';
import { useAuthStore } from './stores/useAuthStore';
// Removemos o import do MainLayout daqui, pois ele será controlado pelo Router

// Instancia a Store de Autenticação
const authStore = useAuthStore();

// ✅ MANTIDO: O SEGREDINHO DO SALDO
onMounted(async () => {
  if (authStore.token) {
    console.log(">>>>> APP INICIOU: Forçando atualização do saldo...");
    await authStore.fetchBalance();
  }
});
</script>

<template>
  <router-view />
</template>

<style>
/* Estilos globais básicos */
body {
  background-color: #121212;
  margin: 0;
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
}
</style>