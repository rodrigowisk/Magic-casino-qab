<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  games: any[];
  modelValue: string;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
}>();

const filtros = computed(() => {
  const lista = [{ label: 'TODAS', valor: 'all' }];
  
  if (!props.games || props.games.length === 0) return lista;

  const datasUnicas = new Set<string>();
  
  // Define "HOJE" zerando as horas para comparação justa
  const hoje = new Date();
  hoje.setHours(0, 0, 0, 0);

  const amanha = new Date(hoje);
  amanha.setDate(amanha.getDate() + 1);

  // 1. Extrai datas
  props.games.forEach(game => {
      if (!game.commenceTime) return;
      const d = new Date(game.commenceTime);
      const ano = d.getFullYear();
      const mes = String(d.getMonth() + 1).padStart(2, '0');
      const dia = String(d.getDate()).padStart(2, '0');
      datasUnicas.add(`${ano}-${mes}-${dia}`);
  });

  // 2. Ordena
  const datasOrdenadas = Array.from(datasUnicas).sort();

  // 3. Gera Labels e FILTRA O PASSADO
  datasOrdenadas.forEach(dataStr => {
      const parts = dataStr.split('-');
      const y = Number(parts[0]);
      const m = Number(parts[1]);
      const d = Number(parts[2]);
      
      const dataRef = new Date(y, m - 1, d);

      // ✅ CORREÇÃO CRÍTICA:
      // Se a data do jogo for anterior a HOJE (ex: jogos passados ou datas erradas de anos anteriores),
      // nós ignoramos e não criamos o botão.
      if (dataRef.getTime() < hoje.getTime()) return;

      let label = '';
      if (dataRef.getTime() === hoje.getTime()) label = 'HOJE';
      else if (dataRef.getTime() === amanha.getTime()) label = 'AMANHÃ';
      else label = `${String(d).padStart(2, '0')}/${String(m).padStart(2, '0')}`;

      lista.push({ label, valor: dataStr });
  });

  return lista;
});

const selecionarData = (valor: string) => {
  emit('update:modelValue', valor);
};
</script>

<template>
  <div v-if="filtros.length > 1" class="flex items-center gap-2 overflow-x-auto custom-scrollbar">
    <button 
        v-for="filtro in filtros" 
        :key="filtro.valor"
        @click="selecionarData(filtro.valor)"
        type="button"
        :class="[
          'px-4 py-1.5 rounded text-xs font-bold transition-all whitespace-nowrap border select-none',
          modelValue === filtro.valor 
            ? 'bg-blue-600 text-white border-blue-500 shadow-lg shadow-blue-900/40' 
            : 'bg-[#1a2c38] text-gray-400 border-transparent hover:bg-[#213746] hover:text-gray-200'
        ]"
    >
        {{ filtro.label }}
    </button>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; height: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #334155; border-radius: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #475569; }
</style>