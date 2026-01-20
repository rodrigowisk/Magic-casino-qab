using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_sportbook.Models
{
    [Table("Bets")]
    public class Bet
    {
        // 🟢 CORREÇÃO: Mudamos de 'int' para 'string' para combinar com o BetSelection
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string UserCpf { get; set; } = "";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalOdd { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PotentialReturn { get; set; }

        // Para idempotência / rastreio
        public string? CoreTxnId { get; set; }

        public string Status { get; set; } = "pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Relacionamento com as seleções
        public List<BetSelection> Selections { get; set; } = new();
    }
}