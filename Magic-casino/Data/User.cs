using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino.Data
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("cpf")]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Required]
        [StringLength(11, MinimumLength = 11)]
        public string Cpf { get; set; } = string.Empty;

        // --- CAMPO ADICIONADO ---
        [Column("name")]
        public string Name { get; set; } = string.Empty;
        // ------------------------

        [Required]
        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Column("email")]
        public string? Email { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("is_admin")]
        public bool IsAdmin { get; set; } = false;

        [Column("agent_code")]
        public string? AgentCode { get; set; }

        [Column("agent_linked_at")]
        public DateTime? AgentLinkedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Relação 1:1 com a Carteira
        public virtual Wallet? Wallet { get; set; }
    }
}