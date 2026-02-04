import api from "../apiSports"; 
import type { AxiosResponse } from "axios";

// ✅ INTERFACE ESTRITA (Para Consumo/Leitura)
export interface Tournament {
    id: number;           // Obrigatório
    name: string;         // Obrigatório
    description?: string; // Opcional
    sport: string;        // Obrigatório (Soccer, etc)
    entryFee: number;     // Obrigatório
    prizePool: number;
    startDate: string;    // Obrigatório
    endDate: string;      // Obrigatório
    isActive: boolean;
    isFinished: boolean;
    participantsCount: number;
    houseFeePercent: number;
    initialFantasyBalance: number;
    
    // Propriedades dinâmicas (Contexto do Usuário)
    isJoined?: boolean;
    currentFantasyBalance?: number; 
    rank?: number; // ✅ NOVO: Posição do usuário no ranking
    filterRules?: string;
}

// ✅ INTERFACE DO PARTICIPANTE (Para a lista de Ranking)
export interface TournamentParticipant {
    id: number;
    userId: string;
    fantasyBalance: number;
    rank: number;
}

const API_URL = "/tournament/api/tournaments";

class TournamentService {
    
    /**
     * Lista todos os torneios
     */
    listTournaments(userId?: string): Promise<AxiosResponse<Tournament[]>> {
        const params = userId ? { userId } : {};
        return api.get<Tournament[]>(API_URL, { baseURL: '/', params });
    }

    /**
     * Cria um novo torneio (Admin)
     */
    createTournament(payload: Partial<Tournament>): Promise<AxiosResponse<Tournament>> {
        return api.post<Tournament>(API_URL, payload, { baseURL: '/' });
    }

    /**
     * Busca detalhes de um torneio específico
     */
    getTournament(id: string | number, userId?: string): Promise<AxiosResponse<Tournament>> {
        const params = userId ? { userId } : {};
        return api.get<Tournament>(`${API_URL}/${id}`, { baseURL: '/', params });
    }

    /**
     * ✅ NOVO: Busca o Ranking do Torneio
     */
    getRanking(tournamentId: number): Promise<AxiosResponse<TournamentParticipant[]>> {
        return api.get<TournamentParticipant[]>(`${API_URL}/${tournamentId}/ranking`, { baseURL: '/' });
    }

    joinTournament(tournamentId: number, userId: string): Promise<AxiosResponse<any>> {
        return api.post(`${API_URL}/${tournamentId}/join`, { userId }, { baseURL: '/' });
    }

    /**
     * ✅ CORREÇÃO PRINCIPAL: Adicionado o método placeBet que o Cupom chama
     * Aceita o payload de lote { userId, amount, selections[] }
     */
    placeBet(tournamentId: number, payload: { userId: string, amount: number, selections: any[] }): Promise<AxiosResponse<any>> {
        return api.post(`${API_URL}/${tournamentId}/bet`, payload, { baseURL: '/' });
    }

    // Mantido para compatibilidade, caso algum arquivo antigo ainda chame assim
    placeTournamentBet(tournamentId: number, betPayload: any): Promise<AxiosResponse<any>> {
        return api.post(`${API_URL}/${tournamentId}/bet`, betPayload, { baseURL: '/' });
    }
    
}

export default new TournamentService();