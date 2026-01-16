using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_tournament.Models
{
    public class TournamentParticipant
    {
        [Key]
        public int Id { get; set; }

        public int TournamentId { get; set; }
        // Se quiser navegação:
        // [ForeignKey("TournamentId")]
        // public virtual Tournament Tournament { get; set; }

        public string UserId { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal FantasyBalance { get; set; }

        public int Rank { get; set; } = 0;
    }
}