import { ref } from 'vue';

export function usePageLoader() {
    const isLoading = ref(true);
    const loadingProgress = ref(0);
    const isContentReady = ref(false);
    let interval: any = null;

    // Inicia a barra falsa (vai até 90%)
    const startLoader = () => {
        isLoading.value = true;
        isContentReady.value = false;
        loadingProgress.value = 5;
        
        if (interval) clearInterval(interval);
        
        interval = setInterval(() => {
            if (loadingProgress.value < 90) {
                // Aumenta um pouco aleatoriamente para parecer natural
                loadingProgress.value += Math.random() * 10;
            }
        }, 300);
    };

    // Finaliza (vai a 100% e libera a tela)
    const finishLoader = () => {
        clearInterval(interval);
        loadingProgress.value = 100;

        setTimeout(() => {
            isContentReady.value = true; // Libera o DOM de baixo
            setTimeout(() => {
                isLoading.value = false; // Remove a tela preta
            }, 500); 
        }, 300);
    };

    return {
        isLoading,
        loadingProgress,
        isContentReady,
        startLoader,
        finishLoader
    };
}