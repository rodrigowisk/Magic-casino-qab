using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
// Adicione este using para evitar erro com List<> se não tiver
using System.Collections.Generic;

namespace Magic_casino_tournament.Models
{
    public class Tournament
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Sport { get; set; } = "Futebol";

        public string? Category { get; set; } = "Destaques";
        public string? CoverImage { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EntryFee { get; set; }

        public int HouseFeePercent { get; set; } = 10;

        [Column(TypeName = "decimal(18,2)")]
        public decimal InitialFantasyBalance { get; set; } = 1000m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrizePool { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? FixedPrize { get; set; }

        public int? MaxParticipants { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsFinished { get; set; } = false;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        [Column(TypeName = "jsonb")]
        public string? FilterRules { get; set; }

        public string PrizeRuleId { get; set; } = "PREMIO_1";

        // =================================================================
        // ✅ ADICIONADO: Propriedade de Navegação (Essencial para Performance)
        // =================================================================
        public virtual ICollection<TournamentParticipant> Participants { get; set; } = new List<TournamentParticipant>();

        // =================================================================
        // CAMPOS VIRTUAIS (DTO) - Não salvos na tabela de Torneios
        // =================================================================

        [NotMapped]
        public int ParticipantsCount { get; set; }

        [NotMapped]
        public bool IsJoined { get; set; } = false;

        [NotMapped]
        public decimal? CurrentFantasyBalance { get; set; }

        [NotMapped]
        public int? Rank { get; set; }
    }
}