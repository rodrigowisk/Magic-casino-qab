<template>
  <div class="tournament-card">
    <h3 class="card-title">{{ tournament.name }}</h3>
    <p class="card-description">{{ tournament.description }}</p>

    <div class="card-details">
      <div class="detail-item">
        <span class="detail-label">Entrada:</span>
        <span class="detail-value price">R$ {{ tournament.entryFee.toFixed(2) }}</span>
      </div>
      <div class="detail-item">
        <span class="detail-label">Prêmio:</span>
        <span class="detail-value prize">R$ {{ tournament.prizePool?.toFixed(2) || 'A ser definido' }}</span>
      </div>
      <div class="detail-item">
        <span class="detail-label">Fichas Iniciais:</span>
        <span class="detail-value fantasy">{{ tournament.initialFantasyBalance }} pts</span>
      </div>
      <div class="detail-item">
        <span class="detail-label">Início:</span>
        <span class="detail-value date">{{ formatDate(tournament.startDate) }}</span>
      </div>
    </div>

    <button @click="viewTournament" class="btn-join">
      Ver Detalhes e Participar
    </button>
  </div>
</template>

<script lang="ts">
// ✅ CORREÇÃO 1: 'type PropType' é obrigatório no modo estrito do Docker
import { defineComponent, type PropType } from 'vue';

// ✅ CORREÇÃO 2: Caminho relativo seguro
import type { Tournament } from '../../../models/Tournament/Tournament';

export default defineComponent({
  name: 'TournamentCard',
  props: {
    tournament: {
      type: Object as PropType<Tournament>,
      required: true
    }
  },
  methods: {
    formatDate(dateStr: string): string {
      if (!dateStr) return '-';
      return new Date(dateStr).toLocaleString('pt-BR', {
        day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
      });
    },
    viewTournament() {
      console.log("Ver torneio", this.tournament.id);
    }
  }
});
</script>

<style scoped>
.tournament-card {
  background: linear-gradient(145deg, #2a2a2a, #1f1f1f);
  border: 1px solid #333;
  border-radius: 12px;
  padding: 20px;
  color: white;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.3);
  transition: transform 0.2s ease-in-out, box-shadow 0.2s ease-in-out;
}

.tournament-card:hover {
  transform: translateY(-5px);
  box-shadow: 0 12px 24px rgba(0, 0, 0, 0.6);
}

.card-title {
  color: #ffd700; /* Dourado */
  font-size: 1.6rem;
  margin-bottom: 10px;
  text-align: center;
}

.card-description {
  font-size: 0.95rem;
  color: #bbb;
  margin-bottom: 20px;
  line-height: 1.5;
}

.card-details {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  margin-bottom: 25px;
}

.detail-item {
  width: 48%; /* Duas colunas */
  margin-bottom: 10px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.detail-label {
  font-size: 0.85rem;
  color: #999;
  font-weight: bold;
}

.detail-value {
  font-weight: bold;
  font-size: 0.95rem;
}

.detail-value.price { color: #4caf50; }
.detail-value.fantasy { color: #2196f3; }
.detail-value.date { color: #ff9800; font-size: 0.85rem; }

.btn-join {
  width: 100%;
  background: #ffd700;
  color: #000;
  border: none;
  padding: 12px;
  border-radius: 6px;
  font-weight: bold;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-join:hover {
  background: #e6c200;
}
</style>