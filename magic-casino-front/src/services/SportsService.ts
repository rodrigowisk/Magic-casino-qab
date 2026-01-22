import axios from 'axios';

// ---------------------------------------------------------------------------
// 1️⃣ CLIENTE DE DADOS DE ESPORTES
// O VITE_API_URL vem do seu .env (que configuramos como /sportbook/api/sports)
// ---------------------------------------------------------------------------
const apiSports = axios.create({
    baseURL: import.meta.env.VITE_API_URL || '/sportbook/api/sports', 
    timeout: 10000
});

// ---------------------------------------------------------------------------
// 2️⃣ CLIENTE DE CONFIGURAÇÃO (ADMIN)
// Caminho relativo para passar pelo Proxy -> Nginx -> Sportbook
// ---------------------------------------------------------------------------
const apiAdmin = axios.create({
    baseURL: '/sportbook/api/admin', 
    timeout: 10000
});

// --- INTERCEPTOR (IMPORTANTE) ---
// Adiciona o token automaticamente se o usuário estiver logado.
// Isso evita erros 401/403 no painel admin ou apostas.
const injectToken = (config: any) => {
    const token = localStorage.getItem('token');
    if (token) {
        // Remove aspas extras se existirem
        const cleanToken = token.replace(/['"]+/g, '');
        config.headers.Authorization = `Bearer ${cleanToken}`;
    }
    return config;
};

apiSports.interceptors.request.use(injectToken);
apiAdmin.interceptors.request.use(injectToken);

// --- INTERFACE ---
export interface SportSummary {
    key: string;   
    count: number; 
}

export default {
    // =================================================================
    // MÉTODOS DE ESPORTES
    // =================================================================

    async getActiveSports(): Promise<SportSummary[]> {
        // Bate em: /sportbook/api/sports/active-sports
        const response = await apiSports.get('/active-sports');
        return response.data;
    },

    async getEvents(sportKey: string, page: number = 1, limit: number = 20) {
        const response = await apiSports.get(`/events?sport=${sportKey}&page=${page}&pageSize=${limit}`);
        return response.data;
    },

    async getEventsBySport(sportKey: string) {
        return this.getEvents(sportKey, 1, 20);
    },

    async getEventDetails(eventId: string) {
        const response = await apiSports.get(`/event/${eventId}`);
        return response.data;
    },

    // =================================================================
    // MÉTODOS DO ADMIN (TopSportsMenu)
    // =================================================================
    
    async getAdminConfig() {
        // Bate em: /sportbook/api/admin/configuration
        const response = await apiAdmin.get('/configuration');
        return response.data;
    }
}