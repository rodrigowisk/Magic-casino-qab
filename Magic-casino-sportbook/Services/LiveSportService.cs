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
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
        }

        // ==========================================================================================
        // 🚀 ATUALIZAR LIVE (Com Lógica de Economia Inteligente)
        // ==========================================================================================
        public async Task<List<string>> UpdateLiveGamesAsync(List<SportsEvent> liveGames, AppDbContext context)
        {
            var endedGameIds = new List<string>();
            if (!liveGames.Any()) return endedGameIds;

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            // ✅ VOLTAMOS PARA O LOTE DE 10 (Economia)
            var batches = liveGames.Chunk(10).ToList();

            foreach (var batch in batches)
            {
                // Heartbeat
                foreach (var game in batch)
                {
                    game.LastUpdate = DateTime.UtcNow;
                    context.Entry(game).Property(x => x.LastUpdate).IsModified = true;
                }

                var gameIds = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
                var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={gameIds}";

                try
                {
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();

                        // 🚨 CENÁRIO DE ERRO: Lote contaminado
                        if (jsonString.Contains("PARAM_INVALID"))
                        {
                            Console.WriteLine($"⚠️ [RESGATE] Lote contaminado ({batch.Length} jogos). Iniciando varredura individual...");

                            // 🔍 ESTRATÉGIA DE RESGATE: Verifica 1 por 1 só desse lote
                            await ProcessIndividualFallbackAsync(client, batch, context, endedGameIds);
                        }
                        else if (!string.IsNullOrWhiteSpace(jsonString))
                        {
                            // CENÁRIO FELIZ: Lote processado com 1 requisição
                            var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
                            if (data != null && data.Results != null && data.Success == 1)
                            {
                                DispatchUpdates(data.Results, batch.ToList(), context, endedGameIds);
                            }
                        }
                    }
                    else if ((int)response.StatusCode == 429)
                    {
                        Console.WriteLine($"⛔ [LIMIT] 429 Detectado. Pausando 2s...");
                        await Task.Delay(2000);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro Update: {ex.Message}");
                }

                await Task.Delay(1000); // Delay entre lotes
            }
            return endedGameIds;
        }

        // ==========================================================================================
        // 🚑 MÉTODO DE RESGATE (Executado apenas quando o lote falha)
        // ==========================================================================================
        private async Task ProcessIndividualFallbackAsync(HttpClient client, SportsEvent[] batch, AppDbContext context, List<string> endedGameIds)
        {
            foreach (var game in batch)
            {
                var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={game.ExternalId}";
                try
                {
                    var response = await client.GetAsync(url);
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // 💀 ACHEI O CULPADO!
                    if (jsonString.Contains("PARAM_INVALID"))
                    {
                        Console.WriteLine($"🗑️ [LIMPEZA] Jogo inválido removido: {game.ExternalId} ({game.HomeTeam})");

                        // Marca como encerrado para sair da lista Live e parar de travar o sistema
                        game.Status = "Ended";
                        context.Entry(game).State = EntityState.Modified;
                        endedGameIds.Add(game.ExternalId);
                    }
                    else
                    {
                        // Jogo inocente: Processa ele normalmente
                        var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
                        if (data != null && data.Results != null)
                        {
                            DispatchUpdates(data.Results, new List<SportsEvent> { game }, context, endedGameIds);
                        }
                    }
                }
                catch
                {
                    // Ignora erro individual para não travar o resgate
                }

                await Task.Delay(200); // Delay curto para o resgate não demorar tanto
            }
        }

        // ==========================================================================================
        // 🕵️ HOTZONE (Verifica Início) - Mesma lógica de economia
        // ==========================================================================================
        public async Task<List<string>> VerifyKickoffWithApiAsync(List<SportsEvent> candidates, IServiceProvider sp)
        {
            var newLiveIds = new List<string>();
            if (!candidates.Any()) return newLiveIds;

            using var scope = sp.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.AttachRange(candidates);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var batches = candidates.Chunk(10).ToList();

            foreach (var batch in batches)
            {
                var gameIds = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
                var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={gameIds}";

                try
                {
                    var response = await client.GetAsync(url);
                    var jsonString = await response.Content.ReadAsStringAsync();

                    if (jsonString.Contains("PARAM_INVALID"))
                    {
                        // Se falhar no HotZone, não precisamos ser tão agressivos. 
                        // Apenas logamos e pulamos, pois o jogo inválido provavelmente é um Prematch que foi cancelado.
                        // Mas para garantir que jogos bons entrem, fazemos um fallback simples:
                        Console.WriteLine($"⚠️ [HOTZONE] Lote com ID ruim. Verificando individualmente...");

                        foreach (var g in batch)
                        {
                            var indUrl = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={g.ExternalId}";
                            var indResp = await client.GetAsync(indUrl);
                            var indJson = await indResp.Content.ReadAsStringAsync();

                            if (!indJson.Contains("PARAM_INVALID"))
                            {
                                ProcessHotZonePacket(indJson, new List<SportsEvent> { g }, context, newLiveIds);
                            }
                            else
                            {
                                // Marca como cancelado para sair da lista de verificação
                                g.Status = "Cancelled";
                                context.Entry(g).State = EntityState.Modified;
                            }
                            await Task.Delay(200);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(jsonString))
                    {
                        ProcessHotZonePacket(jsonString, batch.ToList(), context, newLiveIds);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ HotZone Check Erro: {ex.Message}");
                }

                await context.SaveChangesAsync();
                await Task.Delay(1000);
            }

            return newLiveIds;
        }

        private void ProcessHotZonePacket(string jsonString, List<SportsEvent> games, AppDbContext context, List<string> newLiveIds)
        {
            var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
            if (data != null && data.Results != null)
            {
                foreach (var packetList in data.Results)
                {
                    var ev = packetList.FirstOrDefault(p => p.Type == "EV");
                    if (ev == null) continue;

                    var gameDb = games.FirstOrDefault(g => g.ExternalId == ev.Fi);
                    if (gameDb == null) continue;

                    // LÓGICA DE STATUS: 1 = In Play
                    bool isLive = (ev.TimeStatus == "1") || (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0");

                    if (isLive)
                    {
                        gameDb.Status = "Live";
                        if (string.IsNullOrEmpty(gameDb.Score)) gameDb.Score = "0-0";
                        if (string.IsNullOrEmpty(gameDb.GameTime)) gameDb.GameTime = "0'";

                        // Prioridade máxima
                        gameDb.LastUpdate = DateTime.UtcNow.AddMinutes(-10);

                        context.Entry(gameDb).State = EntityState.Modified;
                        newLiveIds.Add(gameDb.ExternalId);
                    }
                }
            }
        }

        // ==========================================================================================
        // 🚦 DISPATCHER (Mantido igual)
        // ==========================================================================================
        private void DispatchUpdates(List<List<B365Packet>> results, List<SportsEvent> batchGames, AppDbContext context, List<string> endedGameIds)
        {
            foreach (var packets in results)
            {
                if (packets == null || !packets.Any()) continue;

                string targetId = packets.FirstOrDefault(p => p.Type == "EV")?.Fi
                               ?? packets.FirstOrDefault(p => !string.IsNullOrEmpty(p.Fi))?.Fi;

                if (string.IsNullOrEmpty(targetId)) continue;

                var gameDb = batchGames.FirstOrDefault(g => g.ExternalId.Trim() == targetId.Trim());
                if (gameDb == null) continue;

                var ev = packets.FirstOrDefault(p => p.Type == "EV");
                var logs = packets.Where(p => p.Type == "ST").ToList();

                if (ev != null)
                {
                    bool mudou = false;
                    string sportKey = (gameDb.SportKey ?? "").ToLower();

                    if (sportKey.Contains("soccer") || sportKey.Contains("futebol"))
                        mudou = UpdateSoccer(gameDb, ev, logs);
                    else if (sportKey.Contains("basketball") || sportKey.Contains("basquete") || sportKey.Contains("nba"))
                        mudou = UpdateBasketball(gameDb, ev);
                    else if (sportKey.Contains("tennis") || sportKey.Contains("tenis") || sportKey.Contains("table"))
                        mudou = UpdateTennis(gameDb, ev);
                    else if (sportKey.Contains("volleyball") || sportKey.Contains("volei"))
                        mudou = UpdateVolleyball(gameDb, ev);
                    else
                        mudou = UpdateGenericSport(gameDb, ev);

                    // Verifica Fim de Jogo
                    if (ev.TimeStatus == "3" || ev.Status == "3")
                    {
                        bool jogoValido = (!string.IsNullOrEmpty(gameDb.Score) && gameDb.Score != "0-0") ||
                                          (!string.IsNullOrEmpty(gameDb.GameTime) && gameDb.GameTime != "0'");

                        if (gameDb.Status != "Ended" && jogoValido)
                        {
                            Console.WriteLine($"🏁 [FIM] {gameDb.HomeTeam} ({sportKey}) Encerrado pela API.");
                            gameDb.Status = "Ended";
                            endedGameIds.Add(gameDb.ExternalId);
                            mudou = true;
                        }
                    }

                    if (mudou) context.Entry(gameDb).State = EntityState.Modified;
                }

                ProcessOdds(gameDb, packets, ref context);
            }
        }

        // --- MÉTODOS DE PARSE (Mantenha os que você já tem abaixo: UpdateSoccer, etc) ---
        // (Estou colando os mesmos para manter o arquivo completo e funcional)

        private bool UpdateSoccer(SportsEvent game, B365Packet ev, List<B365Packet> logs)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(ev.Ss) && ev.Ss.Contains("-"))
            {
                if (game.Score != ev.Ss)
                {
                    Console.WriteLine($"⚽ [GOL] {game.HomeTeam}: {ev.Ss}");
                    game.Score = ev.Ss; changed = true;
                }
            }
            else if (string.IsNullOrEmpty(game.Score)) { game.Score = "0-0"; changed = true; }

            string novoTempo = "0'";
            if (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0") novoTempo = ev.Tm + "'";
            else
            {
                foreach (var log in logs)
                {
                    if (!string.IsNullOrEmpty(log.La))
                    {
                        var match = Regex.Match(log.La, @"(\d+)'");
                        if (match.Success) { novoTempo = match.Groups[1].Value + "'"; break; }
                    }
                }
            }

            if (game.GameTime != novoTempo && novoTempo != "0'") { game.GameTime = novoTempo; changed = true; }
            return changed;
        }

        private bool UpdateBasketball(SportsEvent game, B365Packet ev)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(ev.Ss) && game.Score != ev.Ss) { game.Score = ev.Ss; changed = true; }
            if (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0")
            {
                string t = ev.Tm + "'"; if (game.GameTime != t) { game.GameTime = t; changed = true; }
            }
            return changed;
        }

        private bool UpdateTennis(SportsEvent game, B365Packet ev)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(ev.Ss) && game.Score != ev.Ss) { game.Score = ev.Ss; changed = true; }
            return changed;
        }

        private bool UpdateVolleyball(SportsEvent game, B365Packet ev)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(ev.Ss) && game.Score != ev.Ss) { game.Score = ev.Ss; changed = true; }
            return changed;
        }

        private bool UpdateGenericSport(SportsEvent game, B365Packet ev)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(ev.Ss) && game.Score != ev.Ss) { game.Score = ev.Ss; changed = true; }
            if (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0")
            {
                string t = ev.Tm + "'"; if (game.GameTime != t) { game.GameTime = t; changed = true; }
            }
            return changed;
        }

        private void ProcessOdds(SportsEvent game, List<B365Packet> packets, ref AppDbContext context)
        {
            var odds = packets.Where(p => (p.Type == "PA" || p.Type == "MA") && !string.IsNullOrEmpty(p.Od));
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
            return await Task.FromResult(new List<string>());
        }

        private decimal ConvertFraction(string fraction)
        {
            if (string.IsNullOrEmpty(fraction) || fraction == "0/0") return 0m;
            try
            {
                if (fraction.Contains("/"))
                {
                    var parts = fraction.Split('/');
                    if (parts.Length == 2) return (decimal.Parse(parts[0]) / decimal.Parse(parts[1])) + 1;
                }
                return decimal.Parse(fraction, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return 0m; }
        }
    }
}