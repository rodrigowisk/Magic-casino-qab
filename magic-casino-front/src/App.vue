<script setup lang="ts">
import { onMounted } from 'vue';
import MainLayout from './layouts/MainLayout.vue';
import { useAuthStore } from './stores/useAuthStore';

// Instancia a Store de Autenticação
const authStore = useAuthStore();

// ✅ O SEGREDINHO: 
// Assim que o componente App montar (no F5), verificamos se tem token.
// Se tiver, mandamos buscar o saldo real no banco de dados.
onMounted(async () => {
  if (authStore.token) {
    console.log(">>>>> APP INICIOU: Forçando atualização do saldo...");
    await authStore.fetchBalance();
  }
});
</script>

<template>
  <MainLayout>
    <router-view />
  </MainLayout>
</template>