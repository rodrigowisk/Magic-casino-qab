using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Magic_casino_sportbook.Data.Models; // Necessário para encontrar EventMarket
using Magic_casino_sportbook.Models;

namespace Magic_casino_sportbook.Models
{
    [Table("SportsEvents")]
    public class SportsEvent
    {
        // ✅ 1. ADICIONADO: Chave Primária Interna (GUID)
        // Isso resolve o erro "definition for 'Id' not found"
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string ExternalId { get; set; } = string.Empty;

        public string? SportKeyRaw { get; set; }
        public string SportKey { get; set; } = string.Empty;
        public string? SportCategory { get; set; }
        public string? LeagueSlug { get; set; }
        public string? LeagueExternalId { get; set; }

        public string League { get; set; } = string.Empty;
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;

        // ✅ CAMPOS DE IMAGEM
        public string? HomeTeamId { get; set; }
        public string? AwayTeamId { get; set; }
        public string? LeagueId { get; set; }

        // ⭐ Código do País (ex: 'br', 'us')
        public string? CountryCode { get; set; }

        public string? HomeTeamLogo { get; set; }
        public string? AwayTeamLogo { get; set; }

        // ✅ CAMPOS DE STATUS
        public string? Status { get; set; }
        public string? Score { get; set; }
        public string? GameTime { get; set; }

        public int HomeAttacks { get; set; }
        public int AwayAttacks { get; set; }

        public int HomeDangerousAttacks { get; set; }
        public int AwayDangerousAttacks { get; set; }

        public int HomeCorners { get; set; }
        public int AwayCorners { get; set; }

        public int HomeOnTarget { get; set; }
        public int AwayOnTarget { get; set; }

        public int HomeOffTarget { get; set; }
        public int AwayOffTarget { get; set; }

        public int HomePossession { get; set; }
        public int AwayPossession { get; set; }

        public string? OddsSource { get; set; }

        public DateTime CommenceTime { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal RawOddsHome { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal RawOddsDraw { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal RawOddsAway { get; set; }

        // ✅ 2. CORRIGIDO: Usa 'EventMarket' em vez de 'MarketOdd'
        public ICollection<EventMarket> Odds { get; set; } = new List<EventMarket>();

        public int HomeRedCards { get; set; }
        public int AwayRedCards { get; set; }

        public int HomeYellowCards { get; set; }
        public int AwayYellowCards { get; set; }

    }
}