using System.Text.Json;
using Magic_casino_sportbook.Data.Models; // Ajuste para seu namespace de Models

namespace Magic_casino_sportbook.Services.Parsers
{
    // Modelo auxiliar para trafegar dados antes de salvar no banco
    public class MarketDto
    {
        public string ExternalId { get; set; }
        public string MarketName { get; set; }   // Ex: "Resultado Final", "Total de Gols"
        public string OutcomeName { get; set; }  // Ex: "Casa", "Over 2.5"
        public decimal Price { get; set; }       // Ex: 1.90
        public string Header { get; set; }       // Ex: "Brighton" (Útil para saber quem é o time)
        public decimal? Handicap { get; set; }   // Ex: -0.5 (Para Asian)
    }

    public interface IMarketParser
    {
        // Retorna uma lista de odds encontradas naquele bloco do JSON
        List<MarketDto> Parse(JsonElement root, string sportKey);
    }
}