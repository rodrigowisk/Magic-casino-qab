namespace Magic_casino_sportbook.Events
{
    // Este evento contém tudo que o consumidor precisa para pagar o usuário
    public class BetWonEvent
    {
        public string BetId { get; set; } = string.Empty;
        public string UserCpf { get; set; } = string.Empty;
        public decimal PayoutAmount { get; set; }
        public DateTime WonAt { get; set; }
    }
}