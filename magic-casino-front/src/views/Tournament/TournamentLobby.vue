<template>
  <div class="lobby-container animate-fade-in">
    
    <div class="lobby-header">
      <div>
        <h1 class="text-3xl font-black text-white uppercase tracking-wider italic">
          <span class="text-yellow-500">Magic</span> Tournaments 🏆
        </h1>
        <p class="text-gray-400 text-sm mt-1">
          Mostre sua habilidade, suba no ranking e ganhe prêmios reais.
        </p>
      </div>
      
      <div class="balance-badge">
        <span class="text-xs text-gray-400 uppercase font-bold mr-2">Seu Saldo</span>
        <span class="text-green-400 font-mono font-bold">R$ 1.250,00</span>
      </div>
    </div>

    <div class="filters-bar">
      <button 
        v-for="filter in filters" 
        :key="filter.id"
        @click="activeFilter = filter.id"
        class="filter-pill"
        :class="{ 'active': activeFilter === filter.id }"
      >
        {{ filter.label }}
      </button>
    </div>

    <div v-if="isLoading" class="loading-container">
       <div class="loader"></div>
       <p>Buscando torneios...</p>
    </div>

    <div v-else-if="tournaments.length === 0" class="empty-state">
       <span class="text-4xl mb-2">🤷‍♂️</span>
       <h3>Nenhum torneio ativo no momento.</h3>
       <p>Fique ligado, novos rounds abrem em breve!</p>
    </div>

    <div v-else class="tournament-grid">
      <div 
        v-for="t in filteredTournaments" 
        :key="t.id" 
        class="tournament-card group"
      >
        <div class="card-sport-badge">
           {{ getSportIcon(t.sport) }}
        </div>

        <div class="card-body">
            <div class="mb-4">
                <div class="flex justify-between items-start">
                    <h3 class="card-title">{{ t.name }}</h3>
                    <span class="live-badge" v-if="isLive(t.startDate)">AO VIVO</span>
                    <span class="future-badge" v-else>EM BREVE</span>
                </div>
                <p class="card-desc">{{ t.description || 'Sem descrição extra.' }}</p>
            </div>

            <div class="info-grid">
                <div class="info-item">
                    <span class="label">Prize Pool</span>
                    <span class="value gold">R$ {{ formatCurrency(t.prizePool || 0) }}</span> </div>
                <div class="info-item text-right">
                    <span class="label">Participantes</span>
                    <span class="value text-white">{{ t.participantsCount || 0 }}<span class="text-gray-600"> Jogadores</span></span>
                </div>
            </div>

            <div class="dates-container">
                <div class="date-row">
                    <span class="icon">📅</span> 
                    <span>Início: {{ formatDate(t.startDate) }}</span>
                </div>
                <div class="date-row">
                    <span class="icon">🏁</span> 
                    <span>Fim: {{ formatDate(t.endDate) }}</span>
                </div>
            </div>
        </div>

        <div class="card-footer">
            <div class="price-section">
                <span class="text-xs text-gray-500 font-bold uppercase">Entrada</span>
                <span class="text-xl font-bold text-white">R$ {{ formatCurrency(t.entryFee) }}</span>
            </div>
            
            <button 
                @click="t.isJoined ? enterTournament(t.id) : confirmPurchase(t)" 
                class="btn-action"
                :class="t.isJoined ? 'btn-enter' : 'btn-join'"
                :disabled="processingId === t.id"
            >
                <span v-if="processingId === t.id" class="loader-xs"></span>
                <span v-else>{{ t.isJoined ? 'ENTRAR AGORA' : 'PARTICIPAR' }}</span>
            </button>
        </div>
      </div>
    </div>

  </div>
</template>

<script lang="ts">
import { defineComponent } from 'vue';
import Swal from 'sweetalert2';
import tournamentService from '../../services/Tournament/TournamentService';

