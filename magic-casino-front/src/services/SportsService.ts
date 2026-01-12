import axios from 'axios';

// ATENÇÃO: Verifique a porta onde seu .NET está rodando (no seu print era https://localhost:7193)
const api = axios.create({
    baseURL: 'http://localhost:8090/api/sports',
    timeout: 10000
});

export interface SportSummary {
    key: string;   // Ex: 'soccer_brazil_serie_a'
    count: number; // Ex: 10
}

export default {
    // Busca apenas o resumo (para os menus e cards)
    async getActiveSports(): Promise<SportSummary[]> {
        const response = await api.get('/active-sports');
        return response.data;
    },

    // Busca os jogos de um esporte específico (quando clicar no card)
    async getEventsBySport(sportKey: string) {
        const response = await api.get(`/events?sport=${sportKey}`);
        return response.data;
    }
}