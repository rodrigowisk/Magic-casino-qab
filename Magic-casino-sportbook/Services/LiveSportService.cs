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
                // Atualiza LastUpdate para manter o "Heartbeat" do worker
                foreach (var game in batch)
                {
                    game.LastUpdate = DateTime.UtcNow;
                    // Garante que o EF saiba que mudou, se estiver rastreado
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
                                // Processa dados, salva no Redis e dispara SignalR
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

                // Salva no banco APENAS se houver mudanças críticas (ex: Status mudou para Ended)
                if (context.ChangeTracker.HasChanges())
                {
                    await context.SaveChangesAsync();
                }

                await Task.Delay(1100);
            }
            return endedGameIds;
        }

        // ==========================================================================================
        // 🚑 MÉTODO DE RESGATE (Fallback individual quando o lote falha)
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

                        // 🔥 SEGURANÇA: Marca como 'Delayed' em vez de 'Ended' para não processar bilhetes erroneamente
                        game.Status = "Delayed";

                        context.Entry(game).State = EntityState.Modified;
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
        // 🕵️ HOTZONE (Verifica Kickoff e Remove do Pré-Jogo)
        // ==========================================================================================
        public async Task<List<string>> VerifyKickoffWithApiAsync(List<SportsEvent> candidates, IServiceProvider sp)
        {
            var idsToRemoveFromPreMatch = new List<string>();
            if (!candidates.Any()) return idsToRemoveFromPreMatch;

            // ✅ CORREÇÃO CRÍTICA: Criamos um escopo NOVO e usamos AttachRange
            using var scope = sp.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Anexa os objetos ao contexto deste método para permitir salvamento
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
                                await ProcessHotZonePacket(indJson, new List<SportsEvent> { g }, context, idsToRemoveFromPreMatch);
                            }
                            else
                            {
                                // 🔥 CORREÇÃO: Avisar para remover da tela se o ID for inválido
                                Console.WriteLine($"🗑️ Removendo Jogo Inválido (Delayed): {g.ExternalId}");
                                g.Status = "Delayed";
                                context.Entry(g).State = EntityState.Modified;
                                idsToRemoveFromPreMatch.Add(g.ExternalId);
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

        // Processa o JSON da HotZone
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

                    // --- ANÁLISE DE STATUS B365 ---
                    string ts = ev.TimeStatus ?? "0";
                    string tt = ev.Tt ?? "0"; // Timer Status (1 = Rodando)

                    bool tempoExagerado = false;
                    if (int.TryParse(ev.Tm, out int minutos))
                    {
                        if (minutos > 100 && ev.Tt == "0") tempoExagerado = true;
                    }

                    // CASO 1: JOGO FICOU LIVE
                    // 🔥 MELHORIA: Considera Live se TS=1 OU se o Cronômetro (TT) estiver rodando (1)
                    bool isLive = (ts == "1") ||
                                  (tt == "1") ||
                                  (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0" && ev.Tm != "00");

                    // 🔥 CASO 2: JOGO ACABOU / FOI CANCELADO / ADIADO
                    bool isDead = ts == "3" || ts == "4" || ts == "5" || ts == "7" || ts == "8" || ts == "9";

                    if (isLive && !tempoExagerado)
                    {
                        // VIROU LIVE AGORA
                        Console.WriteLine($"✅ GO LIVE: {gameDb.HomeTeam} (ID: {gameDb.ExternalId}) | TS: {ts} | TM: {ev.Tm}");
                        gameDb.Status = "Live";
                        if (string.IsNullOrEmpty(gameDb.Score)) gameDb.Score = "0-0";
                        if (string.IsNullOrEmpty(gameDb.GameTime)) gameDb.GameTime = "0'";
                        gameDb.LastUpdate = DateTime.UtcNow.AddMinutes(-10);

                        context.Entry(gameDb).State = EntityState.Modified;

                        // Adiciona para remover do Pré-Jogo e ir para o Ao Vivo
                        idsToRemoveFromPreMatch.Add(gameDb.ExternalId);

                        var cacheKey = $"live_game:{gameDb.ExternalId}";
                        await _redisDb.StringSetAsync(cacheKey, JsonSerializer.Serialize(gameDb, _jsonOptions), TimeSpan.FromHours(24));
                    }
                    else if (isDead)
                    {
                        // JOGO ACABOU ANTES DA HORA
                        Console.WriteLine($"🗑️ AUTO-CLEAN: {gameDb.HomeTeam} (Status API: {ts}) -> Removido (Delayed/Ended).");

                        gameDb.Status = "Delayed";
                        if (ts == "3") gameDb.Status = "Ended";

                        context.Entry(gameDb).State = EntityState.Modified;
                        idsToRemoveFromPreMatch.Add(gameDb.ExternalId);
                    }
                    else
                    {
                        // 🔥 LÓGICA WAITING-LIVE RESTAURADA (Para limpar jogos da tela na hora certa)
                        // Se TS=0 (Não iniciado na API) mas horário já passou

                        // Garante UTC para comparação justa
                        var startTimeUtc = gameDb.CommenceTime.Kind == DateTimeKind.Utc
                            ? gameDb.CommenceTime
                            : DateTime.SpecifyKind(gameDb.CommenceTime, DateTimeKind.Utc);

                        if (DateTime.UtcNow >= startTimeUtc)
                        {
                            if (gameDb.Status == "Prematch")
                            {
                                // Remove da tela IMEDIATAMENTE e coloca em espera
                                Console.WriteLine($"⏳ AGUARDANDO: {gameDb.HomeTeam} (Status: WaitingLive) - API diz TS={ts}");
                                gameDb.Status = "WaitingLive";
                                context.Entry(gameDb).State = EntityState.Modified;
                                idsToRemoveFromPreMatch.Add(gameDb.ExternalId);
                            }
                            else if (gameDb.Status == "WaitingLive")
                            {
                                // Já estamos esperando. Verificamos se passou do limite (20 min)
                                var tolerancia = startTimeUtc.AddMinutes(20);

                                if (DateTime.UtcNow > tolerancia)
                                {
                                    // Desiste, marca Delayed
                                    // LOG DE DIAGNÓSTICO: Ajuda a entender por que caiu aqui
                                    Console.WriteLine($"⛔ TIMEOUT (20m): {gameDb.HomeTeam} ID:{gameDb.ExternalId} | TS:{ts} | TT:{tt} | TM:{ev.Tm} -> Delayed.");

                                    gameDb.Status = "Delayed";
                                    context.Entry(gameDb).State = EntityState.Modified;
                                    // Não precisa adicionar em idsToRemoveFromPreMatch pq já foi removido antes
                                }
                            }
                        }
                    }
                }
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
                        }
                    }
                    else
                    {
                        if (sportKey.Contains("soccer") || sportKey.Contains("futebol")) UpdateSoccer(gameDb, ev, logs);
                        else if (sportKey.Contains("basket")) UpdateBasketball(gameDb, ev);
                        else if (sportKey.Contains("tennis")) UpdateTennis(gameDb, ev);
                        else if (sportKey.Contains("volley")) UpdateVolleyball(gameDb, ev);
                        else UpdateGenericSport(gameDb, ev);

                        // 🔥 CORREÇÃO DE ODDS AQUI
                        ProcessOdds(gameDb, packets);

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
            if (!string.IsNullOrEmpty(ev.Ss) && ev.Ss.Contains("-"))
            {
                if (game.Score != ev.Ss) { game.Score = ev.Ss; changed = true; }
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

        // ==========================================================================================
        // 🔥 CORREÇÃO ESTRUTURAL DE ODDS (Blindada e Universal)
        // ==========================================================================================
        private void ProcessOdds(SportsEvent game, List<B365Packet> packets)
        {
            // 1. Encontra o ID do Mercado Principal
            string mainMarketId = null;

            var marketDef = packets.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.Id) &&
                !string.IsNullOrEmpty(p.Na) &&
                (p.Na == "Full Time Result" ||
                 p.Na == "Match Winner" ||
                 p.Na == "Game Lines" ||
                 p.Na == "Money Line" ||
                 p.Na == "Resultado Final" ||
                 p.Na == "1X2")
            );

            if (marketDef != null)
            {
                mainMarketId = marketDef.Id;
            }

            // 2. Filtra as Odds que pertencem a esse mercado (ou todas se não achar definição)
            var odds = packets.Where(p =>
                (p.Type == "PA" || p.Type == "MA") &&
                !string.IsNullOrEmpty(p.Od) &&
                (mainMarketId == null || p.Ma == mainMarketId)
            );

            foreach (var odd in odds)
            {
                decimal valor = ConvertFraction(odd.Od);
                if (valor <= 1.0m) continue;

                string n2 = (odd.N2 ?? "").Trim();
                string name = (odd.Na ?? "").ToLower().Trim();

                bool isHome = n2 == "1" || name == "1" || name.Contains("home") || name.Contains("casa") || name == game.HomeTeam?.ToLower().Trim();
                bool isAway = n2 == "2" || name == "2" || name.Contains("away") || name.Contains("fora") || name == game.AwayTeam?.ToLower().Trim();
                bool isDraw = n2 == "x" || name == "x" || name.Contains("draw") || name.Contains("empate");

                if (isHome) game.RawOddsHome = valor;
                else if (isAway) game.RawOddsAway = valor;
                else if (isDraw) game.RawOddsDraw = valor;
            }
        }

        public async Task<List<string>> CheckForKickoffAsync(List<SportsEvent> games, AppDbContext context)
        {
            return await Task.FromResult(new List<string>());
        }

        // CORREÇÃO: Aceita string? para evitar warnings e trata nulos internamente
        private decimal ConvertFraction(string? fraction)
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