export default defineComponent({
  name: 'TournamentLobby',
  data() {
    return {
      isLoading: true,
      processingId: null as number | null,
      activeFilter: 'all',
      currentUserId: '', 
      filters: [
        { id: 'all', label: 'Todos' },
        { id: 'soccer', label: '⚽ Futebol' },
        { id: 'nba', label: '🏀 Basquete' },
        { id: 'high', label: '💎 High Roller' }
      ],
      tournaments: [] as any[],
    };
  },
  computed: {
    filteredTournaments() {
        if (this.activeFilter === 'all') return this.tournaments;
        if (this.activeFilter === 'high') return this.tournaments.filter((t: any) => t.entryFee >= 100);
        return this.tournaments; 
    }
  },
  async mounted() {
      // 1. Carrega o usuário e LIMPA o CPF
      this.loadCurrentUser();
      
      // 2. Busca os torneios
      await this.loadTournaments();
  },
  methods: {
    loadCurrentUser() {
        try {
            // Tenta ler de várias chaves possíveis do login
            const storedString = localStorage.getItem('user') || localStorage.getItem('user_data') || localStorage.getItem('session');
            
            if (storedString) {
                const userData = JSON.parse(storedString);
                
                // Tenta encontrar o CPF em qualquer variação (Code, code, Cpf, cpf)
                const rawId = userData.Code || userData.code || userData.Cpf || userData.cpf || '';
                
                // 🔥 CORREÇÃO PRINCIPAL: Remove pontos e traços para bater com o banco
                this.currentUserId = String(rawId).replace(/\D/g, ''); 

                console.log("👤 USUÁRIO CARREGADO:", {
                    Raw: rawId,
                    Clean: this.currentUserId
                });
            } else {
                console.warn("⚠️ Nenhum usuário encontrado no LocalStorage");
            }
        } catch (e) {
            console.error("Erro ao ler usuário:", e);
        }
    },

    async loadTournaments() {
        this.isLoading = true;
        try {
            // Envia o CPF Limpo
            console.log("📡 Buscando torneios para o CPF:", this.currentUserId);
            
            const res = await tournamentService.listTournaments(this.currentUserId);
            
            this.tournaments = res.data || [];
            
            // Log para debug: Verifica o que o backend respondeu
            this.tournaments.forEach(t => {
                console.log(`🏆 Torneio ${t.id} (${t.name}) -> Inscrito? ${t.isJoined}`);
            });

        } catch (error) {
            console.error(error);
            this.tournaments = [];
        } finally {
            this.isLoading = false;
        }
    },

    enterTournament(id: number) {
    this.$router.push({ 
        name: 'TournamentPlay', 
        params: { id: id } 
    });
},

    getSportIcon(sport: string) {
        if (!sport) return '🏆';
        if (sport.toLowerCase().includes('soccer') || sport.toLowerCase().includes('futebol')) return '⚽';
        if (sport.toLowerCase().includes('basket')) return '🏀';
        return '🎮';
    },

    isLive(dateStr: string) {
        return new Date(dateStr) <= new Date();
    },

    formatCurrency(val: number) {
        return val.toFixed(2).replace('.', ',');
    },

    formatDate(dateStr: string) {
        const d = new Date(dateStr);
        return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' }) + ' ' + d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
    },

    async confirmPurchase(tournament: any) {
        const result = await Swal.fire({
            title: 'Confirmar Inscrição?',
            html: `
                <div style="text-align: left; background: #1f2937; padding: 15px; border-radius: 8px; color: #fff;">
                    <p style="margin:0 0 5px 0; font-size: 0.9rem; color: #9ca3af;">Torneio:</p>
                    <h3 style="margin:0 0 15px 0; color: #ffd700;">${tournament.name}</h3>
                    <div style="display:flex; justify-content:space-between; border-top: 1px solid #374151; padding-top: 10px;">
                        <span>Valor da Entrada:</span>
                        <strong style="color: #4ade80;">R$ ${this.formatCurrency(tournament.entryFee)}</strong>
                    </div>
                </div>
            `,
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'CONFIRMAR COMPRA',
            cancelButtonText: 'Cancelar',
            background: '#121214',
            color: '#fff',
            confirmButtonColor: '#10b981',
            cancelButtonColor: '#374151'
        });

        if (result.isConfirmed) {
            this.processJoin(tournament.id);
        }
    },

    async processJoin(id: number) {
        this.processingId = id;
        try {
            await tournamentService.joinTournament(id, this.currentUserId);
            
            const localTournament = this.tournaments.find(t => t.id === id);
            if (localTournament) {
                localTournament.participantsCount = (localTournament.participantsCount || 0) + 1;
                localTournament.isJoined = true; // Força visualmente
                
                if (localTournament.entryFee > 0) {
                    const feePercent = localTournament.houseFeePercent || 10;
                    const houseCut = localTournament.entryFee * (feePercent / 100);
                    const prizeAdd = localTournament.entryFee - houseCut;
                    localTournament.prizePool = (localTournament.prizePool || 0) + prizeAdd;
                }
            }

            Swal.fire({
                title: 'Inscrição Realizada!',
                text: 'Você já pode montar suas apostas.',
                icon: 'success',
                background: '#121214',
                color: '#fff',
                confirmButtonColor: '#3b82f6',
                confirmButtonText: 'IR PARA O JOGO'
            }).then(() => {
                this.enterTournament(id);
            });

        } catch (error: any) {
             Swal.fire({
                title: 'Falha na Inscrição',
                text: error.response?.data?.error || error.response?.data || 'Verifique seu saldo ou tente novamente.',
                icon: 'error',
                background: '#121214',
                color: '#fff'
            });
        } finally {
            this.processingId = null;
        }
    }
  }
});
</script>


<style scoped>
/* Container Geral */
.lobby-container {
    padding: 2rem;
    max-width: 1200px;
    margin: 0 auto;
    font-family: 'Inter', sans-serif;
    color: #f8fafc;
    min-height: 100vh;
}

/* Header */
.lobby-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
    margin-bottom: 2rem;
    padding-bottom: 1rem;
    border-bottom: 1px solid #27272a;
}

