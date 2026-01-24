using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Hubs; // ✅ NECESSÁRIO PARA O SIGNALR
using Magic_casino_sportbook.Models;
using Microsoft.AspNetCore.SignalR; // ✅ NECESSÁRIO PARA O SIGNALR
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
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

        // ✅ DEPENDÊNCIA: REDIS
        private readonly IDatabase _redisDb;

        // ✅ NOVA DEPENDÊNCIA: SIGNALR (O Mensageiro)
        private readonly IHubContext<GameHub> _hubContext;

        // 🔒 O PORTEIRO GLOBAL (SEMAPHORE):
        private static readonly SemaphoreSlim _apiGate = new SemaphoreSlim(1, 1);

        // ✅ CONSTRUTOR ATUALIZADO (Recebe Redis + SignalR)
        public LiveSportService(
            IHttpClientFactory httpClientFactory,
            IConnectionMultiplexer redis,
            IHubContext<GameHub> hubContext)
        {
            _httpClientFactory = httpClientFactory;
            _hubContext = hubContext; // Injeta o SignalR
            _apiToken = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";

            // Pega o banco de dados do Redis
            _redisDb = redis.GetDatabase();

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
        }

        // ==========================================================================================
        // 🚀 ATUALIZAR LIVE (Híbrido: Redis + SQL + SignalR)
        // ==========================================================================================
        public async Task<List<string>> UpdateLiveGamesAsync(List<SportsEvent> liveGames, AppDbContext context)
        {
            var endedGameIds = new List<string>();
            if (!liveGames.Any()) return endedGameIds;

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var batches = liveGames.Chunk(10).ToList();

            foreach (var batch in batches)
            {
                // Heartbeat SQL (Mantido apenas para rotação da fila do Worker)
                // Isso gera um UPDATE leve, mas não grava as odds no disco toda hora.
                foreach (var game in batch)
                {
                    game.LastUpdate = DateTime.UtcNow;
                    context.Entry(game).Property(x => x.LastUpdate).IsModified = true;
                }

                var gameIds = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
                var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={gameIds}";

                // 🔒 ENTRA NA FILA
                await _apiGate.WaitAsync();
                try
                {
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();

                        if (jsonString.Contains("PARAM_INVALID"))
                        {
                            Console.WriteLine($"⚠️ [RESGATE] Lote contaminado. Verificando individualmente...");
                            await ProcessIndividualFallbackAsync(client, batch, context, endedGameIds);
                        }
                        else if (!string.IsNullOrWhiteSpace(jsonString))
                        {
                            var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
                            if (data != null && data.Results != null && data.Success == 1)
                            {
                                // 🔥 AGORA É ASYNC (Grava no Redis + Envia SignalR)
                                await DispatchUpdates(data.Results, batch.ToList(), context, endedGameIds);
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
                finally
                {
                    _apiGate.Release();
                }

                // ✅ CORREÇÃO CRÍTICA: SALVAR NO BANCO SE HOUVER MUDANÇAS (Ex: Jogo acabou)
                // Sem isso, o status 'Ended' nunca era persistido.
                if (context.ChangeTracker.HasChanges())
                {
                    await context.SaveChangesAsync();
                }

                await Task.Delay(1100);
            }
            return endedGameIds;
        }

        // ==========================================================================================
        // 🚑 MÉTODO DE RESGATE
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

                    if (jsonString.Contains("PARAM_INVALID"))
                    {
                        Console.WriteLine($"🗑️ [LIMPEZA] Jogo inválido removido: {game.ExternalId}");
                        game.Status = "Ended";
                        context.Entry(game).State = EntityState.Modified; // Grava no SQL para limpar
                        endedGameIds.Add(game.ExternalId);
                    }
                    else
                    {
                        var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
                        if (data != null && data.Results != null)
                        {
                            await DispatchUpdates(data.Results, new List<SportsEvent> { game }, context, endedGameIds);
                        }
                    }
                }
                catch { }
                await Task.Delay(1500);
            }
        }

        // ==========================================================================================
        // 🕵️ HOTZONE (Redis + SQL Check)
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

                await _apiGate.WaitAsync();
                try
                {
                    var response = await client.GetAsync(url);
                    var jsonString = await response.Content.ReadAsStringAsync();

                    if (jsonString.Contains("PARAM_INVALID"))
                    {
                        Console.WriteLine($"⚠️ [HOTZONE] Lote com ID ruim. Verificando individualmente...");
                        foreach (var g in batch)
                        {
                            var indUrl = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={g.ExternalId}";
                            var indResp = await client.GetAsync(indUrl);
                            var indJson = await indResp.Content.ReadAsStringAsync();

                            if (!indJson.Contains("PARAM_INVALID"))
                            {
                                await ProcessHotZonePacket(indJson, new List<SportsEvent> { g }, context, newLiveIds);
                            }
                            else
                            {
                                g.Status = "Cancelled";
                                context.Entry(g).State = EntityState.Modified;
                            }
                            await Task.Delay(1500);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(jsonString))
                    {
                        await ProcessHotZonePacket(jsonString, batch.ToList(), context, newLiveIds);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ HotZone Check Erro: {ex.Message}");
                }
                finally
                {
                    _apiGate.Release();
                }

                await context.SaveChangesAsync();
                await Task.Delay(1100);
            }

            return newLiveIds;
        }

        private async Task ProcessHotZonePacket(string jsonString, List<SportsEvent> games, AppDbContext context, List<string> newLiveIds)
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

                    bool tempoExagerado = false;
                    if (int.TryParse(ev.Tm, out int minutos))
                    {
                        if (minutos > 100 && ev.Tt == "0") tempoExagerado = true;
                    }

                    bool isLive = (ev.TimeStatus == "1") || (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0");

                    if (isLive && !tempoExagerado)
                    {
                        gameDb.Status = "Live";
                        if (string.IsNullOrEmpty(gameDb.Score)) gameDb.Score = "0-0";
                        if (string.IsNullOrEmpty(gameDb.GameTime)) gameDb.GameTime = "0'";
                        gameDb.LastUpdate = DateTime.UtcNow.AddMinutes(-10);

                        context.Entry(gameDb).State = EntityState.Modified; // SQL: Mudança de Status é crítica
                        newLiveIds.Add(gameDb.ExternalId);

                        // 🔥 Grava o estado inicial no Redis também
                        var cacheKey = $"live_game:{gameDb.ExternalId}";
                        await _redisDb.StringSetAsync(cacheKey, JsonSerializer.Serialize(gameDb, _jsonOptions), TimeSpan.FromHours(24));
                    }
                }
            }
        }

        // ==========================================================================================
        // 🚦 DISPATCHER HÍBRIDO (O Coração da Mudança - COM SIGNALR)
        // ==========================================================================================
        private async Task DispatchUpdates(List<List<B365Packet>> results, List<SportsEvent> batchGames, AppDbContext context, List<string> endedGameIds)
        {
            // 📦 Lista para acumular atualizações e enviar ao Front de uma vez
            var updatesToSend = new List<object>();

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
                    bool mudou = false; // "mudou" agora controla apenas lógica de parsing
                    string sportKey = (gameDb.SportKey ?? "").ToLower();

                    // --- BLINDAGEM 106' ---
                    bool tempoExagerado = false;
                    if (int.TryParse(ev.Tm, out int minutos))
                    {
                        if (minutos > 100 && ev.Tt == "0") tempoExagerado = true;
                    }

                    // --- 🔥 CORREÇÃO: VERIFICA SE O JOGO ACABOU (CRÍTICO -> VAI PRO SQL) ---
                    // Adicionado lógica para detectar fim mesmo quando Status/TS é "0" mas TM é "90"
                    bool apiStatusFinal = ev.TimeStatus == "3" || ev.Status == "3";
                    bool tempoFinalStatusZero = (ev.TimeStatus == "0" || ev.Status == "0") && ev.Tm == "90";

                    if (apiStatusFinal || tempoFinalStatusZero || tempoExagerado)
                    {
                        bool temDados = (!string.IsNullOrEmpty(gameDb.Score) && gameDb.Score != "0-0") ||
                                        (!string.IsNullOrEmpty(gameDb.GameTime) && gameDb.GameTime != "0'");

                        if (gameDb.Status != "Ended" && temDados)
                        {
                            Console.WriteLine($"🏁 [FIM] {gameDb.HomeTeam} (ID: {gameDb.ExternalId}) -> Ended no SQL.");
                            gameDb.Status = "Ended";
                            gameDb.GameTime = "FT";
                            if (!string.IsNullOrEmpty(ev.Ss)) gameDb.Score = ev.Ss;

                            endedGameIds.Add(gameDb.ExternalId);

                            // ⚠️ IMPORTANTE: Se acabou, GRAVA NO SQL para pagar as apostas
                            context.Entry(gameDb).State = EntityState.Modified;
                        }
                    }
                    else
                    {
                        // --- JOGO ROLANDO (ALTA FREQUÊNCIA -> VAI PRO REDIS) ---

                        // Atualiza o objeto em Memória (gameDb)
                        if (sportKey.Contains("soccer") || sportKey.Contains("futebol")) mudou = UpdateSoccer(gameDb, ev, logs);
                        else if (sportKey.Contains("basket")) mudou = UpdateBasketball(gameDb, ev);
                        else if (sportKey.Contains("tennis")) mudou = UpdateTennis(gameDb, ev);
                        else if (sportKey.Contains("volley")) mudou = UpdateVolleyball(gameDb, ev);
                        else mudou = UpdateGenericSport(gameDb, ev);

                        // Processa Odds em Memória
                        ProcessOdds(gameDb, packets);

                        // 1. 🔥 MÁGICA 1: SALVA NO REDIS (Persistência Rápida)
                        var cacheKey = $"live_game:{gameDb.ExternalId}";
                        await _redisDb.StringSetAsync(cacheKey, JsonSerializer.Serialize(gameDb, _jsonOptions), TimeSpan.FromHours(24));

                        // 2. 🔥 MÁGICA 2: PREPARA PACOTE PARA O FRONT (SignalR)
                        // Criamos um objeto leve só com o que mudou/importa para o Front
                        updatesToSend.Add(new
                        {
                            id = gameDb.ExternalId,
                            time = gameDb.GameTime,
                            score = gameDb.Score,
                            status = gameDb.Status ?? "Live", // ✅ CORREÇÃO: Usar .Status em vez de .Period
                            homeOdd = gameDb.RawOddsHome,
                            drawOdd = gameDb.RawOddsDraw,
                            awayOdd = gameDb.RawOddsAway
                        });

                        // 🚫 NÃO marcamos EntityState.Modified aqui. O SQL descansa.
                    }
                }
            }

            // 🔥 DISPARO FINAL: ENVIA O LOTE PARA O FRONTEND VIA SIGNALR
            if (updatesToSend.Any())
            {
                // Console.WriteLine($"📡 Enviando {updatesToSend.Count} updates via SignalR...");
                await _hubContext.Clients.All.SendAsync("LiveOddsUpdate", updatesToSend);
                await Task.Delay(100);
            }
        }

        // ==========================================================================================
        // MÉTODOS DE PARSE (Mantidos iguais, mas agora atualizam apenas a memória)
        // ==========================================================================================

        private bool UpdateSoccer(SportsEvent game, B365Packet ev, List<B365Packet> logs)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(ev.Ss) && ev.Ss.Contains("-"))
            {
                if (game.Score != ev.Ss) { Console.WriteLine($"⚽ {game.HomeTeam}: {ev.Ss}"); game.Score = ev.Ss; changed = true; }
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
            if (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0") { string t = ev.Tm + "'"; if (game.GameTime != t) { game.GameTime = t; changed = true; } }
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
            if (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0") { string t = ev.Tm + "'"; if (game.GameTime != t) { game.GameTime = t; changed = true; } }
            return changed;
        }

        private void ProcessOdds(SportsEvent game, List<B365Packet> packets)
        {
            var odds = packets.Where(p => (p.Type == "PA" || p.Type == "MA") && !string.IsNullOrEmpty(p.Od));
            foreach (var odd in odds)
            {
                decimal valor = ConvertFraction(odd.Od);
                if (valor <= 1.0m) continue;
                string n2 = (odd.N2 ?? "").Trim();
                string name = (odd.Na ?? "").ToLower().Trim();
                bool isHome = n2 == "1" || name == "1" || name.Contains("home") || name.Contains("casa");
                bool isAway = n2 == "2" || name == "2" || name.Contains("away") || name.Contains("fora");
                bool isDraw = n2 == "x" || name == "x" || name.Contains("draw") || name.Contains("empate");

                if (isHome) game.RawOddsHome = valor;
                else if (isAway) game.RawOddsAway = valor;
                else if (isDraw) game.RawOddsDraw = valor;
            }
            // OBS: Não chamamos mais context.Entry(...).State = Modified aqui.
            // A atualização vai pro Redis no DispatchUpdates.
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