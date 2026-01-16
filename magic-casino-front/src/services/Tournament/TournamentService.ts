import axios, { type AxiosResponse } from "axios";

// ✅ CAMINHO RELATIVO OBRIGATÓRIO (Não use @)
import type { Tournament } from "../../models/Tournament/Tournament";

const API_URL = "/tournament/api/tournaments";

class TournamentService {
    
    // Lista todos
    listTournaments(): Promise<AxiosResponse<Tournament[]>> {
        return axios.get<Tournament[]>(API_URL);
    }

    // Cria (Admin)
    createTournament(payload: Tournament): Promise<AxiosResponse<Tournament>> {
        return axios.post<Tournament>(API_URL, payload);
    }

    // Detalhes
    getTournament(id: string | number): Promise<AxiosResponse<Tournament>> {
        return axios.get<Tournament>(`${API_URL}/${id}`);
    }

    // Entrar (Join)
    joinTournament(tournamentId: number, userId: string): Promise<AxiosResponse<any>> {
        return axios.post(`${API_URL}/${tournamentId}/join`, { userId });
    }
}

export default new TournamentService();