export interface Tournament {
  id?: number;             // Backend envia int
  name: string;
  description?: string;
  sport: string;           // "Futebol", etc
  
  entryFee: number;        // Valor da entrada (R$)
  houseFeePercent: number; // % da casa (ex: 10)
  
  initialFantasyBalance: number; // Fichas iniciais
  
  prizePool?: number;      // Prêmio acumulado (calculado no back)
  
  startDate: string;
  endDate: string;
  
  isActive?: boolean;
  isFinished?: boolean;
}