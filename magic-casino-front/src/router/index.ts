import { createRouter, createWebHistory } from 'vue-router'

// Layouts
import MainLayout from '../layouts/MainLayout.vue';
import AdminLayout from '../layouts/AdminLayout.vue';

// Views Públicas
import Home from '../views/Home.vue'
import SportEvents from '../views/SportEvents.vue'
import MyBets from '../views/MyBets.vue'
import LiveEvents from '../views/LiveEvents.vue' 
import EventDetails from '../views/EventDetails.vue'
import DepositView from '../views/DepositView.vue' // ✅ NOVA IMPORTAÇÃO

// Admin Views
import TournamentAdminList from '../views/Admin/Tournament/TournamentAdminList.vue'
import TournamentCreate from '../views/Admin/Tournament/TournamentCreate.vue'
import ConfigPage from '../views/ConfigPage.vue'

// ✅ IMPORTAÇÃO DO NOVO COMPONENTE DE CONFIGURAÇÃO DE ESPORTES
// (Verifique se o arquivo está exatamente nesta pasta e com este nome)
import SportsConfiguration from '../components/Admin/SportsConfiguration.vue'

// Componente placeholder para telas que ainda vamos criar
const EmBreve = { template: '<div class="p-10 text-white text-xl">🚧 Módulo em construção...</div>' }

const router = createRouter({
  history: createWebHistory(),
  routes: [
    // 🌍 ÁREA PÚBLICA
    {
      path: '/',
      component: MainLayout,
      children: [
        { path: '', name: 'home', component: Home },
        { path: 'sports/:id', name: 'sport-events', component: SportEvents },
        { path: 'minhas-apostas', name: 'MyBets', component: MyBets },
        { path: 'live', name: 'live', component: LiveEvents },
        { path: 'event/:id', name: 'event-details', component: EventDetails },
        
        // ✅ NOVA ROTA DE DEPÓSITO
        { path: 'deposito', name: 'deposit', component: DepositView },
      ]
    },

    // 🔒 ÁREA ADMINISTRATIVA
    {
      path: '/admin',
      component: AdminLayout,
      children: [
        // Redireciona /admin direto para torneios
        { path: '', redirect: '/admin/tournaments' },

        // --- ABA 1: TORNEIOS ---
        { path: 'tournaments', name: 'TournamentAdminList', component: TournamentAdminList },
        { path: 'tournaments/create', name: 'TournamentCreate', component: TournamentCreate },

        // --- ABA 2: SPORTBOOK (CONFIGURAÇÃO) ---
        // ✅ Aqui conectamos a sua tela nova
        { path: 'sportbook', name: 'SportsConfiguration', component: SportsConfiguration },

        // --- ABA 3: CASSINO ---
        { path: 'casino', name: 'AdminCasino', component: EmBreve },

        // --- ABA 4: CONFIGURAÇÕES GERAIS ---
        { path: 'config', name: 'AdminConfig', component: ConfigPage }
      ]
    }
  ]
})

export default router