import axios from 'axios';

// ---------------------------------------------------------------------------
// 1️⃣ CLIENTE DE DADOS DE ESPORTES (Odds, Jogos, Eventos)
// URL Base: http://localhost:8090/api/sports
// ---------------------------------------------------------------------------
const apiSports = axios.create({
    baseURL: 'http://localhost:8090/api/sports', 
    timeout: 10000
});

// ---------------------------------------------------------------------------
// 2️⃣ CLIENTE DE CONFIGURAÇÃO (Painel Admin)
// URL Base: http://localhost:8090/api/admin
// ---------------------------------------------------------------------------
const apiAdmin = axios.create({
    baseURL: 'http://localhost:8090/api/admin', 
    timeout: 10000
});

export interface SportSummary {
    key: string;   // Ex: 'soccer'
    count: number; // Ex: 463
}

export default {
    // =================================================================
    // MÉTODOS DE ESPORTES (Usam apiSports)
    // =================================================================

    // Busca o resumo dos esportes (bolinhas do topo)
    async getActiveSports(): Promise<SportSummary[]> {
        const response = await apiSports.get('/active-sports');
        return response.data;
    },

    // ✅ Paginação
    async getEvents(sportKey: string, page: number = 1, limit: number = 20) {
        const response = await apiSports.get(`/events?sport=${sportKey}&page=${page}&pageSize=${limit}`);
        return response.data;
    },

    // ✅ Mantido para compatibilidade com Sidebar
    async getEventsBySport(sportKey: string) {
        return this.getEvents(sportKey, 1, 20);
    },

    // Busca detalhes de um jogo específico
    async getEventDetails(eventId: string) {
        const response = await apiSports.get(`/event/${eventId}`);
        return response.data;
    },

    // =================================================================
    // 🆕 MÉTODO NOVO (Usa apiAdmin) - ESSE ERA O QUE FALTAVA
    // =================================================================
    
    // Busca a configuração ON/OFF do Painel Master
    async getAdminConfig() {
        // Bate em: GET http://localhost:8090/api/admin/configuration
        const response = await apiAdmin.get('/configuration');
        return response.data;
    }
}