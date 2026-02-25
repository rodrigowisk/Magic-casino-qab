<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'; 
import { useRouter, useRoute } from 'vue-router';
import Swal from 'sweetalert2';

import PageLoader from '../../components/PageLoader.vue';
import { usePageLoader } from '../../composables/usePageLoader';
import tournamentService from '../../services/Tournament/TournamentService';
// 👇 Importação do Serviço SignalR (Singleton)
import tournamentSignal from '../../services/Tournament/TournamentSignalService';
import { useAuthStore } from '../../stores/useAuthStore';

// Componentes
import BannerCarousel from '../../components/Tournament/BannerCarousel.vue';
import TournamentCarousel from '../../components/Tournament/TournamentCarousel.vue';
import TournamentFilters from '../../components/Tournament/TournamentFilters.vue';
import TournamentInfoModal from '../../components/Tournament/TournamentInfoModal.vue';

// --- TIPAGENS (TypeScript) ---
interface Tournament {
  id: number;
  sport?: string;
  entryFee: number;
  isJoined?: boolean;
  participantsCount?: number;
  houseFeePercent?: number;
  prizePool?: number;
  isFinished?: boolean;
  isActive?: boolean;
  isFavorite?: boolean;
  category?: string;
  Category?: string;
  name?: string;
  description?: string;
  startDate?: string;
  maxParticipants?: number;
  status?: string; 
  fixedPrize?: number;
  [key: string]: any;
}

// --- SETUP ---
const router = useRouter();
const route = useRoute(); // 🔥 DECLARAMOS O ROUTE PARA LER A URL 🔥
const authStore = useAuthStore();
const { isLoading, loadingProgress, isContentReady, startLoader, finishLoader } = usePageLoader();

// --- ESTADOS ---
const processingId = ref<number | null>(null); // ID do torneio sendo processado (Loading)
const activeFilter = ref<string>('all');
const currentUserId = ref<string>('');
const tournaments = ref<Tournament[]>([]);

// Estados do Modal de Info
const showInfoModal = ref(false);
const selectedTournament = ref<Tournament | null>(null);

// ✅ CORREÇÃO: Monitora Login/Logout para atualizar a lista em tempo real
watch(() => authStore.user, async (newUser) => {
    // 1. Atualiza o ID do usuário local
    if (newUser) {
        currentUserId.value = extractUserId(newUser);
    } else {
        currentUserId.value = '';
    }

    // 2. Recarrega os torneios. 
    await loadTournaments();
});


// --- LÓGICA SIGNALR (USANDO O SERVIÇO) ---
// Função que processa o dado recebido do SignalR (Lista/Lobby)
const handleRealTimeUpdate = (data: any) => {
    // Se for um comando de remoção explícita
    if (data.status === 'Deleted' || data.deletedId) {
        const idToRemove = data.id || data.deletedId;
        tournaments.value = tournaments.value.filter(t => t.id !== idToRemove);
        return;
    }

    const updatedTournament = data as Tournament;
    const index = tournaments.value.findIndex(t => t.id === updatedTournament.id);
    
    // Checa se o status indica que o torneio deve sair da lista (Cancelado/Finalizado/Inativo)
    const isFinishedStatus = updatedTournament.status === 'Cancelled' || 
                             updatedTournament.status === 'Finished' || 
                             updatedTournament.isActive === false;

    if (index !== -1) {
        // --- JÁ EXISTE NA LISTA ---
        if (isFinishedStatus) {
            // Se virou finalizado, remove
            tournaments.value.splice(index, 1);
        } else {
            // Se continua ativo, atualiza os dados preservando o objeto local
            tournaments.value[index] = { ...tournaments.value[index], ...updatedTournament };
        }
    } else {
        // --- NÃO EXISTE (É NOVO) ---
        if (!isFinishedStatus) {
            // Adiciona no topo da lista
            tournaments.value.unshift(updatedTournament);
        }
    }
};

