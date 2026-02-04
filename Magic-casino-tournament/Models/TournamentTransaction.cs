using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_tournament.Models
{
    // ✅ AUDITORIA FINANCEIRA: Registra toda movimentação de dinheiro no torneio
    public class TournamentTransaction
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; } = string.Empty; // CPF ou ID do usuário

        public int TournamentId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public string Type { get; set; } = "ENTRY_FEE"; // ENTRY_FEE, PRIZE, REFUND

        public string Status { get; set; } = "PENDING"; // PENDING, COMPLETED, FAILED

        public string? CoreTransactionId { get; set; } // ID da transação lá no Core (para conciliação)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}