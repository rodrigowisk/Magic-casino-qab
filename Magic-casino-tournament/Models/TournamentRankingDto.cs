namespace Magic_casino_tournament.Models
{
    public class TournamentRankingDto
    {
        // --- Identificação ---
        public required string UserId { get; set; }
        public required string UserName { get; set; }
        public required string Avatar { get; set; }

        // --- Ranking e Posição ---
        public int Posicao { get; set; }  // Usado pelo Front
        public int Rank { get; set; }     // Usado pelo Service (CS1656)

        // --- Dados Financeiros ---
        public decimal SaldoAtual { get; set; }
        public decimal SaldoPossivel { get; set; }
        public decimal Points { get; set; } // Necessário para o Service (CS0117)

        // --- Controle de Bilhetes (Front) ---
        public required string ProgressoBilhetes { get; set; }
        public int BilhetesFinalizados { get; set; }
        public int BilhetesTotais { get; set; }

        // --- Controle de Bilhetes (Backend/Legado) ---
        // Estas propriedades são vitais porque o SQL do Service grava nelas
        public int MyTicketCount { get; set; }     // (CS0117)
        public int MyFinishedTickets { get; set; } // (CS0117)
    }
}