// --- LIFECYCLE ---
onMounted(async () => {
  loadCurrentUser();
  
  // 1. Carrega a lista inicial via API REST
  await loadTournaments();

  // 2. Configura o "Ouvinte" no serviço compartilhado (Lobby Updates)
  tournamentSignal.setTournamentListListener((data: any) => {
      console.log('🔔 Lobby recebeu update:', data);
      handleRealTimeUpdate(data);
  });

  // 3. Configura o "Ouvinte" específico para a mudança de inscritos (Contador)
  tournamentSignal.setParticipantCountListener((tournamentId: number, count: number) => {
      const tournament = tournaments.value.find(t => t.id === tournamentId);
      if (tournament) {
          // Descobre quantos jogadores novos entraram via SignalR
          const addedPlayers = count - (tournament.participantsCount || 0);
          
          // Atualiza o número de inscritos no card
          tournament.participantsCount = count;
          
          // Se entrou gente nova, o torneio cobra entrada, e NÃO tem prêmio fixo
          if (addedPlayers > 0 && tournament.entryFee > 0 && !tournament.fixedPrize) {
              const feePercent = tournament.houseFeePercent || 10; // Padrão 10%
              const houseCut = tournament.entryFee * (feePercent / 100);
              const prizeToAdd = (tournament.entryFee - houseCut) * addedPlayers;
              
              // Atualiza o prêmio, e o Vue injeta no Card automaticamente!
              tournament.prizePool = (tournament.prizePool || 0) + prizeToAdd;
          }
      }
  });

  // ✅ 4. Configura o Ouvinte do Resultado da Inscrição (RabbitMQ)
  // Serve apenas para tratamento de erro tardio, já que o fluxo principal é otimista
  if (tournamentSignal.setJoinResultListener) {
      tournamentSignal.setJoinResultListener((data: any) => {
          console.log('📨 Resposta da Inscrição (RabbitMQ):', data);
          
          // Se houver erro E o botão ainda estiver travado (o que não deve acontecer no fluxo novo)
          if ((data.Status !== 'Success' && data.status !== 'Success') && processingId.value) {
                showAlert('Atenção', data.Message || 'Erro no processamento da fila.', 'error');
                processingId.value = null;
          }
      });
  }

  // 5. Inicia a conexão
  await tournamentSignal.start();

  // 6. Fala para o backend que este usuário está na tela do Lobby
  await tournamentSignal.joinLobby();
});

onUnmounted(async () => {
  // Sai do Lobby antes de desconectar ou mudar de página
  await tournamentSignal.leaveLobby();
  await tournamentSignal.stop();
});

// --- HELPER DE NORMALIZAÇÃO DE ESPORTE ---
const normalizeSport = (sport?: string) => {
  if (!sport) return '';
  return String(sport).toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").trim();
};

// --- COMPUTEDS (LISTAS ESPECÍFICAS) ---

// 1. Torneios em Destaque
const featuredTournamentsList = computed(() => {
  return tournaments.value.filter(t => {
    const cat = (t.category || t.Category || '').toLowerCase();
    return (cat.includes('destaque')) && !isItemFinished(t);
  });
});

// 2. Torneios Disponíveis
const allActiveTournamentsList = computed(() => {
  return tournaments.value.filter(t => !isItemFinished(t));
});

// 3. Torneios Grátis
const freeTournamentsList = computed(() => {
  return tournaments.value.filter(t => t.entryFee === 0 && !isItemFinished(t));
});

// 4. Torneios que Participo
const myJoinedTournamentsList = computed(() => {
  return tournaments.value.filter(t => t.isJoined && !isItemFinished(t));
});

// 🔥 NOVOS CARROSSÉIS POR ESPORTE 🔥

// 5. Torneios de Futebol
const soccerTournamentsList = computed(() => {
  return tournaments.value.filter(t => {
    if (isItemFinished(t)) return false;
    const s = normalizeSport(t.sport);
    return s.includes('futebol') || s.includes('soccer');
  });
});

// 6. Torneios de Basquete
const basketballTournamentsList = computed(() => {
  return tournaments.value.filter(t => {
    if (isItemFinished(t)) return false;
    const s = normalizeSport(t.sport);
    return s.includes('basket') || s.includes('basquete');
  });
});

