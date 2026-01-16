using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_tournament.Models
{
    public class TournamentBet
    {
        [Key]
        public int Id { get; set; }

        public int ParticipantId { get; set; }
        [ForeignKey("ParticipantId")]
        public virtual TournamentParticipant Participant { get; set; }

        public string GameId { get; set; } = string.Empty;
        public string SportKey { get; set; } = string.Empty;
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;

        public string MarketName { get; set; } = string.Empty;
        public string SelectionName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Odds { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PotentialWin { get; set; }

        public string Status { get; set; } = "Pending";
        public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SettledAt { get; set; }
    }
}