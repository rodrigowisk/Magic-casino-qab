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
  const selections = ref<Selection[]>([]);

  // Carrega do LocalStorage ao iniciar
  if (localStorage.getItem('betSlip')) {
    try {
      const stored = localStorage.getItem('betSlip');
      if (stored) {
        selections.value = JSON.parse(stored);
      }
    } catch (e) {
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
    
    // ✅ CORREÇÃO TS2532: Verifica explicitamente se o índice existe
    if (existingIndex !== -1 && selections.value[existingIndex]) {
      // Se já existe EXATAMENTE igual, não faz nada
      if (selections.value[existingIndex].type === type) {
        return; 
      }
      // Se é diferente, substitui
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
      // Adiciona nova
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

  const removeSelection = (id: string) => {
    selections.value = selections.value.filter(s => s.id !== id);
    saveToStorage();
  };

  const clearStore = () => {
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