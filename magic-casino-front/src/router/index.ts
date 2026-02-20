import { createRouter, createWebHistory, RouterView } from 'vue-router'

// Views Públicas
//import Home from '../views/Home.vue'
import SportEvents from '../views/SportEvents.vue'
import MyBets from '../views/MyBets.vue'
import LiveEvents from '../views/LiveEvents.vue' 
import EventDetails from '../views/EventDetails.vue'
import DepositView from '../views/DepositView.vue' 

// ✅ Views de Torneio
import TournamentLobby from '../views/Tournament/TournamentLobby.vue'
import TournamentWrapper from '../views/Tournament/TournamentWrapper.vue'
import TournamentPlay from '../views/Tournament/TournamentPlay.vue'
import TournamentLive from '../views/Tournament/TournamentLive.vue' 
import TournamentMyBets from '../views/Tournament/TournamentMyBets.vue'
import TournamentRanking from '../views/Tournament/TournamentRanking.vue'
import TournamentHistory from '../views/Tournament/TournamentHistory.vue'
import TournamentMatchDetail from '../views/Tournament/TournamentMatchDetail.vue';
import TournamentLiveMatchDetail from '../views/Tournament/TournamentLiveMatchDetail.vue';
import Favorites from '../views/Tournament/Favorites.vue';
// Importando o Info explicitamente para manter padrão (opcional, mas recomendado)
import TournamentInfo from '../views/Tournament/TournamentInfo.vue'; 

// ✅ Admin Views
import TournamentAdminList from '../views/Admin/Tournament/TournamentAdminList.vue'
import TournamentCreate from '../views/Admin/Tournament/TournamentCreate.vue'
import ConfigPage from '../views/ConfigPage.vue'
import SportsConfiguration from '../components/Admin/SportsConfiguration.vue'
import AdminSender from '../views/Admin/AdminSender.vue';

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
      redirect: '/tournaments' // <--- ALTERE AQUI: Redireciona direto para o lobby
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
    { 
      path: '/inbox', 
      name: 'Inbox', 
      // O caminho deve bater com a sua pasta. Pela imagem é esse:
      component: () => import('../components/UserInbox.vue'),
      meta: { layout: 'default', requiresAuth: true } 
    },

    // =======================================================
    // 🏆 2. ROTAS DE LOBBY (Layout Limpo / Tournament)
    // =======================================================
    { 
      path: '/tournaments', 
      name: 'TournamentLobby', 
      component: TournamentLobby,
      meta: { layout: 'default', requiresAuth: true }
    },

    { 
      path: '/tournaments/favorites', 
      name: 'Favorites',
      component: Favorites,
      meta: { layout: 'default', requiresAuth: true } 
    },
    {
      path: '/tournaments/list/:type', // Ex: /tournaments/list/free, /tournaments/list/featured
      name: 'TournamentList',
      component: () => import('../views/Tournament/TournamentListPage.vue'),
      props: true // Permite passar o :type como prop para o componente
    },
    
    // ✅ ROTA PAI DO TORNEIO (WRAPPER)
    { 
      path: '/tournament/:id',
      component: TournamentWrapper,
      meta: { layout: 'tournament', requiresAuth: true },
      // Redireciona para o 'play' usando o nome da rota (mais seguro)
      redirect: to => { return { name: 'TournamentPlay', params: { id: to.params.id } }}, 
      children: [
        { 
          path: 'play', 
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
        // --- CORREÇÃO AQUI ---
        {
          path: 'live/:gameId', // Caminho relativo (sem a barra inicial e sem repetir /tournament/:id)
          name: 'TournamentLiveMatchDetail',
          component: TournamentLiveMatchDetail,
          meta: { layout: 'tournament', requiresAuth: true },
          props: true
        },
        // ---------------------
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
          path: 'info',
          name: 'TournamentInfo',
          component: TournamentInfo // Usando o import do topo
        },
        { 
          path: 'ranking', 
          name: 'TournamentRanking', 
          component: TournamentRanking,
          props: true
        },
        {
          path: 'match/:gameId', 
          name: 'TournamentMatchDetail',
          component: TournamentMatchDetail,
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

        // Módulo Mensagens/Sistema
        { path: 'messages', name: 'AdminMessages', component: AdminSender },

        // Configurações
        { path: 'sportbook', name: 'SportsConfiguration', component: SportsConfiguration },
        { path: 'casino', name: 'AdminCasino', component: EmBreve },
        { path: 'config', name: 'AdminConfig', component: ConfigPage }
        
      ]
    }
  ]
})

export default router