// 7. Torneios de Tênis
const tennisTournamentsList = computed(() => {
  return tournaments.value.filter(t => {
    if (isItemFinished(t)) return false;
    const s = normalizeSport(t.sport);
    return s.includes('tenis') || s.includes('tennis');
  });
});

// 8. Torneios Mistos
const mixedTournamentsList = computed(() => {
  return tournaments.value.filter(t => {
    if (isItemFinished(t)) return false;
    const s = normalizeSport(t.sport);
    return s.includes('misto') || s.includes('mix');
  });
});

// --- COMPUTED (FILTRAGEM GERAL PARA AS ABAS E VER TODOS) ---
const filteredTournaments = computed(() => {
  const list = tournaments.value.filter(t => !isItemFinished(t)); 
  
  switch (activeFilter.value) {
    case 'featured': 
      return list.filter(t => {
        const cat = (t.category || t.Category || '').toLowerCase();
        return cat.includes('destaque');
      });
      
    case 'favorites': 
      return list.filter(t => t.isFavorite === true);
      
    case 'free': 
      return list.filter(t => t.entryFee === 0);

    case 'soccer':
      return list.filter(t => {
        const s = normalizeSport(t.sport);
        return s.includes('futebol') || s.includes('soccer');
      });

    // Corrigido de 'nba' para 'basketball' para casar com o viewAllType
    case 'basketball':
      return list.filter(t => {
        const s = normalizeSport(t.sport);
        return s.includes('basket') || s.includes('basquete');
      });
      
    // Adicionado Tênis que estava faltando
    case 'tennis':
      return list.filter(t => {
        const s = normalizeSport(t.sport);
        return s.includes('tenis') || s.includes('tennis');
      });

    // Adicionado Misto que estava faltando
    case 'mixed':
      return list.filter(t => {
        const s = normalizeSport(t.sport);
        return s.includes('misto') || s.includes('mix');
      });
      
    // Adicionado Que Participo (mine) que estava faltando
    case 'mine':
      return list.filter(t => t.isJoined);

    case 'all': 
    default:
      return list; 
  }
});


// --- MÉTODOS AUXILIARES ---

const isItemFinished = (t: Tournament) => {
    if (t.isFinished === true) return true;
    if (String(t.status || '').toUpperCase() === 'FINISHED') return true;
    if (t.isActive === false) return true; 
    return false;
};

const extractUserId = (userData: any): string => {
  if (!userData) return '';
  const rawId = userData.cpf || userData.Cpf || userData.code || userData.Code || '';
  return String(rawId).replace(/\D/g, ''); 
};

const showAlert = (title: string, text: string, icon: 'warning' | 'error' | 'success') => {
  Swal.fire({ title, text, icon, background: '#0f172a', color: '#fff' });
};

// 👇 ESCUTA O EVENTO DO CORAÇÃO E ATUALIZA A TELA NA HORA
const handleFavoriteToggle = (payload: { id: number, isFavorite: boolean }) => {
    const t = tournaments.value.find(x => x.id === payload.id);
    if (t) {
        t.isFavorite = payload.isFavorite;
    }
};

// --- MÉTODOS DE MODAL ---
const openInfoModal = (tournament: Tournament) => {
    selectedTournament.value = tournament;
    showInfoModal.value = true;
};

const closeInfoModal = () => {
    showInfoModal.value = false;
    setTimeout(() => { selectedTournament.value = null; }, 300); // Limpa após animação
};

const handleJoinFromModal = (id: number) => {
    closeInfoModal();
    // Pequeno delay para a UI fluir melhor
    setTimeout(() => {
        processJoin(id);
    }, 100);
};

// --- MÉTODOS DE NEGÓCIO ---
const loadCurrentUser = () => {
  if (authStore.user) {
    currentUserId.value = extractUserId(authStore.user);
    return;
  } 

  try {
    const storedString = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
    if (storedString) {
      currentUserId.value = extractUserId(JSON.parse(storedString));
    }
  } catch (error) { 
    console.error("Erro ao analisar dados do usuário no localStorage:", error); 
  }
};

