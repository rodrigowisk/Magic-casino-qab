using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Magic_casino_sportbook.Services
{
    public class LiveSportService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiToken;
        private const string BASE_URL = "https://api.b365api.com";
        private readonly JsonSerializerOptions _jsonOptions;

        public LiveSportService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _apiToken = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, // Lê "SS" ou "ss"
                NumberHandling = JsonNumberHandling.AllowReadingFromString, // Lê "1" como int ou string
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
        }

        public async Task<List<string>> UpdateLiveGamesAsync(List<SportsEvent> liveGames, AppDbContext context)
        {
            var endedGameIds = new List<string>();
            if (!liveGames.Any()) return endedGameIds;

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var batches = liveGames.Chunk(10).ToList();

            foreach (var batch in batches)
            {
                // 1. HEARTBEAT: Atualiza LastUpdate para destravar a fila
                foreach (var game in batch)
                {
                    game.LastUpdate = DateTime.UtcNow;
                    context.Entry(game).State = EntityState.Modified;
                }

                var gameIds = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
                var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={gameIds}";

                try
                {
                    var response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(jsonString))
                        {
                            var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);

                            if (data != null && data.Results != null && data.Success == 1)
                            {
                                ProcessSoccerResults(data.Results, batch.ToList(), context, endedGameIds);
                            }
                        }
                    }
                    else if ((int)response.StatusCode == 429)
                    {
                        Console.WriteLine($"⛔ [LIMIT] 429 Detectado. Pausando 5s...");
                        await Task.Delay(5000);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro Update: {ex.Message}");
                }

                await Task.Delay(1200);
            }
            return endedGameIds;
        }

        private void ProcessSoccerResults(List<List<B365Packet>> results, List<SportsEvent> batchGames, AppDbContext context, List<string> endedGameIds)
        {
            foreach (var packets in results)
            {
                if (packets == null || !packets.Any()) continue;

                // 1. Identificar o Jogo (Procura FI no pacote EV ou em qualquer outro)
                string targetId = packets.FirstOrDefault(p => p.Type == "EV")?.Fi
                               ?? packets.FirstOrDefault(p => !string.IsNullOrEmpty(p.Fi))?.Fi;

                if (string.IsNullOrEmpty(targetId)) continue;

                var gameDb = batchGames.FirstOrDefault(g => g.ExternalId.Trim() == targetId.Trim());
                if (gameDb == null) continue;

                // Separa os pacotes
                var ev = packets.FirstOrDefault(p => p.Type == "EV");
                var logs = packets.Where(p => p.Type == "ST").ToList();

                if (ev != null)
                {
                    bool mudou = false;

                    // --- ⚽ 1. PLACAR (Campo SS) ---
                    // Ex: "0-1"
                    if (!string.IsNullOrEmpty(ev.Ss) && ev.Ss.Contains("-"))
                    {
                        if (gameDb.Score != ev.Ss)
                        {
                            Console.WriteLine($"⚽ [GOL] {gameDb.HomeTeam} {ev.Ss} (Antes: {gameDb.Score})");
                            gameDb.Score = ev.Ss;
                            mudou = true;
                        }
                    }
                    else if (gameDb.Score == null)
                    {
                        gameDb.Score = "0-0"; // Inicializa
                        mudou = true;
                    }

                    // --- ⏱️ 2. TEMPO (Campo TM ou Logs LA) ---
                    string novoTempo = "0'";

                    // Prioridade A: Campo TM se vier preenchido e diferente de 0
                    if (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0")
                    {
                        novoTempo = ev.Tm + "'";
                        // Se quiser ser preciso, adiciona segundos: if (ev.Ts != "0") ...
                    }
                    else
                    {
                        // Prioridade B: Caçar nos Logs (ST) se o TM falhou
                        // Ex: "38' - 2nd Yellow Card"
                        foreach (var log in logs)
                        {
                            if (!string.IsNullOrEmpty(log.La))
                            {
                                var match = Regex.Match(log.La, @"(\d+)'");
                                if (match.Success)
                                {
                                    novoTempo = match.Groups[1].Value + "'";
                                    break; // Pega o primeiro que achar
                                }
                            }
                        }
                    }

                    // Atualiza o tempo (evita zerar se já tiver tempo corrido)
                    if (gameDb.GameTime != novoTempo && novoTempo != "0'")
                    {
                        // Console.WriteLine($"⏱️ [TEMPO] {gameDb.HomeTeam}: {novoTempo}");
                        gameDb.GameTime = novoTempo;
                        mudou = true;
                    }

                    // --- 🏁 3. FIM DE JOGO ---
                    if (ev.Status == "3") // Status 3 = Encerrado
                    {
                        if (gameDb.Status != "Ended")
                        {
                            Console.WriteLine($"🏁 [FIM] {gameDb.HomeTeam} Encerrado.");
                            gameDb.Status = "Ended";
                            endedGameIds.Add(gameDb.ExternalId);
                            mudou = true;
                        }
                    }

                    if (mudou)
                    {
                        context.Entry(gameDb).State = EntityState.Modified;
                    }
                }

                // --- ODDS ---
                ProcessOdds(gameDb, packets, ref context);
            }
        }

        private void ProcessOdds(SportsEvent game, List<B365Packet> packets, ref AppDbContext context)
        {
            var odds = packets.Where(p => string.Equals(p.Type, "PA", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(p.Od));
            bool changed = false;

            foreach (var odd in odds)
            {
                decimal valor = ConvertFraction(odd.Od);
                if (valor <= 1.0m) continue;

                string n2 = (odd.N2 ?? "").Trim();
                string name = (odd.Na ?? "").ToLower().Trim();

                bool isHome = n2 == "1" || name == "1" || name.Contains("home") || name.Contains("casa");
                bool isAway = n2 == "2" || name == "2" || name.Contains("away") || name.Contains("fora");
                bool isDraw = n2 == "x" || name == "x" || name.Contains("draw") || name.Contains("empate");

                if (isHome && game.RawOddsHome != valor) { game.RawOddsHome = valor; changed = true; }
                else if (isAway && game.RawOddsAway != valor) { game.RawOddsAway = valor; changed = true; }
                else if (isDraw && game.RawOddsDraw != valor) { game.RawOddsDraw = valor; changed = true; }
            }

            if (changed) context.Entry(game).State = EntityState.Modified;
        }

        public async Task<List<string>> CheckForKickoffAsync(List<SportsEvent> games, AppDbContext context)
        {
            var newLiveIds = new List<string>();
            bool mudouAlgo = false;
            foreach (var g in games)
            {
                if (DateTime.UtcNow >= g.CommenceTime && g.Status != "Live")
                {
                    g.Status = "Live";
                    if (string.IsNullOrEmpty(g.Score)) g.Score = "0-0";
                    g.GameTime = "0'";
                    newLiveIds.Add(g.ExternalId);
                    context.Entry(g).State = EntityState.Modified;
                    mudouAlgo = true;
                }
            }
            if (mudouAlgo) await context.SaveChangesAsync();
            return newLiveIds;
        }

        private decimal ConvertFraction(string fraction)
        {
            if (string.IsNullOrEmpty(fraction) || fraction == "0/0") return 0m;
            try
            {
                if (fraction.Contains("/"))
                {
                    var parts = fraction.Split('/');
                    if (parts.Length == 2)
                        return (decimal.Parse(parts[0]) / decimal.Parse(parts[1])) + 1;
                }
                return decimal.Parse(fraction, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return 0m; }
        }
    }
}