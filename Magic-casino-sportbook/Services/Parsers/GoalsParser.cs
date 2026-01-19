using System.Text.Json;
using System.Globalization;

namespace Magic_casino_sportbook.Services.Parsers
{
    public class GoalsParser : IMarketParser
    {
        public List<MarketDto> Parse(JsonElement root, string sportKey)
        {
            var list = new List<MarketDto>();

            // A API separa: 'goals' (futebol) e 'main' (basquete points)
            // Vamos procurar em ambos os lugares

            // 1. FUTEBOL (Bloco 'goals')
            if (root.TryGetProperty("goals", out var goalsSec) && goalsSec.TryGetProperty("sp", out var spGoals))
            {
                ProcessOverUnder(spGoals, "goals_over_under", "Total de Gols", list);
                ProcessOverUnder(spGoals, "alternative_total_goals", "Total de Gols (Alt)", list);
            }

            // 2. BASQUETE / TÊNIS / VÔLEI (Muitas vezes está dentro de 'main' como 'total_points')
            if (root.TryGetProperty("main", out var mainSec) && mainSec.TryGetProperty("sp", out var spMain))
            {
                ProcessOverUnder(spMain, "total_points", "Total de Pontos", list);
                ProcessOverUnder(spMain, "total_match_points", "Total de Pontos", list);
                ProcessOverUnder(spMain, "total", "Total", list); // Genérico
            }

            return list;
        }

        private void ProcessOverUnder(JsonElement sp, string jsonKey, string marketName, List<MarketDto> list)
        {
            if (sp.TryGetProperty(jsonKey, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    string name = GetStr(item, "name"); // Valor da linha (Ex: 2.5, 150.5)
                    string header = GetStr(item, "header"); // "Over" ou "Under"

                    if (string.IsNullOrEmpty(header) && (name.ToLower().Contains("over") || name.ToLower().Contains("under")))
                    {
                        header = name; // Às vezes vem invertido
                    }

                    if (HasOdds(item))
                    {
                        list.Add(new MarketDto
                        {
                            ExternalId = GetStr(item, "id"),
                            MarketName = marketName,
                            OutcomeName = $"{header} {name}".Trim(),
                            Price = ParseDec(item, "odds")
                        });
                    }
                }
            }
        }

        private bool HasOdds(JsonElement el) => el.TryGetProperty("odds", out var p) && !string.IsNullOrEmpty(p.GetString());
        private string GetStr(JsonElement el, string key) => el.TryGetProperty(key, out var p) ? p.GetString() ?? "" : "";
        private decimal ParseDec(JsonElement el, string key) { if (el.TryGetProperty(key, out var p) && decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v; return 0; }
    }
}