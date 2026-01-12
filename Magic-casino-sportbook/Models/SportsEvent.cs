using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Magic_casino_sportbook.Models
{
    public class SportsEvent
    {
        [Key]
        public string ExternalId { get; set; }
        public string SportKey { get; set; }
        public string League { get; set; }
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public DateTime CommenceTime { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

        // Lista dinâmica para suportar todos os mercados
        public ICollection<MarketOdd> Odds { get; set; } = new List<MarketOdd>();
    }
}