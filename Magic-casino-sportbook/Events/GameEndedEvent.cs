using System;

namespace Magic_casino_sportbook.Events
{
    // Esta é a mensagem que será enviada para a fila
    public class GameEndedEvent
    {
        public string GameId { get; set; } = string.Empty; // ID externo (ex: "189452189")
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;
        public string Score { get; set; } = string.Empty; // Ex: "2-1"
        public string SportKey { get; set; } = string.Empty; // Ex: "soccer"
        public DateTime EndedAt { get; set; } = DateTime.UtcNow;
    }
}