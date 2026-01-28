using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino.Data
{
    [Table("transactions")]
    public class Transaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        // Referência externa (ID do PIX na Velana)
        [Column("external_reference")]
        public string? ExternalReference { get; set; }

        // CPF do Usuário (Relacionamento)
        [Required]
        [Column("user_cpf")]
        public string UserCpf { get; set; } = string.Empty;

        [Column("amount", TypeName = "numeric")]
        public decimal Amount { get; set; }

        // Status: pending, paid, canceled, failed
        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("paid_at")]
        public DateTime? PaidAt { get; set; }

        // Relacionamento opcional com o Usuário
        [ForeignKey("UserCpf")]
        public virtual User? User { get; set; }
    }
}