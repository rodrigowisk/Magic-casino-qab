import api from "../apiSports";
import type { AxiosResponse } from "axios";

export interface TournamentTemplate {
    id?: number;
    name: string;
    filterRules: string; // JSON String
}

const API_URL = "/tournament/api/TournamentTemplates";

class TournamentTemplateService {
    
    getAll(): Promise<AxiosResponse<TournamentTemplate[]>> {
        return api.get<TournamentTemplate[]>(API_URL, { baseURL: '/' });
    }

    create(template: TournamentTemplate): Promise<AxiosResponse<TournamentTemplate>> {
        return api.post<TournamentTemplate>(API_URL, template, { baseURL: '/' });
    }

    delete(id: number): Promise<AxiosResponse<any>> {
        return api.delete(`${API_URL}/${id}`, { baseURL: '/' });
    }
}

export default new TournamentTemplateService();