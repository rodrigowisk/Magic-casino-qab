import { defineStore } from 'pinia';
import { ref } from 'vue';

export const useLoaderStore = defineStore('globalLoader', () => {
    const isLoading = ref(false);
    const loadingText = ref('Carregando...');
    const progress = ref(0);

    const startLoader = (text = 'Carregando...') => {
        isLoading.value = true;
        loadingText.value = text;
        progress.value = 10; // Começa com um pouco de progresso
    };

    const finishLoader = () => {
        progress.value = 100;
        setTimeout(() => {
            isLoading.value = false;
            progress.value = 0;
        }, 500); // Pequeno delay para suavizar
    };

    const setProgress = (val: number) => {
        progress.value = val;
    };

    return {
        isLoading,
        loadingText,
        progress,
        startLoader,
        finishLoader,
        setProgress
    };
});