namespace Magic_casino_sportbook.Events
{
    public class BetPlacedEvent
    {
        public string BetId { get; set; } = string.Empty; // 👈 MUDOU DE INT PARA STRING
        public string UserCpf { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal PotentialReturn { get; set; }
        public DateTime Timestamp { get; set; }
        public int SelectionsCount { get; set; }
    }
}