const loadTournaments = async () => {
  startLoader();
  try {
    const res = await tournamentService.listTournaments(currentUserId.value);
    tournaments.value = res.data || [];

    // 🔥 AQUI ESTÁ A LÓGICA DE ABRIR O MODAL PELO LINK COMPARTILHADO 🔥
    if (route.query.showInfo) {
        const sharedId = Number(route.query.showInfo);
        const sharedTourney = tournaments.value.find(t => t.id === sharedId);
        
        if (sharedTourney) {
            openInfoModal(sharedTourney);
            
            // Limpa o parâmetro da URL suavemente (sem dar reload na página)
            // Assim, se o usuário der F5 depois, o modal não abre sozinho de novo.
            router.replace({ query: undefined }); 
        }
    }

  } catch (error) {
    console.error("Erro ao buscar torneios:", error);
    tournaments.value = [];
  } finally {
    finishLoader();
  }
};

const enterTournament = (id: number) => {
  router.push({ name: 'TournamentPlay', params: { id } });
};

const updateLocalTournamentState = (id: number) => {
  const localTournament = tournaments.value.find(t => t.id === id);
  if (!localTournament) return;

  // Atualiza apenas o estado visual de 'Inscrito'
  localTournament.isJoined = true;
};

// ✅ MÉTODO DE INSCRIÇÃO CORRIGIDO (Otimista + Seguro)
// ✅ MÉTODO DE INSCRIÇÃO CORRIGIDO (Otimista + Seguro - Sem Redirecionar)
const processJoin = async (id: number) => {
  if (!authStore.isAuthenticated && !currentUserId.value) {
    showAlert('Login Necessário', 'Por favor, entre na sua conta para participar.', 'warning');
    return;
  }

  // 1. Ativa o Loading no botão (Feedback visual imediato)
  processingId.value = id;
  
  try {
    const userName = authStore.user?.name || authStore.user?.Name || 'Jogador';
    const userAvatar = authStore.user?.avatar || authStore.user?.Avatar || ''; 

    // 2. Envia para o Backend (HTTP)
    await tournamentService.joinTournament(id, currentUserId.value, userName, userAvatar);
    
    // -----------------------------------------------------------
    // 🚀 SUCESSO DO ENVIO: UI OTIMISTA
    // -----------------------------------------------------------
    
    // 3. Atualiza estado local (Card fica verde/Inscrito)
    updateLocalTournamentState(id);
    
    // Mantive o Toast de sucesso (além do efeito visual que colocamos no card)
    const Toast = Swal.mixin({
        toast: true, position: 'top-end', showConfirmButton: false, timer: 1500, background: '#0f172a', color: '#fff'
    });
    Toast.fire({ icon: 'success', title: 'Inscrição Confirmada!' });

    // 🔥 AQUI ESTAVA O SEGREDO: Removi o enterTournament(id); 
    // Agora ele permanece no lobby!

  } catch (error: any) {
    // --- TRATAMENTO DE ERRO ---
    const errorData = error.response?.data;
    const errorMsg = (typeof errorData === 'string' ? errorData : errorData?.error || errorData?.message || '').toLowerCase();
    
    // Edge Case: Se a API disser "Usuário já inscrito" (erro 400)
    if (errorMsg.includes('já inscrito') || errorMsg.includes('already joined')) {
      updateLocalTournamentState(id);
      // 🔥 AQUI TAMBÉM: Removi o enterTournament(id); para ele não pular de tela à força.
    } else {
      // Erro real (ex: Saldo insuficiente)
      showAlert('Atenção', errorData?.message || 'Não foi possível enviar a solicitação.', 'error');
    }
  } finally {
    // Destrava o botão sempre
    processingId.value = null;
  }
};
</script>

