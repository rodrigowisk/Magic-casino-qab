namespace Magic_casino_sportbook.Models
{
    public class Bet
    {
        public int Id { get; set; }

        public string UserCpf { get; set; } = "";

        public decimal Amount { get; set; }
        public decimal TotalOdd { get; set; }
        public decimal PotentialReturn { get; set; }

        // ✅ Para idempotência / rastreio
        public string? CoreTxnId { get; set; }

        public string Status { get; set; } = "pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<BetSelection> Selections { get; set; } = new();
    }
}
