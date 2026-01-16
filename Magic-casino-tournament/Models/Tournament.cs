using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        [Column(TypeName = "decimal(18,2)")]
        public decimal EntryFee { get; set; }

        public int HouseFeePercent { get; set; } = 10;

        [Column(TypeName = "decimal(18,2)")]
        public decimal InitialFantasyBalance { get; set; } = 1000m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrizePool { get; set; } = 0m;

        public bool IsActive { get; set; } = true;
        public bool IsFinished { get; set; } = false;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}