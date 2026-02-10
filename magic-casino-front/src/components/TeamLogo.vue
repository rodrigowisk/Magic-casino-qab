<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  teamName: string;
  size?: string;
}>();

// 1. CARREGA TODAS AS IMAGENS (EAGER = JÁ CARREGA O CAMINHO)
const teamLogos = import.meta.glob('/src/assets/teams/*.png', { 
    eager: true, 
    import: 'default' 
});

// 2. FUNÇÃO DE LIMPEZA (NORMALIZAÇÃO)
// Transforma "GKS Tychy" em "gkstychy" e "1093_Colon.png" em "colon"
const normalize = (str: string) => {
    return str
        .toLowerCase()
        .normalize('NFD').replace(/[\u0300-\u036f]/g, '') // Remove acentos
        .replace(/^\d+_/, '') // 🔥 REMOVE O ID DO INÍCIO (ex: "1093_")
        .replace(/_/g, '')    // Remove underscores
        .replace(/[^a-z0-9]/g, ''); // Remove espaços e caracteres especiais
};

// 3. CRIA UM MAPA OTIMIZADO: { "nomedotime": "caminho_da_imagem" }
// Isso roda apenas uma vez quando o componente é montado, muito rápido.
const logoMap: Record<string, string> = {};

for (const path in teamLogos) {
    // path é algo como "/src/assets/teams/1093_Colon.png"
    
    // Pega só o nome do arquivo: "1093_Colon.png"
    const filename = path.split('/').pop() || '';
    
    // Remove a extensão: "1093_Colon"
    const nameWithoutExt = filename.replace(/\.[^/.]+$/, "");
    
    // Normaliza para chave de busca: "colon"
    const key = normalize(nameWithoutExt);
    
    logoMap[key] = teamLogos[path] as string;
}

// 4. BUSCA INTELIGENTE
const finalSrc = computed(() => {
    if (!props.teamName) return undefined;

    // Normaliza o nome que veio da prop (ex: "Colón" -> "colon")
    const searchKey = normalize(props.teamName);

    // 1ª Tentativa: Busca exata
    if (logoMap[searchKey]) {
        return logoMap[searchKey];
    }

    // 2ª Tentativa (Fallback): Tenta encontrar se o nome do arquivo CONTÉM o nome do time
    // Útil para casos como "1097_GKS_Tychy_71" vs "GKS Tychy"
    const foundKey = Object.keys(logoMap).find(key => key.includes(searchKey) || searchKey.includes(key));
    
    return foundKey ? logoMap[foundKey] : undefined;
});

// Helper de tamanho de fonte para o Placeholder
const fontSizeClass = computed(() => {
    const sizeStr = props.size || '32';
    const sizeNum = parseInt(sizeStr.replace(/\D/g, '')) || 32;
    return sizeNum > 30 ? 'text-xs' : 'text-[9px]';
});
</script>

<template>
  <div 
    class="relative shrink-0 flex items-center justify-center transition-all duration-300 select-none"
    :class="[
      size || 'w-8 h-8', 
      !finalSrc ? 'rounded-full bg-[#1a2c38] border border-white/10 overflow-hidden' : ''
    ]"
  >
    <img 
      v-if="finalSrc"
      :src="finalSrc" 
      loading="lazy" 
      class="w-full h-full object-contain filter drop-shadow-md"
      :alt="teamName"
      @error="(e) => (e.target as HTMLImageElement).style.display = 'none'"
    />

    <div 
        v-else 
        class="w-full h-full flex items-center justify-center font-bold text-gray-500"
        :class="fontSizeClass"
    >
        {{ teamName ? teamName.charAt(0).toUpperCase() : '?' }}
    </div>

  </div>
</template>