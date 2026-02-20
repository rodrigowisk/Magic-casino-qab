using System.Text.Json;
using Magic_casino_sportbook.Models;

namespace Magic_casino_sportbook.Services.Parsers
{
    public class GameStatsParser
    {
        public void UpdateGameDetails(JsonElement item, SportsEvent game)
        {
            // 1. PLACAR (ss)
            var score = GetStringSafe(item, "ss");
            if (!string.IsNullOrEmpty(score)) game.Score = score;

            // 2. TEMPO (tm)
            var minute = GetStringSafe(item, "tm");
            if (!string.IsNullOrEmpty(minute)) game.GameTime = minute + "'";

            // 3. ESTATÍSTICAS COMPLETAS
            if (item.TryGetProperty("stats", out var stats) && stats.ValueKind == JsonValueKind.Object)
            {
                // Cartões (Já tinhamos)
                ParseStat(stats, "redcards", (h, a) => { game.HomeRedCards = h; game.AwayRedCards = a; });
                ParseStat(stats, "yellowcards", (h, a) => { game.HomeYellowCards = h; game.AwayYellowCards = a; });

                // Escanteios
                ParseStat(stats, "corners", (h, a) => { game.HomeCorners = h; game.AwayCorners = a; });

                // Ataques
                ParseStat(stats, "attacks", (h, a) => { game.HomeAttacks = h; game.AwayAttacks = a; });
                ParseStat(stats, "dangerous_attacks", (h, a) => { game.HomeDangerousAttacks = h; game.AwayDangerousAttacks = a; });

                // Chutes (No Gol e Fora)
                ParseStat(stats, "on_target", (h, a) => { game.HomeOnTarget = h; game.AwayOnTarget = a; });
                ParseStat(stats, "off_target", (h, a) => { game.HomeOffTarget = h; game.AwayOffTarget = a; });

                // Posse de Bola
                ParseStat(stats, "possession_rt", (h, a) => { game.HomePossession = h; game.AwayPossession = a; });
            }
        }

        // --- Helpers Genéricos ---

        // Método inteligente para ler arrays ["Home", "Away"] do JSON e aplicar no objeto
        private void ParseStat(JsonElement statsNode, string key, Action<int, int> apply)
        {
            if (statsNode.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() >= 2)
            {
                int homeVal = TryParseIntSafe(arr[0]);
                int awayVal = TryParseIntSafe(arr[1]);
                apply(homeVal, awayVal);
            }
        }

        private string? GetStringSafe(JsonElement el, string key)
        {
            if (el.TryGetProperty(key, out var p))
            {
                return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
            }
            return null;
        }

        private int TryParseIntSafe(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Number) return el.GetInt32();
            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out int val)) return val;
            return 0;
        }
    }
}