using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_tournament.Models // 👈 O namespace deve ser este
{
    public class SportsEvent
    {
        [Key]
        public int Id { get; set; }

        public string ExternalId { get; set; } = string.Empty; // ID da API (ex: "365_123456")

        public string SportKey { get; set; } = string.Empty;
        public string SportTitle { get; set; } = string.Empty;

        public DateTime CommenceTime { get; set; }

        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;

        // Placar e Status
        public string? Score { get; set; }
        public string? GameTime { get; set; }
        public string Status { get; set; } = "Prematch"; // Prematch, Live, Ended
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RawOddsHome { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal RawOddsDraw { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal RawOddsAway { get; set; }
    }
}