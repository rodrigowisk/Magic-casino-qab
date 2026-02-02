namespace Magic_Casino_Core.DTOs
{
    public class WalletTransactionDto
    {
        public string UserCpf { get; set; } = string.Empty;
        public decimal Amount { get; set; }

        // Campos adicionais necessários para o registro
        public string? Source { get; set; }      // Ex: "Sportbook"
        public string? ReferenceId { get; set; } // UUID da aposta
        public string? Type { get; set; }        // Ex: "bet", "win", "refund"
    }
}