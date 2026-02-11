using System.Text.Json;
using System.Globalization;

namespace Magic_casino_sportbook.Services.Parsers
{
    public class GoalsParser : IMarketParser
    {
        public List<MarketDto> Parse(JsonElement root, string sportKey)
        {
            var list = new List<MarketDto>();

            // 1. FUTEBOL (Bloco 'goals')
            if (root.TryGetProperty("goals", out var goalsSec) && goalsSec.TryGetProperty("sp", out var spGoals))
            {
                ProcessOverUnder(spGoals, "goals_over_under", "Total de Gols", list);
                ProcessOverUnder(spGoals, "alternative_total_goals", "Total de Gols (Alt)", list);

                // Adicione aqui se houver escanteios nesse bloco (comum na BetsAPI)
                ProcessOverUnder(spGoals, "corners_over_under", "Escanteios", list);
                ProcessOverUnder(spGoals, "alternative_corners", "Escanteios (Alt)", list);
            }

            // 2. BASQUETE / TÊNIS / VÔLEI
            if (root.TryGetProperty("main", out var mainSec) && mainSec.TryGetProperty("sp", out var spMain))
            {
                ProcessOverUnder(spMain, "total_points", "Total de Pontos", list);
                ProcessOverUnder(spMain, "total_match_points", "Total de Pontos", list);
                ProcessOverUnder(spMain, "total", "Total", list);
            }

            return list;
        }

        private void ProcessOverUnder(JsonElement sp, string jsonKey, string marketName, List<MarketDto> list)
        {
            if (sp.TryGetProperty(jsonKey, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    string name = GetStr(item, "name"); // O VALOR DA LINHA (Ex: 2.5)
                    string header = GetStr(item, "header"); // "Over" ou "Under"

                    if (string.IsNullOrEmpty(header) && (name.ToLower().Contains("over") || name.ToLower().Contains("under")))
                    {
                        header = name;
                    }

                    if (HasOdds(item))
                    {
                        // ✅ CORREÇÃO: Converte o "name" (2.5) para decimal
                        decimal? handicapVal = null;
                        if (decimal.TryParse(name, NumberStyles.Any, CultureInfo.InvariantCulture, out var hVal))
                        {
                            handicapVal = hVal;
                        }

                        list.Add(new MarketDto
                        {
                            ExternalId = GetStr(item, "id"),
                            MarketName = marketName,
                            OutcomeName = $"{header} {name}".Trim(),
                            Price = ParseDec(item, "odds"),
                            Handicap = handicapVal // <--- AQUI ESTAVA FALTANDO!
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