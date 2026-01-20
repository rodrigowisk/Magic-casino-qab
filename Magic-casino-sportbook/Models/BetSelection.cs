using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Magic_casino_sportbook.Models
{
    [Table("BetSelections")]
    public class BetSelection
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // ✅ FORÇAMOS O EF A USAR ESTA COLUNA COMO CHAVE ESTRANGEIRA
        public string BetId { get; set; } = string.Empty;

        [JsonIgnore]
        [ForeignKey("BetId")] // <--- ESTA LINHA RESOLVE O ERRO "BetId1"
        public Bet? Bet { get; set; }

        [Required]
        public string MatchId { get; set; } = string.Empty;

        public string MatchName { get; set; } = string.Empty;

        [Required]
        public string MarketName { get; set; } = string.Empty;

        // --- COMPATIBILIDADE (SelectionName -> OutcomeName) ---
        [Required]
        public string OutcomeName { get; set; } = string.Empty;

        [NotMapped]
        public string SelectionName
        {
            get { return OutcomeName; }
            set { OutcomeName = value; }
        }
        // -------------------------------------------------------

        [Column(TypeName = "decimal(18,2)")]
        public decimal Odd { get; set; }

        public string Status { get; set; } = "pending";

        public DateTime CommenceTime { get; set; }

        public string? FinalScore { get; set; }
        public bool? IsWinner { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}