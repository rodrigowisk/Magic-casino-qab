using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Magic_casino_tournament.Models
{
    // DTO para mapear a resposta que vem do Sportbook
    public class SportbookGameDto
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public string Score { get; set; } = string.Empty; // Ex: "2-1"

        [JsonPropertyName("homeTeam")]
        public string HomeTeam { get; set; } = string.Empty;

        [JsonPropertyName("awayTeam")]
        public string AwayTeam { get; set; } = string.Empty;

        // As odds de resumo (que vêm zeradas em alguns jogos)
        [JsonPropertyName("rawOddsHome")]
        public decimal RawOddsHome { get; set; }

        [JsonPropertyName("rawOddsDraw")]
        public decimal RawOddsDraw { get; set; }

        [JsonPropertyName("rawOddsAway")]
        public decimal RawOddsAway { get; set; }

        // 🚨 ESTA É A PARTE CRÍTICA QUE FALTAVA 🚨
        // Sem isso, o TournamentService não consegue ler a lista de odds do JSON
        // e o fallback falha, causando o erro "Cotação indisponível".
        [JsonPropertyName("odds")]
        public List<SportbookMarketDto> Odds { get; set; } = new List<SportbookMarketDto>();
    }

    public class SportbookMarketDto
    {
        [JsonPropertyName("marketName")]
        public string MarketName { get; set; } = string.Empty;

        [JsonPropertyName("outcomeName")]
        public string OutcomeName { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }
    }
}