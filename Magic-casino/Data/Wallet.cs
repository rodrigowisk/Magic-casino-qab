using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino.Data
{
    [Table("wallets")]
    public class Wallet
    {
        [Key]
        [Column("user_cpf")]
        [Required]
        public string UserCpf { get; set; } = string.Empty;

        [Column("balance_fiver", TypeName = "numeric")]
        public decimal BalanceFiver { get; set; } = 0.00m;

        [Column("balance_qab", TypeName = "numeric")]
        public decimal BalanceQab { get; set; } = 0.00m;

        [Column("balance_bonus", TypeName = "numeric")]
        public decimal BalanceBonus { get; set; } = 0.00m;

        [ForeignKey("UserCpf")]
        public virtual User? User { get; set; }
    }
}