.balance-badge {
    background: #18181b;
    border: 1px solid #27272a;
    padding: 0.5rem 1rem;
    border-radius: 8px;
    display: flex;
    align-items: center;
}

/* Filtros */
.filters-bar {
    display: flex;
    gap: 10px;
    margin-bottom: 2rem;
    overflow-x: auto;
    padding-bottom: 5px;
}

.filter-pill {
    background: #18181b;
    border: 1px solid #27272a;
    color: #94a3b8;
    padding: 8px 16px;
    border-radius: 20px;
    font-size: 0.85rem;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.2s;
    white-space: nowrap;
}

.filter-pill:hover { background: #27272a; color: #fff; }
.filter-pill.active { background: #ffd700; color: #000; border-color: #ffd700; }

/* Grid de Torneios */
.tournament-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
    gap: 1.5rem;
}

/* Card Design */
.tournament-card {
    background: #121214; /* Cor escura base */
    border: 1px solid #27272a;
    border-radius: 12px;
    overflow: hidden;
    position: relative;
    transition: transform 0.2s, box-shadow 0.2s;
    display: flex;
    flex-direction: column;
}

.tournament-card:hover {
    transform: translateY(-4px);
    box-shadow: 0 10px 30px -10px rgba(0,0,0,0.5);
    border-color: #3f3f46;
}

.card-sport-badge {
    position: absolute;
    top: 10px;
    right: 10px;
    font-size: 1.5rem;
    opacity: 0.2;
    transition: 0.3s;
}
.tournament-card:hover .card-sport-badge { opacity: 1; transform: scale(1.1); }

.card-body { padding: 1.5rem; flex: 1; }

.card-title {
    font-size: 1.1rem;
    font-weight: 800;
    color: #fff;
    line-height: 1.3;
    margin: 0;
    max-width: 75%;
}

.card-desc {
    font-size: 0.8rem;
    color: #9ca3af;
    margin-top: 5px;
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
}

.live-badge { background: #ef4444; color: white; padding: 2px 6px; border-radius: 4px; font-size: 0.6rem; font-weight: 800; letter-spacing: 0.5px; }
.future-badge { background: #3b82f6; color: white; padding: 2px 6px; border-radius: 4px; font-size: 0.6rem; font-weight: 800; letter-spacing: 0.5px; }

.info-grid {
    display: flex;
    justify-content: space-between;
    margin: 1.5rem 0;
    background: #18181b;
    padding: 10px;
    border-radius: 8px;
}

.info-item { display: flex; flex-direction: column; }
.info-item .label { font-size: 0.65rem; color: #6b7280; text-transform: uppercase; font-weight: 700; margin-bottom: 2px; }
.info-item .value { font-size: 0.9rem; font-weight: 700; }
.info-item .value.gold { color: #fbbf24; }

.dates-container { font-size: 0.75rem; color: #6b7280; display: flex; flex-direction: column; gap: 4px; }

/* CSS Válido */
.date-row { 
    display: flex; 
    align-items: center; 
    gap: 6px; 
}

/* Footer do Card */
.card-footer {
    background: #18181b;
    padding: 1rem 1.5rem;
    border-top: 1px solid #27272a;
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.price-section { display: flex; flex-direction: column; }

/* ✅ Novos Estilos de Botão */
.btn-action {
    color: white;
    font-weight: 800;
    border: none;
    padding: 10px 20px;
    border-radius: 6px;
    font-size: 0.85rem;
    cursor: pointer;
    transition: 0.2s;
    letter-spacing: 0.5px;
    width: 140px; /* Largura fixa para manter alinhamento */
    display: flex;
    align-items: center;
    justify-content: center;
}

.btn-action:disabled { background: #374151; cursor: not-allowed; transform: none; box-shadow: none; }

/* Estilo COMPRAR (Verde) */
.btn-join {
    background: #10b981;
}
.btn-join:hover { background: #059669; transform: scale(1.05); }

/* Estilo ENTRAR (Azul) */
.btn-enter {
    background: #3b82f6;
    box-shadow: 0 4px 15px rgba(59, 130, 246, 0.4);
}
.btn-enter:hover { background: #2563eb; transform: scale(1.05); }

/* Estados de Carregamento */
.loading-container { text-align: center; padding: 4rem; color: #94a3b8; }
.empty-state { text-align: center; padding: 4rem; color: #52525b; border: 2px dashed #27272a; border-radius: 12px; }

.loader { border: 3px solid #3f3f46; border-top: 3px solid #ffd700; border-radius: 50%; width: 30px; height: 30px; animation: spin 1s linear infinite; margin: 0 auto 10px; }
.loader-xs { border: 2px solid rgba(255,255,255,0.3); border-top: 2px solid white; border-radius: 50%; width: 14px; height: 14px; animation: spin 1s linear infinite; display: inline-block; }

@keyframes spin { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }
.animate-fade-in { animation: fadeIn 0.5s ease-out; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
</style>