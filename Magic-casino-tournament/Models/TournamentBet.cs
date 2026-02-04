using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_tournament.Models
{
    public class TournamentBet
    {
        [Key]
        public int Id { get; set; }

        public int TournamentId { get; set; }
        public string UserId { get; set; } = string.Empty;

        public int ParticipantId { get; set; }
        [ForeignKey("ParticipantId")]
        public virtual TournamentParticipant Participant { get; set; }

        // Dados Financeiros do BILHETE INTEIRO
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } // Valor Apostado Total

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalOdds { get; set; } // Odd Multiplicada

        [Column(TypeName = "decimal(18,2)")]
        public decimal PotentialWin { get; set; } // Retorno Total

        public string Status { get; set; } = "Pending"; // Pending, Won, Lost
        public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SettledAt { get; set; }

        // ✅ RELACIONAMENTO: Um bilhete tem várias seleções
        public virtual List<TournamentBetSelection> Selections { get; set; } = new();
    }
}