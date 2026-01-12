import { createRouter, createWebHistory } from 'vue-router'
import Home from '../views/Home.vue'
import SportEvents from '../views/SportEvents.vue'
import MyBets from '../views/MyBets.vue'
import ConfigPage from '../views/ConfigPage.vue' // <--- 1. IMPORTAR A PÁGINA DE CONFIGURAÇÃO

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { 
      path: '/', 
      name: 'home', 
      component: Home 
    },
    // --- ROTA DINÂMICA DE ESPORTES ---
    { 
      path: '/sports/:id', 
      name: 'sport-events', 
      component: SportEvents 
    },
    // --- HISTÓRICO DE APOSTAS ---
    {
      path: '/minhas-apostas',
      name: 'MyBets',
      component: MyBets
    },
    // --- NOVA ROTA: CONFIGURAÇÕES GERAIS ---
    {
      path: '/config',       // O link será: localhost:5173/config
      name: 'Config',
      component: ConfigPage
    }
  ]
})

export default router