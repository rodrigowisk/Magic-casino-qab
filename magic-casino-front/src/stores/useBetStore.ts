import { defineStore } from 'pinia';
import { ref, computed } from 'vue';

export type BetType = '1' | 'X' | '2';

export interface BetSelection {
  id: string;
  homeTeam: string;
  awayTeam: string;
  selection: string;
  odds: number;
  type: BetType;
  marketName: string;
  // ✅ ADICIONADO: Campo para armazenar a data do jogo
  commenceTime?: string; 
}

export const useBetStore = defineStore('bet', () => {
  // --- Estado ---
  const selections = ref<BetSelection[]>([]);
  const isVisible = ref(false); 

  // --- Computeds ---
  const count = computed(() => selections.value.length);
  const totalOdds = computed(() => {
    if (selections.value.length === 0) return 0;
    return selections.value.reduce((acc, item) => acc * (item.odds || 1), 1);
  });

  // --- Actions ---
  // ✅ ATUALIZADO: Agora aceita o parâmetro commenceTime
  function addOrReplaceSelection(
    id: string, 
    homeTeam: string, 
    awayTeam: string, 
    selectionName: string, 
    odds: number, 
    type: BetType,
    commenceTime?: string
  ) {
    const index = selections.value.findIndex(s => s.id === id);
    
    const newItem: BetSelection = {
      id, 
      homeTeam, 
      awayTeam, 
      selection: selectionName, 
      odds, 
      type, 
      marketName: 'Resultado Final',
      commenceTime // ✅ Salvando a data no objeto
    };

    if (index !== -1) {
      selections.value[index] = newItem;
    } else {
      selections.value.push(newItem);
    }
    
    isVisible.value = true; 
  }

  function removeSelection(id: string) {
    const index = selections.value.findIndex(s => s.id === id);
    if (index !== -1) selections.value.splice(index, 1);
  }

  function clearStore() {
    selections.value = [];
    isVisible.value = false; 
  }
  
  function toggleBetSlip() {
    isVisible.value = !isVisible.value;
  }

  return {
    selections, count, totalOdds, isVisible,
    addOrReplaceSelection, removeSelection, clearStore, toggleBetSlip
  };
});