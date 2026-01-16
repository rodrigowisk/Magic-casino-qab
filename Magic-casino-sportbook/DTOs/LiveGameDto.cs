using System;
using System.Text.Json.Serialization; // 👈 OBRIGATÓRIO

namespace Magic_casino_sportbook.DTOs
{
    public class LiveGameDto
    {
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

        // 💰 CORREÇÃO DO AO VIVO:
        // O Vue espera 'homeOdd', não 'RawOddsHome'. Nós traduzimos aqui!
        [JsonPropertyName("homeOdd")]
        public decimal RawOddsHome { get; set; }

        [JsonPropertyName("drawOdd")]
        public decimal RawOddsDraw { get; set; }

        [JsonPropertyName("awayOdd")]
        public decimal RawOddsAway { get; set; }
    }
}