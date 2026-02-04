<template>
  <div class="admin-container animate-fade-in">
    <div class="header-actions">
      <div class="title-section">
        <h2 class="main-title">📋 Torneios Ativos</h2>
        <p class="subtitle">Gerencie as competições em andamento</p>
      </div>
      <router-link to="/admin/tournaments/create" class="btn-new">
        + Novo Torneio
      </router-link>
    </div>

    <div class="table-card shadow-lg">
      <table class="admin-table">
        <thead>
          <tr>
            <th>Nome</th>
            <th>Entrada</th>
            <th>Saldo Inicial</th>
            <th>Início</th>
            <th>Status</th>
            <th class="text-right">Ações</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="t in tournaments" :key="t.id" class="table-row">
            <td class="font-bold text-white">{{ t.name }}</td>
            <td class="text-green-400">R$ {{ t.entryFee.toFixed(2) }}</td>
            <td class="text-blue-400">{{ t.initialFantasyBalance }} pts</td>
            <td class="text-gray-400 text-sm">{{ formatDate(t.startDate) }}</td>
            <td>
               <span :class="t.isActive ? 'badge-active' : 'badge-off'">
                  {{ t.isActive ? 'Ativo' : 'Inativo' }}
               </span>
            </td>
            <td class="text-right">
              <button class="btn-action">Editar</button>
            </td>
          </tr>
          
          <tr v-if="tournaments.length === 0">
            <td colspan="6" class="empty-state">
              Nenhum torneio encontrado.
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script lang="ts">
import { defineComponent } from 'vue';
import tournamentService from "../../../services/Tournament/TournamentService";
// Certifique-se que o caminho da Model está correto
import type { Tournament } from "../../../models/Tournament/Tournament"; 

export default defineComponent({
  name: 'TournamentAdminList',
  data() {
    return {
      tournaments: [] as Tournament[]
    };
  },
  async mounted() {
    try {
      const response = await tournamentService.listTournaments();
      this.tournaments = response.data;
    } catch (error) {
      console.error("Erro ao carregar lista", error);
    }
  },
  methods: {
    formatDate(dateStr: string): string {
      if (!dateStr) return '-';
      return new Date(dateStr).toLocaleString('pt-BR', {
        day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
      });
    }
  }
});
</script>

<style scoped>
/* Layout Base */
.admin-container { 
  padding: 1.5rem; 
  color: #f8fafc; 
  max-width: 1200px; 
  margin: 0 auto; 
  font-family: 'Inter', sans-serif;
}

.header-actions { 
  display: flex; 
  justify-content: space-between; 
  align-items: center; 
  margin-bottom: 1.5rem; 
}

.main-title { font-size: 1.25rem; font-weight: 700; color: #ffd700; margin: 0; }
.subtitle { font-size: 0.8rem; color: #94a3b8; margin: 4px 0 0 0; }

/* Botão Novo */
.btn-new { 
  background: #ffd700; 
  color: #0f172a; 
  padding: 0.6rem 1.2rem; 
  text-decoration: none; 
  border-radius: 6px; 
  font-weight: 800; 
  font-size: 0.85rem;
  text-transform: uppercase;
  transition: all 0.2s;
  box-shadow: 0 4px 6px -1px rgba(255, 215, 0, 0.2);
}
.btn-new:hover { background: #fbbf24; transform: translateY(-2px); }

/* Tabela */
.table-card {
  background: #1e293b;
  border-radius: 8px;
  border: 1px solid #334155;
  overflow: hidden; /* Arredonda a tabela */
}

.admin-table { 
  width: 100%; 
  border-collapse: collapse; 
  text-align: left; 
}

th { 
  background: #0f172a; 
  padding: 1rem; 
  font-size: 0.75rem; 
  text-transform: uppercase; 
  color: #64748b; 
  font-weight: 700;
  border-bottom: 1px solid #334155;
}

td { 
  padding: 1rem; 
  border-bottom: 1px solid #334155; 
  color: #e2e8f0; 
  font-size: 0.9rem;
}

.table-row:hover { background: #2a3441; }
.table-row:last-child td { border-bottom: none; }

/* Badges e Cores */
.badge-active { 
  background: rgba(74, 222, 128, 0.1); 
  color: #4ade80; 
  padding: 4px 8px; 
  border-radius: 4px; 
  font-size: 0.75rem; 
  font-weight: 700; 
  border: 1px solid rgba(74, 222, 128, 0.2);
}

.badge-off { 
  background: rgba(248, 113, 113, 0.1); 
  color: #f87171; 
  padding: 4px 8px; 
  border-radius: 4px; 
  font-size: 0.75rem; 
  font-weight: 700;
  border: 1px solid rgba(248, 113, 113, 0.2);
}

.btn-action {
  background: transparent;
  border: 1px solid #475569;
  color: #94a3b8;
  padding: 4px 10px;
  border-radius: 4px;
  font-size: 0.75rem;
  cursor: pointer;
  transition: all 0.2s;
}
.btn-action:hover { border-color: #cbd5e1; color: #fff; }

.empty-state { text-align: center; padding: 3rem; color: #64748b; font-style: italic; }
.text-right { text-align: right; }

.animate-fade-in { animation: fadeIn 0.4s ease-out; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
</style>