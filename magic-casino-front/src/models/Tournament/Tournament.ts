export interface Tournament {
    id: number;
    name: string;
    description?: string;
    sport?: string;
    entryFee: number;
    houseFeePercent?: number;
    initialFantasyBalance?: number;
    prizePool?: number;
    isActive: boolean;
    isFinished: boolean;
    startDate: string;
    endDate: string;
    rank?: number;
    
    prizeRuleId?: string;
    // Campos Extras
    participantsCount?: number;
    isJoined?: boolean;
    
    // ✅ CORREÇÃO: Adicione este campo para o TS parar de reclamar
    filterRules?: string; 
}