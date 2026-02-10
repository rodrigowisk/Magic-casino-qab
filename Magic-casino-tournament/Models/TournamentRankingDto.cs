namespace Magic_casino_tournament.Models
{
    public class TournamentRankingDto
    {
        public int Posicao { get; set; }        // 1º, 2º, 3º...

        public string UserId { get; set; }      // Identificador único (GUID/CPF)
        public string UserName { get; set; }    // Nome de exibição
        public string Avatar { get; set; }      // URL da imagem

        // Dados Financeiros
        public decimal SaldoAtual { get; set; }    // O que ele tem garantido agora (FantasyBalance)
        public decimal SaldoPossivel { get; set; } // Saldo Atual + Potencial das apostas em aberto

        // Controle de Bilhetes
        public string ProgressoBilhetes { get; set; } // String formatada: "3/10" (Finalizados / Total)

        // (Opcional) Mando também os números crus caso o front queira fazer uma barra de progresso
        public int BilhetesFinalizados { get; set; }
        public int BilhetesTotais { get; set; }
    }
}