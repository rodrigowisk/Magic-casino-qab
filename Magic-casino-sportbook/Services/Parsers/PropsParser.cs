using System.Text.Json;
using System.Globalization;

namespace Magic_casino_sportbook.Services.Parsers
{
    public class PropsParser : IMarketParser
    {
        public List<MarketDto> Parse(JsonElement root, string sportKey)
        {
            var list = new List<MarketDto>();

            // 1. Bloco PLAYER (Marcadores)
            if (root.TryGetProperty("player", out var playerBlock) && playerBlock.TryGetProperty("sp", out var spPlayer))
            {
                if (spPlayer.TryGetProperty("goalscorers", out var scorers))
                {
                    foreach (var item in scorers.EnumerateArray())
                    {
                        string playerName = GetStr(item, "name");
                        string type = GetStr(item, "header");

                        if (type == "Anytime") type = "A Qualquer Momento";
                        if (type == "First") type = "Primeiro a Marcar";
                        if (type == "Last") type = "Último a Marcar";

                        // Jogador não tem handicap numérico, passa null
                        AddOdd(item, "Marcadores de Gol", $"{playerName} ({type})", list, null);
                    }
                }
            }

            // 2. Bloco CARDS (Cartões)
            if (root.TryGetProperty("cards", out var cardsBlock) && cardsBlock.TryGetProperty("sp", out var spCards))
            {
                if (spCards.TryGetProperty("number_of_cards_in_match", out var numCards))
                {
                    foreach (var item in numCards.EnumerateArray())
                    {
                        string line = GetStr(item, "name"); // "4.5"
                        string type = GetStr(item, "header"); // "Over", "Under"

                        // ✅ CORREÇÃO: Converte a linha para decimal
                        decimal? handicapVal = null;
                        if (decimal.TryParse(line, NumberStyles.Any, CultureInfo.InvariantCulture, out var hVal))
                        {
                            handicapVal = hVal;
                        }

                        AddOdd(item, "Total de Cartões", $"{type} {line}", list, handicapVal);
                    }
                }
            }

            return list;
        }

        // ✅ Método auxiliar atualizado para aceitar Handicap
        private void AddOdd(JsonElement item, string market, string outcome, List<MarketDto> list, decimal? handicap)
        {
            var priceStr = GetStr(item, "odds");
            if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            {
                list.Add(new MarketDto
                {
                    ExternalId = GetStr(item, "id"),
                    MarketName = market,
                    OutcomeName = outcome,
                    Price = price,
                    Handicap = handicap // <--- SALVA NO BANCO
                });
            }
        }

        private string GetStr(JsonElement el, string key)
            => el.TryGetProperty(key, out var p) ? p.GetString() ?? "" : "";
    }
}