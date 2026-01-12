<template>
  <div class="flex items-center gap-4 py-4">
    <h2 class="text-xl font-bold uppercase text-white"># Soccer</h2>
    
    <div class="flex gap-2 overflow-x-auto pb-2 custom-scrollbar">
      <button 
        v-for="filtro in filtros" 
        :key="filtro.valor"
        @click="selecionarData(filtro.valor)"
        type="button"
        :class="[
          'px-4 py-1 rounded text-sm transition-colors whitespace-nowrap',
          dataSelecionada === filtro.valor 
            ? 'bg-blue-600 text-white font-bold' 
            : 'bg-gray-800 text-gray-400 hover:bg-gray-700'
        ]"
      >
        {{ filtro.label }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';

// ✅ Interface reforçada para garantir que valor nunca seja undefined
interface FiltroData {
  label: string;
  valor: string; // Formato YYYY-MM-DD ou 'all'
}

// ✅ Tipagem explícita dos Emits para evitar erro TS2322 no componente pai
const emit = defineEmits<{
  (e: 'filtrar', valor: string): void
}>();

const dataSelecionada = ref<string>('all');

const filtros = computed<FiltroData[]>(() => {
  const lista: FiltroData[] = [{ label: 'TODAS', valor: 'all' }];
  const hoje = new Date();

  for (let i = 0; i < 5; i++) {
    const dataRef = new Date();
    dataRef.setDate(hoje.getDate() + i);

    let label = '';
    if (i === 0) {
      label = 'HOJE';
    } else if (i === 1) {
      label = 'AMANHÃ';
    } else {
      const nomeDia = dataRef.toLocaleDateString('pt-BR', { weekday: 'long' });
      // Garante que o split nunca retorne undefined para o TypeScript
      const diaFormatado = nomeDia.split('-')[0] || nomeDia;
      label = diaFormatado.charAt(0).toUpperCase() + diaFormatado.slice(1).toUpperCase();
    }

    // ✅ Correção de segurança: Garante que valor seja sempre string
    const isoString = dataRef.toISOString();
    const valor = isoString.split('T')[0] || '';
    
    lista.push({ label, valor });
  }
  return lista;
});

const selecionarData = (valor: string): void => {
  dataSelecionada.value = valor;
  emit('filtrar', valor);
};
</script>