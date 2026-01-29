import { defineStore } from 'pinia';
import { ref } from 'vue';
import axios from 'axios';

export const useFavoritesStore = defineStore('favorites', () => {
    // Estado Global Compartilhado
    const favoriteLeagues = ref<Set<string>>(new Set());

    // Verifica se é favorito
    const isFavorite = (leagueName: string) => {
        return favoriteLeagues.value.has(leagueName);
    };

    // Carrega favoritos (LocalStorage)
    const fetchFavorites = () => {
        try {
            const saved = localStorage.getItem('user_fav_leagues');
            if (saved) {
                const parsed = JSON.parse(saved);
                favoriteLeagues.value = new Set(parsed);
            }
        } catch (e) {
            console.error("Erro ao carregar favoritos da store", e);
        }
    };

    // Ação de Alternar (Salva no Banco e LocalStorage)
    const toggleFavorite = async (leagueName: string, sportKey: string) => {
        // 1. Atualização Visual Instantânea
        if (favoriteLeagues.value.has(leagueName)) {
            favoriteLeagues.value.delete(leagueName);
        } else {
            favoriteLeagues.value.add(leagueName);
        }

        // 2. Salva Local
        try {
            localStorage.setItem('user_fav_leagues', JSON.stringify(Array.from(favoriteLeagues.value)));
        } catch (error) {
            console.error("Erro local:", error);
        }

        // 3. Salva no Servidor
        try {
            // Usa a rota relativa que o Nginx redireciona corretamente
            await axios.post('/sportbook/api/favorites/toggle', {
                LeagueName: leagueName,
                SportKey: sportKey
            });
        } catch (error) {
            console.error("Erro sync servidor:", error);
        }
    };

    return {
        favoriteLeagues,
        isFavorite,
        fetchFavorites,
        toggleFavorite
    };
});