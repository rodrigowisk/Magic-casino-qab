using System;
using System.Text.Json.Serialization;

namespace Magic_casino_sportbook.DTOs
{
    public class SportsEventDto
    {
        [JsonPropertyName("externalId")]
        public string ExternalId { get; set; } = string.Empty;

        [JsonPropertyName("sportKey")]
        public string SportKey { get; set; } = string.Empty;

        [JsonPropertyName("homeTeam")]
        public string HomeTeam { get; set; } = string.Empty;

        [JsonPropertyName("awayTeam")]
        public string AwayTeam { get; set; } = string.Empty;

        // ✅ NOVOS CAMPOS DE URL (AGORA COMPLETOS)

        [JsonPropertyName("homeTeamLogo")]
        public string? HomeTeamLogo { get; set; }

        [JsonPropertyName("awayTeamLogo")]
        public string? AwayTeamLogo { get; set; }

        // 👇 ESTE ERA O QUE FALTAVA
        [JsonPropertyName("leagueLogo")]
        public string? LeagueLogo { get; set; }
        // 👆 -----------------------

        [JsonPropertyName("commenceTime")]
        public DateTime CommenceTime { get; set; }

        [JsonPropertyName("league")]
        public string League { get; set; } = string.Empty;

        [JsonPropertyName("rawOddsHome")]
        public decimal? RawOddsHome { get; set; }

        [JsonPropertyName("rawOddsDraw")]
        public decimal? RawOddsDraw { get; set; }

        [JsonPropertyName("rawOddsAway")]
        public decimal? RawOddsAway { get; set; }
    }
}