import { defineStore } from 'pinia';
import { ref, computed } from 'vue';

// ✅ DEFINIÇÃO DO TIPO EXPORTADA
export type BetType = '1' | 'X' | '2';

export interface Selection {
  id: string;
  homeTeam: string;
  awayTeam: string;
  selection: string;
  odds: number;
  type: BetType;
  marketName?: string;
  commenceTime: string;
}

export const useBetStore = defineStore('bet', () => {
  // 🔥 LOG PARA CONFIRMAR ATUALIZAÇÃO NO CONSOLE
  console.log("✅ [DEBUG V3.5 FINAL] useBetStore inicializada com sucesso!");

  const selections = ref<Selection[]>([]);

  // Carrega do LocalStorage ao iniciar
  if (localStorage.getItem('betSlip')) {
    try {
      const stored = localStorage.getItem('betSlip');
      if (stored) {
        const parsed = JSON.parse(stored);
        if (Array.isArray(parsed)) {
          selections.value = parsed as Selection[];
        } else {
          selections.value = [];
        }
      }
    } catch (e) {
      console.error("[DEBUG V3] Erro ao carregar localStorage:", e);
      selections.value = [];
    }
  }

  const count = computed(() => selections.value.length);

  const totalOdds = computed(() => {
    if (selections.value.length === 0) return 0;
    return selections.value.reduce((acc, curr) => acc * curr.odds, 1);
  });

  const saveToStorage = () => {
    localStorage.setItem('betSlip', JSON.stringify(selections.value));
  };

  const addOrReplaceSelection = (
    id: string,
    homeTeam: string,
    awayTeam: string,
    selection: string,
    odds: number,
    type: BetType,
    commenceTime: string
  ) => {
    const existingIndex = selections.value.findIndex(s => s.id === id);

    if (existingIndex !== -1 && selections.value[existingIndex]) {
      if (selections.value[existingIndex].type === type) {
        return;
      }
      selections.value[existingIndex] = {
        id,
        homeTeam,
        awayTeam,
        selection,
        odds,
        type,
        commenceTime,
        marketName: 'Resultado Final'
      };
    } else {
      selections.value.push({
        id,
        homeTeam,
        awayTeam,
        selection,
        odds,
        type,
        commenceTime,
        marketName: 'Resultado Final'
      });
    }

    saveToStorage();
  };

  // ✅ CORREÇÃO DEFINITIVA: 
  // Alterado de `id?: string` para `id: string`.
  // Isso força quem chama a função a garantir que tem uma string, 
  // eliminando o erro TS2345 de "undefined is not assignable to string".
  const removeSelection = (id: string) => {
    if (!id) {
        console.warn("⚠️ [DEBUG V3] Tentativa de remover ID nulo/undefined cancelada.");
        return;
    }
    // Garante que a comparação é feita com string pura
    selections.value = selections.value.filter(s => String(s.id) !== String(id));
    saveToStorage();
  };

  const clearStore = () => {
    console.log("🧹 [DEBUG V3] Store: limpando tudo.");
    selections.value = [];
    saveToStorage();
  };

  return {
    selections,
    count,
    totalOdds,
    addOrReplaceSelection,
    removeSelection,
    clearStore
  };
});