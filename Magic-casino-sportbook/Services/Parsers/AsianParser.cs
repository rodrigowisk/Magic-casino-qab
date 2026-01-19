using System.Text.Json;
using System.Globalization;

namespace Magic_casino_sportbook.Services.Parsers
{
    public class AsianParser : IMarketParser
    {
        public List<MarketDto> Parse(JsonElement root, string sportKey)
        {
            var list = new List<MarketDto>();

            // 1. Bloco ASIAN_LINES (Futebol)
            if (root.TryGetProperty("asian_lines", out var asianSec) && asianSec.TryGetProperty("sp", out var spAsian))
            {
                ProcessHandicap(spAsian, "asian_handicap", "Handicap Asiático", list);
            }

            // 2. Bloco MAIN (Basquete/Vôlei usam 'handicap' ou 'spread' dentro de main)
            if (root.TryGetProperty("main", out var mainSec) && mainSec.TryGetProperty("sp", out var spMain))
            {
                ProcessHandicap(spMain, "handicap", "Handicap", list);
                ProcessHandicap(spMain, "spread", "Handicap (Spread)", list);
                ProcessHandicap(spMain, "game_handicap", "Handicap de Jogo", list);
                ProcessHandicap(spMain, "set_handicap", "Handicap de Sets", list); // Tênis/Vôlei
            }

            return list;
        }

        private void ProcessHandicap(JsonElement sp, string jsonKey, string marketName, List<MarketDto> list)
        {
            if (sp.TryGetProperty(jsonKey, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    var handicap = GetStr(item, "handicap");
                    if (string.IsNullOrEmpty(handicap)) handicap = GetStr(item, "name"); // As vezes vem no name

                    var team = GetStr(item, "header");

                    if (decimal.TryParse(GetStr(item, "odds"), NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                    {
                        list.Add(new MarketDto
                        {
                            ExternalId = GetStr(item, "id"),
                            MarketName = marketName,
                            OutcomeName = $"{team} {handicap}".Trim(),
                            Price = price,
                            Handicap = decimal.TryParse(handicap, NumberStyles.Any, CultureInfo.InvariantCulture, out var h) ? h : null
                        });
                    }
                }
            }
        }

        private string GetStr(JsonElement el, string key) => el.TryGetProperty(key, out var p) ? p.GetString() ?? "" : "";
    }
}