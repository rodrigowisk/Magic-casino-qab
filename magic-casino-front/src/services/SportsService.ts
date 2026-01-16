import axios from 'axios';

// ⚠️ CONFIRA A PORTA: Se estiver usando Docker, geralmente é 8090 ou 8080.
const api = axios.create({
    baseURL: 'http://localhost:8090/api/sports', 
    timeout: 10000
});

export interface SportSummary {
    key: string;   // Ex: 'soccer'
    count: number; // Ex: 463
}

export default {
    // Busca o resumo dos esportes (bolinhas do topo)
    async getActiveSports(): Promise<SportSummary[]> {
        const response = await api.get('/active-sports');
        return response.data;
    },

    // ✅ NOVO MÉTODO (Paginação)
    async getEvents(sportKey: string, page: number = 1, limit: number = 20) {
        const response = await api.get(`/events?sport=${sportKey}&page=${page}&pageSize=${limit}`);
        return response.data;
    },

    // ✅ CORREÇÃO: Mantém o nome antigo para não quebrar o Sidebar
    async getEventsBySport(sportKey: string) {
        // Redireciona para o novo método pegando a página 1
        return this.getEvents(sportKey, 1, 20);
    },

    // Busca detalhes de um jogo específico (Odd Details)
    async getEventDetails(eventId: string) {
        const response = await api.get(`/event/${eventId}`);
        return response.data;
    }
}