<template>
  <div class="px-3 pb-3 pt-1 md:px-6 md:pb-6 md:pt-2 bg-transparent min-h-full overflow-x-hidden">
    
    <TournamentInfoModal 
        :show="showInfoModal"
        :tournament="selectedTournament"
        @close="closeInfoModal"
        @join="handleJoinFromModal"
    />

    <PageLoader 
        :is-loading="isLoading" 
        :progress="loadingProgress" 
        loading-text="Carregando Torneios..." 
    />

    <div class="transition-opacity duration-700 w-full" :class="isContentReady ? 'opacity-100' : 'opacity-0'">
        
        <BannerCarousel />

        <div v-if="activeFilter === 'all' && featuredTournamentsList.length > 0" class="mt-4 md:mt-6">
            <TournamentCarousel 
                title="Torneios em Destaque"
                :tournaments="featuredTournamentsList"
                :processingId="processingId"
                viewAllType="featured" 
                @join="processJoin"
                @enter="enterTournament"
                @info="openInfoModal" 
                @favorite="handleFavoriteToggle"
            />
        </div>

        <div class="my-4">
            <TournamentFilters v-model="activeFilter" />
        </div>

        <div v-if="activeFilter === 'all'" class="space-y-3 md:space-y-8">
            
            <TournamentCarousel 
                title="Torneios Disponíveis"
                :tournaments="allActiveTournamentsList"
                :processingId="processingId"
                viewAllType="all"
                @join="processJoin"
                @enter="enterTournament"
                @info="openInfoModal"
                @favorite="handleFavoriteToggle"
            />

            <TournamentCarousel 
                v-if="freeTournamentsList.length > 0"
                title="Torneios Grátis"
                :tournaments="freeTournamentsList"
                :processingId="processingId"
                viewAllType="free"
                @join="processJoin"
                @enter="enterTournament"
                @info="openInfoModal"
                @favorite="handleFavoriteToggle"
            />

            <TournamentCarousel 
                v-if="soccerTournamentsList.length > 0"
                title="Torneios de Futebol"
                :tournaments="soccerTournamentsList"
                :processingId="processingId"
                viewAllType="soccer"
                @join="processJoin"
                @enter="enterTournament"
                @info="openInfoModal"
                @favorite="handleFavoriteToggle"
            />

            <TournamentCarousel 
                v-if="basketballTournamentsList.length > 0"
                title="Torneios de Basquete"
                :tournaments="basketballTournamentsList"
                :processingId="processingId"
                viewAllType="basketball"
                @join="processJoin"
                @enter="enterTournament"
                @info="openInfoModal"
                @favorite="handleFavoriteToggle"
            />

            <TournamentCarousel 
                v-if="tennisTournamentsList.length > 0"
                title="Torneios de Tênis"
                :tournaments="tennisTournamentsList"
                :processingId="processingId"
                viewAllType="tennis"
                @join="processJoin"
                @enter="enterTournament"
                @info="openInfoModal"
                @favorite="handleFavoriteToggle"
            />

            <TournamentCarousel 
                v-if="mixedTournamentsList.length > 0"
                title="Torneios Mistos"
                :tournaments="mixedTournamentsList"
                :processingId="processingId"
                viewAllType="mixed"
                @join="processJoin"
                @enter="enterTournament"
                @info="openInfoModal"
                @favorite="handleFavoriteToggle"
            />
            <TournamentCarousel 
                v-if="myJoinedTournamentsList.length > 0"
                title="Torneios que Participo"
                :tournaments="myJoinedTournamentsList"
                :processingId="processingId"
                viewAllType="mine"
                @join="processJoin"
                @enter="enterTournament"
                @info="openInfoModal"
                @favorite="handleFavoriteToggle"
            />
        </div>

        <div v-else>
            <TournamentCarousel 
                :title="activeFilter === 'favorites' ? 'Meus Favoritos' : 
                        activeFilter === 'free' ? 'Torneios Grátis' : 
                        activeFilter === 'featured' ? 'Em Destaque' : 
                        'Torneios Filtrados'"
                :tournaments="filteredTournaments"
                :processingId="processingId"
                @join="processJoin"
                @enter="enterTournament"
                @info="openInfoModal"
                @favorite="handleFavoriteToggle"
            />
        </div>

    </div>
  </div>
</template>

<style scoped>
.scrollbar-hide::-webkit-scrollbar { 
    display: none; 
}
.scrollbar-hide { 
    -ms-overflow-style: none; 
    scrollbar-width: none; 
}
</style>