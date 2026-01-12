using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Magic_casino_slot.Data
{
    [Table("wallets")]
    public class Wallet
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        // Os 3 saldos que definimos
        [Column("balance_fiver")]
        public long BalanceFiver { get; set; } = 0;

        [Column("balance_qab")]
        public long BalanceQab { get; set; } = 0;

        [Column("balance_bonus")]
        public long BalanceBonus { get; set; } = 0;

        // Chave Estrangeira (Liga a carteira ao Usuário)
        [Column("user_code")]
        public string UserCode { get; set; }

        // Propriedade de Navegação (Para o Entity Framework entender a ligação)
        [ForeignKey("UserCode")]
        [JsonIgnore] // Para não dar loop infinito no JSON
        public virtual User User { get; set; }
    }
}