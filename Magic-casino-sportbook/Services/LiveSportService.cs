using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Hubs;
using Magic_casino_sportbook.Models;
using Microsoft.AspNetCore.SignalR;
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

        // Redis para cache rápido
        private readonly IDatabase _redisDb;

        // SignalR para enviar updates ao frontend
        private readonly IHubContext<GameHub> _hubContext;

        // Semáforo para controlar requisições na API (Rate Limit)
        private static readonly SemaphoreSlim _apiGate = new SemaphoreSlim(1, 1);

        public LiveSportService(
            IHttpClientFactory httpClientFactory,
            IConnectionMultiplexer redis,
            IHubContext<GameHub> hubContext)
        {
            _httpClientFactory = httpClientFactory;
            _hubContext = hubContext;
            _apiToken = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";

            // Conexão Redis
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
        // 🚀 ATUALIZAR LIVE (Jogos que já estão rolando - Atualiza Placar/Odds)
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
                foreach (var game in batch)
                {
                    game.LastUpdate = DateTime.UtcNow;
                    if (context.Entry(game).State != EntityState.Detached)
                    {
                        context.Entry(game).Property(x => x.LastUpdate).IsModified = true;
                    }
                }

                var gameIds = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
                var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={gameIds}";

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

                if (context.ChangeTracker.HasChanges())
                {
                    await context.SaveChangesAsync();
                }

                await Task.Delay(1100);
            }
            return endedGameIds;
        }

        private async Task ProcessIndividualFallbackAsync(HttpClient client, SportsEvent[] batch, AppDbContext context, List<string> endedGameIds)
        {
            foreach (var game in batch)
            {
                var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={game.ExternalId.Trim()}";
                try
                {
                    var response = await client.GetAsync(url);
                    var jsonString = await response.Content.ReadAsStringAsync();

                    if (!jsonString.Contains("PARAM_INVALID"))
                    {
                        var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
                        if (data != null && data.Results != null)
                        {
                            await DispatchUpdates(data.Results, new List<SportsEvent> { game }, context, endedGameIds);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ [API ERRO] ID rejeitado pela API: {game.ExternalId}. Mantendo estado atual.");
                    }
                }
                catch { }
                await Task.Delay(1500);
            }
        }

        // ==========================================================================================
        // 🚦 DISPATCHER HÍBRIDO (Redis + SQL + SignalR)
        // ==========================================================================================
        private async Task DispatchUpdates(List<List<B365Packet>> results, List<SportsEvent> batchGames, AppDbContext context, List<string> endedGameIds)
        {
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
                    string sportKey = (gameDb.SportKey ?? "").ToLower();

                    bool tempoExagerado = false;
                    if (int.TryParse(ev.Tm, out int minutos))
                    {
                        if (minutos > 100 && ev.Tt == "0") tempoExagerado = true;
                    }

                    bool apiStatusFinal = ev.TimeStatus == "3" || ev.Status == "3";
                    bool tempoFinalStatusZero = (ev.TimeStatus == "0" || ev.Status == "0") && ev.Tm == "90";

                    if (apiStatusFinal || tempoFinalStatusZero || tempoExagerado)
                    {
                        if (gameDb.Status != "Ended")
                        {
                            Console.WriteLine($"🏁 [FIM DETECTADO] {gameDb.HomeTeam} (ID: {gameDb.ExternalId}) -> Ended.");
                            gameDb.Status = "Ended";
                            gameDb.GameTime = "FT";
                            if (!string.IsNullOrEmpty(ev.Ss)) gameDb.Score = ev.Ss;

                            endedGameIds.Add(gameDb.ExternalId);
                            context.Entry(gameDb).State = EntityState.Modified;

                            // 🗑️ LIMPEZA DE REDIS: Remove o jogo do cache imediatamente
                            var cacheKey = $"live_game:{gameDb.ExternalId}";
                            await _redisDb.KeyDeleteAsync(cacheKey);
                        }
                    }
                    else
                    {
                        if (sportKey.Contains("soccer") || sportKey.Contains("futebol")) UpdateSoccer(gameDb, ev, logs);
                        else if (sportKey.Contains("basket")) UpdateBasketball(gameDb, ev);
                        else if (sportKey.Contains("tennis")) UpdateTennis(gameDb, ev);
                        else if (sportKey.Contains("volley")) UpdateVolleyball(gameDb, ev);
                        else UpdateGenericSport(gameDb, ev);

                        // 🔥 Processamento das Odds (AGORA SEQUENCIAL)
                        ProcessOddsSequential(gameDb, packets);

                        // Atualiza Cache (Live)
                        var cacheKey = $"live_game:{gameDb.ExternalId}";
                        await _redisDb.StringSetAsync(cacheKey, JsonSerializer.Serialize(gameDb, _jsonOptions), TimeSpan.FromHours(24));

                        updatesToSend.Add(new
                        {
                            id = gameDb.ExternalId,
                            time = gameDb.GameTime,
                            score = gameDb.Score,
                            status = gameDb.Status ?? "Live",
                            homeOdd = gameDb.RawOddsHome,
                            drawOdd = gameDb.RawOddsDraw,
                            awayOdd = gameDb.RawOddsAway
                        });
                    }
                }
            }

            if (updatesToSend.Any())
            {
                await _hubContext.Clients.All.SendAsync("LiveOddsUpdate", updatesToSend);
                await Task.Delay(100);
            }
        }

        // ==========================================================================================
        // MÉTODOS DE PARSE POR ESPORTE
        // ==========================================================================================
        private bool UpdateSoccer(SportsEvent game, B365Packet ev, List<B365Packet> logs)
        {
            bool changed = false;

            // 1. Atualiza Placar
            if (!string.IsNullOrEmpty(ev.Ss) && ev.Ss.Contains("-"))
            {
                if (game.Score != ev.Ss) { game.Score = ev.Ss; changed = true; }
            }
            else if (string.IsNullOrEmpty(game.Score)) { game.Score = "0-0"; changed = true; }

            // 2. TEMPO: Correção de Atraso + Formatação "Minuto a Minuto"
            string novoTempo = "0'";

            if (!string.IsNullOrEmpty(ev.Tm))
            {
                int minutosApi = int.Parse(ev.Tm);

                // Intervalo (HT)
                if (minutosApi == 45 && ev.TimeStatus == "2")
                {
                    novoTempo = "HT";
                }
                // Jogo Rodando (TT=1) + Timestamp Válido (TU)
                else if (ev.Tt == "1" && !string.IsNullOrEmpty(ev.Tu) && ev.Tu.Length == 14)
                {
                    try
                    {
                        var dataAtualizacao = DateTime.ParseExact(ev.Tu, "yyyyMMddHHmmss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

                        var agora = DateTime.UtcNow;
                        var diferenca = agora - dataAtualizacao;

                        // CORREÇÃO DE TOLERÂNCIA: Aumentado para 120min para aceitar delays grandes de APIs regionais
                        if (diferenca.TotalSeconds > 0 && diferenca.TotalMinutes < 120)
                        {
                            int segundosApi = !string.IsNullOrEmpty(ev.Ts) ? int.Parse(ev.Ts) : 0;
                            var totalSegundosDoJogo = (minutosApi * 60) + segundosApi + diferenca.TotalSeconds;
                            int minutoReal = (int)(totalSegundosDoJogo / 60);

                            if (minutosApi < 45 && minutoReal > 45) minutoReal = 45;
                            // Se minutoReal > 90, deixa passar (ex: 95')
                            novoTempo = $"{minutoReal}'";
                        }
                        else
                        {
                            novoTempo = $"{minutosApi}'";
                        }
                    }
                    catch
                    {
                        novoTempo = $"{minutosApi}'";
                    }
                }
                else
                {
                    novoTempo = $"{minutosApi}'";
                }
            }
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

            if (game.GameTime != novoTempo && novoTempo != "0'")
            {
                game.GameTime = novoTempo;
                changed = true;
            }

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

        // ==========================================================================================
        // 🔥 CORREÇÃO CRÍTICA DE ODDS: LÓGICA SEQUENCIAL (EVITA SOBRESCRITA DE 2º TEMPO)
        // ==========================================================================================
        private void ProcessOddsSequential(SportsEvent game, List<B365Packet> packets)
        {
            try
            {
                string sportKey = (game.SportKey ?? "").ToLower();
                if (sportKey.Contains("basket") || sportKey == "18") return;

                bool isReadingMainMarket = false;

                // Itera sequencialmente sobre a lista plana para respeitar a hierarquia do JSON
                foreach (var p in packets)
                {
                    // 1. Detecta Mudança de Cabeçalho (MG/MA)
                    if (p.Type == "MG" || p.Type == "MA")
                    {
                        string name = (p.Na ?? "").ToLower().Trim();

                        // Lista de nomes aceitos para o mercado PRINCIPAL
                        if (name == "fulltime result" ||
                            name == "match winner" ||
                            name == "to win" ||
                            name == "1x2" ||
                            name == "vencedor" ||
                            name == "resultado final")
                        {
                            isReadingMainMarket = true;
                        }
                        else
                        {
                            // Se encontrou qualquer outro cabeçalho (ex: "To Win 2nd Half"), para de ler odds
                            if (!string.IsNullOrEmpty(name))
                            {
                                isReadingMainMarket = false;
                            }
                        }
                    }

                    // 2. Processa Odds (PA) APENAS se estivermos lendo o mercado principal
                    if ((p.Type == "PA") && isReadingMainMarket && !string.IsNullOrEmpty(p.Od))
                    {
                        decimal valor = ConvertFraction(p.Od);

                        string name = (p.Na ?? "").ToLower().Trim();
                        string n2 = (p.N2 ?? "").ToLower().Trim();

                        // Filtros extras de segurança
                        if (name.Contains("corner") || name.Contains("card") || name.Contains("goal") || name.Contains("handicap")) continue;

                        bool isHome = n2 == "1" || name == "1" || name.Contains("home") || name.Contains("casa") || (!string.IsNullOrEmpty(game.HomeTeam) && name == game.HomeTeam.ToLower().Trim());
                        bool isAway = n2 == "2" || name == "2" || name.Contains("away") || name.Contains("fora") || (!string.IsNullOrEmpty(game.AwayTeam) && name == game.AwayTeam.ToLower().Trim());
                        bool isDraw = n2 == "x" || name == "x" || name.Contains("draw") || name.Contains("empate");

                        if (isHome) game.RawOddsHome = valor;
                        else if (isAway) game.RawOddsAway = valor;
                        else if (isDraw) game.RawOddsDraw = valor;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro parsing odds: {ex.Message}");
            }
        }

        public async Task<List<string>> CheckForKickoffAsync(List<SportsEvent> games, AppDbContext context)
        {
            return await Task.FromResult(new List<string>());
        }

        public async Task<List<string>> VerifyKickoffWithApiAsync(List<SportsEvent> candidates, IServiceProvider sp)
        {
            var idsToRemoveFromPreMatch = new List<string>();
            if (!candidates.Any()) return idsToRemoveFromPreMatch;

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
                            var indUrl = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={g.ExternalId.Trim()}";
                            var indResp = await client.GetAsync(indUrl);
                            var indJson = await indResp.Content.ReadAsStringAsync();

                            if (!indJson.Contains("PARAM_INVALID"))
                            {
                                await ProcessHotZonePacket(indJson, new List<SportsEvent> { g }, context, idsToRemoveFromPreMatch);
                            }
                            await Task.Delay(1500);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(jsonString))
                    {
                        await ProcessHotZonePacket(jsonString, batch.ToList(), context, idsToRemoveFromPreMatch);
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

            return idsToRemoveFromPreMatch;
        }

        private async Task ProcessHotZonePacket(string jsonString, List<SportsEvent> games, AppDbContext context, List<string> idsToRemoveFromPreMatch)
        {
            var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
            if (data != null && data.Results != null)
            {
                foreach (var packetList in data.Results)
                {
                    var ev = packetList.FirstOrDefault(p => p.Type == "EV");
                    if (ev == null) continue;

                    var gameDb = games.FirstOrDefault(g => g.ExternalId.Trim() == ev.Fi.Trim());
                    if (gameDb == null) continue;

                    string ts = ev.TimeStatus ?? "0";
                    string tt = ev.Tt ?? "0";

                    bool tempoExagerado = false;
                    if (int.TryParse(ev.Tm, out int minutos))
                    {
                        if (minutos > 100 && ev.Tt == "0") tempoExagerado = true;
                    }

                    bool isLive = (ts == "1") || (tt == "1") || (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0" && ev.Tm != "00");
                    bool isDead = ts == "3" || ts == "4" || ts == "5" || ts == "7" || ts == "8" || ts == "9";

                    if (isLive && !tempoExagerado)
                    {
                        Console.WriteLine($"✅ GO LIVE: {gameDb.HomeTeam} (ID: {gameDb.ExternalId}) | TS: {ts} | TM: {ev.Tm}");
                        gameDb.Status = "Live";
                        if (string.IsNullOrEmpty(gameDb.Score)) gameDb.Score = "0-0";
                        if (string.IsNullOrEmpty(gameDb.GameTime)) gameDb.GameTime = "0'";
                        gameDb.LastUpdate = DateTime.UtcNow.AddMinutes(-10);

                        context.Entry(gameDb).State = EntityState.Modified;
                        idsToRemoveFromPreMatch.Add(gameDb.ExternalId);

                        var cacheKey = $"live_game:{gameDb.ExternalId}";
                        await _redisDb.StringSetAsync(cacheKey, JsonSerializer.Serialize(gameDb, _jsonOptions), TimeSpan.FromHours(24));
                    }
                    else if (isDead)
                    {
                        Console.WriteLine($"🗑️ AUTO-CLEAN: {gameDb.HomeTeam} (Status API: {ts}) -> Removido (Delayed/Ended).");
                        gameDb.Status = "Delayed";
                        if (ts == "3") gameDb.Status = "Ended";

                        context.Entry(gameDb).State = EntityState.Modified;
                        idsToRemoveFromPreMatch.Add(gameDb.ExternalId);

                        // 🗑️ LIMPEZA DE REDIS: Remove da hotzone também
                        var cacheKey = $"live_game:{gameDb.ExternalId}";
                        await _redisDb.KeyDeleteAsync(cacheKey);
                    }
                    else
                    {
                        var startTimeUtc = gameDb.CommenceTime.Kind == DateTimeKind.Utc
                            ? gameDb.CommenceTime
                            : DateTime.SpecifyKind(gameDb.CommenceTime, DateTimeKind.Utc);

                        if (DateTime.UtcNow >= startTimeUtc)
                        {
                            if (gameDb.Status == "Prematch")
                            {
                                Console.WriteLine($"⏳ AGUARDANDO: {gameDb.HomeTeam} (Status: WaitingLive)");
                                gameDb.Status = "WaitingLive";
                                context.Entry(gameDb).State = EntityState.Modified;
                                idsToRemoveFromPreMatch.Add(gameDb.ExternalId);
                            }
                            else if (gameDb.Status == "WaitingLive")
                            {
                                var tolerancia = startTimeUtc.AddMinutes(45);
                                if (DateTime.UtcNow > tolerancia)
                                {
                                    Console.WriteLine($"⛔ TIMEOUT REAL (45m): {gameDb.HomeTeam} -> Delayed.");
                                    gameDb.Status = "Delayed";
                                    context.Entry(gameDb).State = EntityState.Modified;

                                    // 🗑️ LIMPEZA DE REDIS: Timeout
                                    var cacheKey = $"live_game:{gameDb.ExternalId}";
                                    await _redisDb.KeyDeleteAsync(cacheKey);
                                }
                            }
                        }
                    }
                }
            }
        }

        private decimal ConvertFraction(string? fraction)
        {
            if (string.IsNullOrEmpty(fraction) || fraction == "0/0") return 0m;
            try
            {
                if (fraction.Contains("/"))
                {
                    var parts = fraction.Split('/');
                    if (parts.Length == 2) return (decimal.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) / decimal.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)) + 1;
                }
                return decimal.Parse(fraction, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return 0m; }
        }
    }
}