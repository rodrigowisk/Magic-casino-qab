using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_tournament.Models
{
    public class TournamentParticipant
    {
        [Key]
        public int Id { get; set; }

        public int TournamentId { get; set; }

        public string UserId { get; set; } = string.Empty;

        // ✅ 1. Adicionamos o UserName para salvar o nome do jogador
        public string UserName { get; set; } = "Jogador";

        // ✅ 2. Removemos o [NotMapped] do Avatar para ele ser salvo no banco
        public string? Avatar { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FantasyBalance { get; set; }

        public int Rank { get; set; } = 0;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public virtual List<TournamentBet> Bets { get; set; } = new();
    }
}