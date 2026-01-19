using System.Text.Json;
using System.Globalization;

namespace Magic_casino_sportbook.Services.Parsers
{
    public class MainParser : IMarketParser
    {
        public List<MarketDto> Parse(JsonElement root, string sportKey)
        {
            var list = new List<MarketDto>();

            if (root.TryGetProperty("main", out var main) && main.TryGetProperty("sp", out var sp))
            {
                // 1. FUTEBOL E PADRÃO (1X2)
                ExtractOdds(sp, "full_time_result", "Resultado Final", list);
                ExtractOdds(sp, "double_chance", "Dupla Hipótese", list);
                ExtractOdds(sp, "both_teams_to_score", "Ambos Marcam", list);

                // 2. BASQUETE / VÔLEI / TÊNIS (Lógica Inteligente para Game Lines)
                // O JSON do basquete vem misturado em "game_lines". Precisamos filtrar.
                if (sp.TryGetProperty("game_lines", out var glArray) && glArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in glArray.EnumerateArray())
                    {
                        var oddsStr = GetStr(item, "odds");
                        var header = GetStr(item, "header");     // Nome do Time
                        var handicap = GetStr(item, "handicap"); // Handicap (+11.5, etc)
                        var name = GetStr(item, "name");         // Nome do mercado (as vezes vem vazio)

                        // Se tem odd válida
                        if (decimal.TryParse(oddsStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) && price > 1)
                        {
                            // CASO 1: É Money Line (Vencedor)
                            // Regra: Tem time (header), tem odd, mas NÃO TEM handicap e NÃO É Total
                            if (!string.IsNullOrEmpty(header) && string.IsNullOrEmpty(handicap) && !name.Contains("Total"))
                            {
                                list.Add(new MarketDto
                                {
                                    ExternalId = GetStr(item, "id"),
                                    MarketName = "Vencedor da Partida", // Padronizamos o nome
                                    OutcomeName = header, // O resultado é o nome do time
                                    Price = price,
                                    Header = header
                                });
                            }

                            // CASO 2: É Handicap (Spread) - Opcional, se quiser salvar aqui também
                            // Se tem handicap numérico (ex: +11.5)
                            else if (!string.IsNullOrEmpty(header) && !string.IsNullOrEmpty(handicap) && IsHandicap(handicap))
                            {
                                list.Add(new MarketDto
                                {
                                    ExternalId = GetStr(item, "id"),
                                    MarketName = "Handicap de Jogo",
                                    OutcomeName = $"{header} {handicap}",
                                    Price = price,
                                    Header = header,
                                    Handicap = ParseDec(item, "handicap")
                                });
                            }
                        }
                    }
                }

                // 3. Fallbacks (Outros formatos raros)
                ExtractOdds(sp, "money_line", "Vencedor da Partida", list);
                ExtractOdds(sp, "to_win_match", "Vencedor da Partida", list);
            }

            // TÊNIS (Às vezes vem no schedule main)
            if (root.TryGetProperty("schedule", out var sched) && sched.TryGetProperty("sp", out var spSched))
            {
                if (spSched.TryGetProperty("main", out var schedArr))
                {
                    foreach (var item in schedArr.EnumerateArray())
                    {
                        if (GetStr(item, "name") == "Money Line")
                        {
                            // No schedule não tem o nome do time no header, tem que inferir (1=Casa, 2=Fora)
                            // Isso é complexo sem o contexto do jogo, mas o 'game_lines' acima resolve 99% dos casos.
                        }
                    }
                }
            }

            return list;
        }

        private void ExtractOdds(JsonElement sp, string jsonKey, string marketName, List<MarketDto> list)
        {
            if (sp.TryGetProperty(jsonKey, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    string outcome = GetStr(item, "name");
                    string header = GetStr(item, "header");
                    if (!string.IsNullOrEmpty(header) && header.Length > 1) outcome = header;

                    var priceStr = GetStr(item, "odds");
                    if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                    {
                        list.Add(new MarketDto
                        {
                            ExternalId = GetStr(item, "id"),
                            MarketName = marketName,
                            OutcomeName = outcome,
                            Price = price,
                            Header = header
                        });
                    }
                }
            }
        }

        private bool IsHandicap(string s) => s.Contains("+") || s.Contains("-");

        private string GetStr(JsonElement el, string key)
            => el.TryGetProperty(key, out var p) ? p.GetString() ?? "" : "";

        private decimal ParseDec(JsonElement el, string key) { if (el.TryGetProperty(key, out var p) && decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v; return 0; }
    }
}