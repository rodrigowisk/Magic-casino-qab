<script setup lang="ts">
import { ref } from 'vue';
import { useConfigStore } from '../stores/useConfigStore';
import Swal from 'sweetalert2';

const configStore = useConfigStore();

// Copia os dados da store para o formulário local
const formName = ref(configStore.siteName);
const formTitle = ref(configStore.browserTitle);
const formLogo = ref(configStore.siteLogo);

const handleSave = () => {
    configStore.saveConfig(formName.value, formLogo.value, formTitle.value);
    
    Swal.fire({
        title: 'Sucesso!',
        text: 'Configurações do site atualizadas.',
        icon: 'success',
        background: '#162032',
        color: '#fff'
    });
};
</script>

<template>
    <div class="p-6 max-w-4xl mx-auto text-white">
        <h1 class="text-3xl font-bold mb-8 flex items-center gap-3">
            ⚙️ Configurações Gerais do Site
        </h1>

        <div class="bg-[#1e293b] p-6 rounded-lg border border-gray-700 shadow-xl space-y-6">
            
            <div class="bg-black/30 p-4 rounded border border-gray-600 mb-6 flex items-center justify-between">
                <div>
                    <span class="text-gray-400 text-xs uppercase font-bold">Preview do Cabeçalho:</span>
                    <div class="text-2xl font-bold italic text-white mt-1">
                        <img v-if="formLogo" :src="formLogo" class="h-10 object-contain" />
                        <span v-else>
                            {{ formName }}
                        </span>
                    </div>
                </div>
            </div>

            <div>
                <label class="block text-sm font-bold text-gray-400 mb-2">Nome do Site (Marca)</label>
                <input v-model="formName" type="text" class="w-full bg-[#0f172a] border border-gray-600 rounded p-3 text-white focus:border-blue-500 outline-none">
            </div>

            <div>
                <label class="block text-sm font-bold text-gray-400 mb-2">Título da Aba (Browser)</label>
                <input v-model="formTitle" type="text" class="w-full bg-[#0f172a] border border-gray-600 rounded p-3 text-white focus:border-blue-500 outline-none">
                <p class="text-xs text-gray-500 mt-1">Ex: O texto que aparece na aba do Chrome.</p>
            </div>

            <div>
                <label class="block text-sm font-bold text-gray-400 mb-2">URL da Logo (Opcional)</label>
                <input v-model="formLogo" type="text" placeholder="https://..." class="w-full bg-[#0f172a] border border-gray-600 rounded p-3 text-white focus:border-blue-500 outline-none">
                <p class="text-xs text-gray-500 mt-1">Deixe em branco para usar apenas o Nome em texto.</p>
            </div>

            <button @click="handleSave" class="w-full bg-blue-600 hover:bg-blue-500 text-white font-bold py-3 rounded transition-colors mt-4">
                SALVAR ALTERAÇÕES
            </button>
        </div>
    </div>
</template>