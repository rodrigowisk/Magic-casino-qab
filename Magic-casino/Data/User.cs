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

        // Alias para compatibilidade
        [NotMapped]
        public string Id => Cpf;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("avatar")]
        public string? Avatar { get; set; }

        [Column("user_name")]
        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Column("email")]
        public string? Email { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("level")]
        public string Level { get; set; } = "bronze";

        [Column("email_verified")]
        public bool EmailVerified { get; set; } = false;

        [Column("verification_token")]
        public string? VerificationToken { get; set; }

        [Column("tournaments_played")]
        public int TournamentsPlayed { get; set; } = 0;

        [Column("is_admin")]
        public bool IsAdmin { get; set; } = false;

        [Column("agent_code")]
        public string? AgentCode { get; set; }

        [Column("agent_linked_at")]
        public DateTime? AgentLinkedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("security_stamp")]
        public string? SecurityStamp { get; set; }

        public virtual Wallet? Wallet { get; set; }
    }
}