using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_slot.Data // Importante: Namespace Magic_casino.Data
{
    [Table("users")] // Nome da tabela no banco
    public class User
    {
        [Key]
        [Column("code")]
        public string Code { get; set; } = string.Empty; // Chave Primária

        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Column("email")]
        public string? Email { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        public virtual Wallet? Wallet { get; set; }

        [Column("is_admin")]
        public bool IsAdmin { get; set; } = false;

        [Column("agent_code")]
        public string? AgentCode { get; set; }

        [Column("agent_linked_at")]
        public DateTime? AgentLinkedAt { get; set; }
    }
}