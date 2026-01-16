<template>
  <div class="admin-container">
    <div class="header-actions">
      <h2>📋 Torneios Ativos</h2>
      <router-link to="/admin/tournaments/create" class="btn-new">
        + Criar Torneio
      </router-link>
    </div>

    <table class="admin-table">
      <thead>
        <tr>
          <th>Nome</th>
          <th>Entrada</th>
          <th>Saldo Inicial</th>
          <th>Início</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="t in tournaments" :key="t.id">
          <td>{{ t.name }}</td>
          <td>R$ {{ t.entryFee.toFixed(2) }}</td>
          <td>{{ t.initialFantasyBalance }} pts</td>
          <td>{{ formatDate(t.startDate) }}</td>
          <td>
             <span :class="t.isActive ? 'badge-active' : 'badge-off'">
                {{ t.isActive ? 'Ativo' : 'Inativo' }}
             </span>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script lang="ts">
import { defineComponent } from 'vue';

// ✅ Caminhos relativos para evitar erro no Docker
import tournamentService from "../../../services/Tournament/TournamentService";
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
      return new Date(dateStr).toLocaleString('pt-BR');
    }
  }
});
</script>

<style scoped>
.admin-container { padding: 20px; color: #fff; }
.header-actions { display: flex; justify-content: space-between; margin-bottom: 20px; }
.admin-table { width: 100%; border-collapse: collapse; background: #2a2a3a; border-radius: 8px; overflow: hidden; }
th, td { padding: 12px; text-align: left; border-bottom: 1px solid #3a3a4a; color: white; }
th { background: #1a1a2a; }
.btn-new { background: #ffd700; color: black; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-weight: bold; }
.badge-active { color: #4CAF50; font-weight: bold; }
.badge-off { color: #f44336; }
</style>