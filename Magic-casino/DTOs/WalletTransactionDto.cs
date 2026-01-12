namespace Magic_Casino_Core.DTOs
{
    public class WalletTransactionDto
    {
        // ✅ Definido como string para aceitar o CPF
        public string UserCpf { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Source { get; set; } = "Sportbook";
        public string ReferenceId { get; set; }
    }
}