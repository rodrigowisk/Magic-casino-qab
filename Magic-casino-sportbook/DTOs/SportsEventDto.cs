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

        [JsonPropertyName("homeTeamLogo")]
        public string? HomeTeamLogo { get; set; }

        [JsonPropertyName("awayTeamLogo")]
        public string? AwayTeamLogo { get; set; }

        [JsonPropertyName("leagueLogo")]
        public string? LeagueLogo { get; set; }

        [JsonPropertyName("countryCode")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("flagUrl")]
        public string? FlagUrl { get; set; }

        [JsonPropertyName("commenceTime")]
        public DateTime CommenceTime { get; set; }

        [JsonPropertyName("league")]
        public string League { get; set; } = string.Empty;

        // ✅ NOVOS CAMPOS INCLUÍDOS
        [JsonPropertyName("homeScore")]
        public int? HomeScore { get; set; }

        [JsonPropertyName("awayScore")]
        public int? AwayScore { get; set; }

        [JsonPropertyName("gameTime")]
        public string? GameTime { get; set; } // Ex: "45", "FT", "HT"

        [JsonPropertyName("status")]
        public string? Status { get; set; } // Ex: "Live", "Prematch", "Ended"

        [JsonPropertyName("rawOddsHome")]
        public decimal? RawOddsHome { get; set; }

        [JsonPropertyName("rawOddsDraw")]
        public decimal? RawOddsDraw { get; set; }

        [JsonPropertyName("rawOddsAway")]
        public decimal? RawOddsAway { get; set; }
    }
}