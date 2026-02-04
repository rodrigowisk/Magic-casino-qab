using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_tournament.Models
{
    public class TournamentBetSelection
    {
        [Key]
        public int Id { get; set; }

        public int TournamentBetId { get; set; }
        [ForeignKey("TournamentBetId")]
        public virtual TournamentBet TournamentBet { get; set; }

        // Dados do Jogo
        public string GameId { get; set; } = string.Empty;
        public string SportKey { get; set; } = string.Empty;
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;

        // Dados da Aposta Específica
        public string MarketName { get; set; } = string.Empty;
        public string SelectionName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Odds { get; set; }

        public string Status { get; set; } = "Pending"; // Pending, Won, Lost
    }
}