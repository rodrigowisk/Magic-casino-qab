import { createRouter, createWebHistory, RouterView } from 'vue-router'

// Views Públicas
import Home from '../views/Home.vue'
import SportEvents from '../views/SportEvents.vue'
import MyBets from '../views/MyBets.vue'
import LiveEvents from '../views/LiveEvents.vue' 
import EventDetails from '../views/EventDetails.vue'
import DepositView from '../views/DepositView.vue' 

// ✅ Views de Torneio
import TournamentLobby from '../views/Tournament/TournamentLobby.vue'
import TournamentWrapper from '../views/Tournament/TournamentWrapper.vue' // ✅ NOVO IMPORT (Wrapper Pai)
import TournamentPlay from '../views/Tournament/TournamentPlay.vue'
import TournamentLive from '../views/Tournament/TournamentLive.vue' 
import TournamentMyBets from '../views/Tournament/TournamentMyBets.vue'
import TournamentRanking from '../views/Tournament/TournamentRanking.vue'
import TournamentHistory from '../views/Tournament/TournamentHistory.vue'

// ✅ Admin Views
import TournamentAdminList from '../views/Admin/Tournament/TournamentAdminList.vue'
import TournamentCreate from '../views/Admin/Tournament/TournamentCreate.vue'
import ConfigPage from '../views/ConfigPage.vue'
import SportsConfiguration from '../components/Admin/SportsConfiguration.vue'

// Placeholder
const EmBreve = { template: '<div class="p-10 text-white text-xl">🚧 Módulo em construção...</div>' }

const router = createRouter({
  history: createWebHistory(),
  routes: [
    
    // =======================================================
    // 🌍 1. ROTAS DO SPORTBOOK (Layout Padrão)
    // =======================================================
    { 
      path: '/', 
      name: 'home', 
      component: Home,
      meta: { layout: 'default' } 
    },
    { 
      path: '/sports/:id', 
      name: 'sport-events', 
      component: SportEvents,
      meta: { layout: 'default' } 
    },
    { 
      path: '/minhas-apostas', 
      name: 'MyBets', 
      component: MyBets,
      meta: { layout: 'default', requiresAuth: true }
    },
    { 
      path: '/live', 
      name: 'live', 
      component: LiveEvents,
      meta: { layout: 'default' }
    },
    { 
      path: '/event/:id', 
      name: 'event-details', 
      component: EventDetails,
      meta: { layout: 'default' }
    },
    { 
      path: '/deposito', 
      name: 'deposit', 
      component: DepositView,
      meta: { layout: 'default', requiresAuth: true }
    },
    { 
      path: '/profile', 
      name: 'Profile', 
      component: () => import('../views/Profile.vue'),
      meta: { layout: 'default', requiresAuth: true } 
    },
    { 
      path: '/transactions', 
      name: 'Transactions', 
      component: () => import('../views/Transactions.vue'),
      meta: { layout: 'default', requiresAuth: true } 
    },

    // =======================================================
    // 🏆 2. ROTAS DE TORNEIO (Layout Limpo / Tournament)
    // =======================================================
    { 
      path: '/tournaments', 
      name: 'TournamentLobby', 
      component: TournamentLobby,
      meta: { layout: 'tournament', requiresAuth: true }
    },
    
    // ✅ ROTA PAI DO TORNEIO (WRAPPER)
    // As sub-rotas agora renderizam dentro do <router-view> do TournamentWrapper
    { 
      path: '/tournament/:id',
      component: TournamentWrapper, // O Wrapper segura o Carrossel
      meta: { layout: 'tournament', requiresAuth: true },
      // Redireciona para 'play' se acessar apenas /tournament/123
      redirect: to => { return { path: `/tournament/${to.params.id}/play` }}, 
      children: [
        { 
          path: 'play', // Nota: Sem a barra '/' na frente (caminho relativo)
          name: 'TournamentPlay', 
          component: TournamentPlay,
          props: true
        },
        { 
          path: 'live', 
          name: 'TournamentLive', 
          component: TournamentLive,
          props: true
        },
        { 
          path: 'my-bets', 
          name: 'TournamentMyBets', 
          component: TournamentMyBets,
          props: true
        },
        { 
          path: 'history', 
          name: 'TournamentHistory', 
          component: TournamentHistory,
          props: true
        },
        { 
          path: 'ranking', 
          name: 'TournamentRanking', 
          component: TournamentRanking,
          props: true
        }
      ]
    },

    // =======================================================
    // 🔒 3. ÁREA ADMINISTRATIVA (Layout Admin)
    // =======================================================
    {
      path: '/admin',
      component: RouterView,
      meta: { layout: 'admin', requiresAuth: true, isAdmin: true },
      children: [
        { path: '', redirect: '/admin/tournaments' },
        
        // Torneios
        { path: 'tournaments', name: 'TournamentAdminList', component: TournamentAdminList },
        { path: 'tournaments/create', name: 'TournamentCreate', component: TournamentCreate },

        // Configurações
        { path: 'sportbook', name: 'SportsConfiguration', component: SportsConfiguration },
        { path: 'casino', name: 'AdminCasino', component: EmBreve },
        { path: 'config', name: 'AdminConfig', component: ConfigPage }
      ]
    }
  ]
})

export default router