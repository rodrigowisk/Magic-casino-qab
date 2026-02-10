using System;

// ⚠️ MUDANÇA CRÍTICA: Namespace unificado "Magic_casino.Contracts"
namespace Magic_casino.Contracts
{
    public class GameEndedEvent
    {
        public string GameId { get; set; } = string.Empty;
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;
        public string Score { get; set; } = string.Empty;
        public string SportKey { get; set; } = string.Empty;
        public DateTime EndedAt { get; set; }
    }
}