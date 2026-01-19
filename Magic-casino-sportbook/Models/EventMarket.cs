using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_sportbook.Data.Models
{
    [Table("EventMarkets")]
    public class EventMarket
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString(); // ID Interno

        public string SportsEventId { get; set; } // Link com o jogo

        public string ExternalId { get; set; }    // ID da BetsAPI (para atualizar depois)

        public string MarketName { get; set; }    // "Resultado Final", "Over/Under"

        public string OutcomeName { get; set; }   // "Casa", "Over 2.5", "Brighton -0.5"

        public decimal Price { get; set; }        // A Odd (1.90)

        public decimal? Handicap { get; set; }    // Valor auxiliar (-0.5, 2.5)

        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }
}