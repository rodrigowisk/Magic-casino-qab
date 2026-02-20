using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_tournament.Models
{
    public class TournamentFavorite
    {
        [Key, Column(Order = 1)]
        public required string UserId { get; set; }

        [Key, Column(Order = 2)]
        public int TournamentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Relacionamento (Opcional, mas bom ter)
        [ForeignKey("TournamentId")]
        public Tournament? Tournament { get; set; }
    }
}