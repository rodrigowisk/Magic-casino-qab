import api from "../apiSports"; 
import type { AxiosResponse } from "axios";

// ✅ INTERFACE AUXILIAR (Para regras de prêmio)
export interface PrizeRule {
    id: string;
    name: string;
}

// ✅ INTERFACE DO TORNEIO (Dados gerais)
export interface Tournament {
    id: number;
    name: string;
    description?: string;
    sport: string;
    entryFee: number;
    prizePool: number;
    startDate: string;
    endDate: string;
    isActive: boolean;
    isFinished: boolean;
    participantsCount: number;
    houseFeePercent: number;
    initialFantasyBalance: number;
    
    // Propriedades dinâmicas (Contexto do Usuário)
    isJoined?: boolean;
    currentFantasyBalance?: number; 
    rank?: number; 
    filterRules?: string;

    // 👇 NOVOS CAMPOS (Opcionais para não quebrar listagens antigas)
    isFavorite?: boolean; // Para o botão de coração
    myPrize?: number;     // Para o histórico
    status?: string;      // Para o histórico

    // ID da regra de premiação
    prizeRuleId?: string; 
}

// ✅ INTERFACE DO RANKING (Compatível com o DTO do C#)
export interface TournamentRankingDto {
    posicao: number;
    userId: string;
    userName: string;
    avatar: string;
    saldoAtual: number;
    saldoPossivel: number;
    progressoBilhetes: string;
    bilhetesFinalizados?: number;
    bilhetesTotais?: number;
}

// Mantido conforme seu arquivo original para segurança
const API_URL = "/api/tournament";

class TournamentService {
    
    /**
     * Lista todos os torneios
     */
    listTournaments(userId?: string): Promise<AxiosResponse<Tournament[]>> {
        const params = userId ? { userId } : {};
        return api.get<Tournament[]>(`${API_URL}/`, { baseURL: '/', params });
    }

    /**
     * ✅ NOVO: Busca histórico completo (Ativos + Finalizados)
     * Chama a rota: /api/tournament/history/{userId}
     */
    getHistory(userId: string): Promise<AxiosResponse<Tournament[]>> {
        return api.get<Tournament[]>(`${API_URL}/history/${userId}`, { baseURL: '/' });
    }

    /**
     * Cria um novo torneio (Admin)
     */
    createTournament(payload: Partial<Tournament>): Promise<AxiosResponse<Tournament>> {
        return api.post<Tournament>(`${API_URL}/`, payload, { baseURL: '/' });
    }

    /**
     * Busca detalhes de um torneio específico
     */
    getTournament(id: string | number, userId?: string): Promise<AxiosResponse<Tournament>> {
        const params = userId ? { userId } : {};
        return api.get<Tournament>(`${API_URL}/${id}`, { baseURL: '/', params });
    }

    /**
     * Busca o Ranking do Torneio
     */
    getRanking(tournamentId: number): Promise<AxiosResponse<TournamentRankingDto[]>> {
        return api.get<TournamentRankingDto[]>(`${API_URL}/${tournamentId}/ranking`, { baseURL: '/' });
    }

    /**
     * Entrar no torneio
     */
    joinTournament(tournamentId: number, userId: string, userName: string, avatar: string): Promise<AxiosResponse<any>> {
        return api.post(`${API_URL}/${tournamentId}/join`, { 
            userId, 
            userName, 
            avatar 
        }, { baseURL: '/' });
    }

    /**
     * Busca histórico de apostas do PRÓPRIO usuário logado
     */
    getMyBets(tournamentId: number): Promise<AxiosResponse<any>> {
        return api.get(`${API_URL}/${tournamentId}/bets`, { baseURL: '/' });
    }

    /**
     * Busca apostas de QUALQUER jogador pelo ID
     */
    getPlayerBets(tournamentId: number, targetUserId: string): Promise<AxiosResponse<any>> {
        return api.get(`${API_URL}/${tournamentId}/participants/${targetUserId}/bets`, { baseURL: '/' });
    }

    /**
     * Realiza a aposta em lote
     */
    placeBet(tournamentId: number, payload: { userId: string, amount: number, selections: any[] }): Promise<AxiosResponse<any>> {
        return api.post(`${API_URL}/${tournamentId}/bet`, payload, { baseURL: '/' });
    }

    // Mantido para compatibilidade
    placeTournamentBet(tournamentId: number, betPayload: any): Promise<AxiosResponse<any>> {
        return api.post(`${API_URL}/${tournamentId}/bet`, betPayload, { baseURL: '/' });
    }
    
    /**
     * Busca as regras de premiação disponíveis
     */
    getPrizeRules(): Promise<AxiosResponse<PrizeRule[]>> {
        return api.get<PrizeRule[]>(`${API_URL}/prize-rules`, { baseURL: '/' });
    }

    /**
     * ✅ NOVO: Toggle Favorito
     */
    async toggleFavorite(tournamentId: number) {
        return await api.post(`${API_URL}/${tournamentId}/favorite`, {}, { baseURL: '/' });
    }

    async getTournamentRanking(id: number) {
    return api.get(`/Tournaments/${id}/ranking`);
    }
    
}

export default new TournamentService();