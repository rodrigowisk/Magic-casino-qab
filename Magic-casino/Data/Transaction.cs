using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Magic_casino.Models;

namespace Magic_casino.Data
{
    [Table("transactions")]
    public class Transaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("external_reference")]
        public string? ExternalReference { get; set; }

        [Required]
        [Column("user_cpf")]
        public string UserCpf { get; set; } = string.Empty;

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("paid_at")]
        public DateTime? PaidAt { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        // ✅ CORREÇÃO: Agora mapeamos porque vimos no print que elas existem!
        [Column("type")]
        public string? Type { get; set; }

        [Column("source")]
        public string? Source { get; set; }

        // Relacionamento
        [ForeignKey("UserCpf")]
        public virtual User? User { get; set; }
    }
}