using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino_sportbook.Models
{
    public class SportsEvent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string ExternalId { get; set; } = string.Empty;

        public string? SportKeyRaw { get; set; }
        public string SportKey { get; set; } = string.Empty;
        public string? SportCategory { get; set; }
        public string? LeagueSlug { get; set; }
        public string? LeagueExternalId { get; set; }

        public string League { get; set; } = string.Empty;
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;

        // ✅ NOVAS COLUNAS PARA IMAGENS (IDs da API)
        public string? HomeTeamId { get; set; }
        public string? AwayTeamId { get; set; }
        public string? LeagueId { get; set; }

        public string? OddsSource { get; set; }

        public DateTime CommenceTime { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal RawOddsHome { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal RawOddsDraw { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal RawOddsAway { get; set; }

        public ICollection<MarketOdd> Odds { get; set; } = new List<MarketOdd>();
    }
}