import api from "../apiSports"; 
import type { AxiosResponse } from "axios";

// URL base do controller 'SportsController.cs'
const API_URL = "/sportbook/api/sports"; 

export interface SportsEventDto {
    externalId: string;
    sportKey: string;
    homeTeam: string;
    awayTeam: string;
    league: string;
    leagueId?: string; // Adicionado
    homeTeamLogo?: string; // Adicionado
    awayTeamLogo?: string; // Adicionado
    countryCode?: string; // Adicionado
    commenceTime: string; 
    rawOddsHome: number;
    rawOddsDraw: number;
    rawOddsAway: number;
}

class SportbookService {
    
    /**
     * Busca jogos Pré-Match.
     * ✅ Alterado para aceitar 'limit' customizado.
     */
    getEventsBySport(sportKey: string, limit: number = 50): Promise<AxiosResponse<SportsEventDto[]>> {
        let key = sportKey.toLowerCase();
        if (key === 'futebol') key = 'soccer';
        if (key === 'basquete') key = 'basketball';
        if (key === 'tenis') key = 'tennis';

        return api.get<SportsEventDto[]>(`${API_URL}/events`, { 
            baseURL: '/', 
            params: { 
                sport: key, 
                page: 1, 
                pageSize: limit // ✅ Agora usamos o limite passado (ex: 1000)
            } 
        });
    }

    getLiveEvents(): Promise<AxiosResponse<any[]>> {
        return api.get<any[]>('/sportbook/api/LiveEvents', { baseURL: '/' });
    }
}

export default new SportbookService();