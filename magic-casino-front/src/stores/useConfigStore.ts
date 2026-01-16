import { defineStore } from 'pinia';
import { ref } from 'vue';

export const useConfigStore = defineStore('config', () => {
    // 1. Estado Inicial (Padrão se não tiver nada configurado)
    const siteName = ref(localStorage.getItem('siteName') || 'Brasil Game');
    const siteLogo = ref(localStorage.getItem('siteLogo') || ''); // URL da imagem
    const browserTitle = ref(localStorage.getItem('browserTitle') || 'Apostas Esportivas');
    
    // 2. Ação para atualizar o título da aba do navegador dinamicamente
    const updateDocumentTitle = () => {
        document.title = `${siteName.value} - ${browserTitle.value}`;
    };

    // 3. Salvar alterações (Persistência)
    const saveConfig = (newName: string, newLogo: string, newTitle: string) => {
        siteName.value = newName;
        siteLogo.value = newLogo;
        browserTitle.value = newTitle;

        // Salva no LocalStorage (Simulando banco de dados por enquanto)
        localStorage.setItem('siteName', newName);
        localStorage.setItem('siteLogo', newLogo);
        localStorage.setItem('browserTitle', newTitle);

        updateDocumentTitle();
    };

    // Inicializa o título ao carregar
    updateDocumentTitle();

    return {
        siteName,
        siteLogo,
        browserTitle,
        saveConfig
    };
});