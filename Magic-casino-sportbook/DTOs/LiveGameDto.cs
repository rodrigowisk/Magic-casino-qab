using System;
using System.Text.Json.Serialization;

namespace Magic_casino_sportbook.DTOs
{
    public class LiveGameDto
    {
        // 🔴 ANTES: [JsonPropertyName("externalId")]
        // 🟢 AGORA: "gameId" (para bater com o Vue)
        [JsonPropertyName("gameId")]
        public string GameId { get; set; } = string.Empty;

        [JsonPropertyName("sportKey")]
        public string SportKey { get; set; } = string.Empty;

        [JsonPropertyName("homeTeam")]
        public string HomeTeam { get; set; } = string.Empty;

        [JsonPropertyName("awayTeam")]
        public string AwayTeam { get; set; } = string.Empty;

        [JsonPropertyName("league")]
        public string League { get; set; } = string.Empty;

        [JsonPropertyName("homeTeamLogo")]
        public string? HomeTeamLogo { get; set; }

        [JsonPropertyName("awayTeamLogo")]
        public string? AwayTeamLogo { get; set; }

        [JsonPropertyName("countryCode")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("flagUrl")]
        public string? FlagUrl { get; set; }

        [JsonPropertyName("commenceTime")]
        public DateTime CommenceTime { get; set; }

        [JsonPropertyName("homeScore")]
        public int HomeScore { get; set; }

        [JsonPropertyName("awayScore")]
        public int AwayScore { get; set; }

        [JsonPropertyName("currentMinute")]
        public string CurrentMinute { get; set; } = string.Empty;

        [JsonPropertyName("period")]
        public string Period { get; set; } = string.Empty;

        [JsonPropertyName("homeOdd")]
        public decimal RawOddsHome { get; set; }

        [JsonPropertyName("drawOdd")] // Se estiver rawOddsDraw, mude para drawOdd
        public decimal RawOddsDraw { get; set; }

        [JsonPropertyName("awayOdd")]
        public decimal RawOddsAway { get; set; }

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

    }
}