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

        // ✅ Gatekeeper global usando Redis para evitar conflito de rate limit no cluster
        private readonly BetsApiGatekeeper _gatekeeper;

        public LiveSportService(
            IHttpClientFactory httpClientFactory,
            IConnectionMultiplexer redis,
            IHubContext<GameHub> hubContext,
            BetsApiGatekeeper gatekeeper)
        {
            _httpClientFactory = httpClientFactory;
            _hubContext = hubContext;
            _gatekeeper = gatekeeper;
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
        // 🧹 LIMPEZA DE ZUMBIS (VACUUM CLEANER)
        // ==========================================================================================
        public async Task CleanupZombiesAsync(List<SportsEvent> endedGames)
        {
            if (!endedGames.Any()) return;

            var idsToRemove = new List<string>();

            foreach (var game in endedGames)
            {
                // 🔥 GARANTIA DE TRIM: Remove espaços que quebram a chave do Redis
                var cleanId = game.ExternalId.Trim();
                var cacheKey = $"live_game:{cleanId}";

                // 1. Deleta do Redis explicitamente
                await _redisDb.KeyDeleteAsync(cacheKey);

                idsToRemove.Add(cleanId);
            }

            // 2. Força envio do sinal de remoção para o Frontend
            if (idsToRemove.Any())
            {
                await _hubContext.Clients.All.SendAsync("RemoveGames", idsToRemove);
            }
        }

        // ==========================================================================================
        // 🚀 ATUALIZAR LIVE (COM DIVIDE & CONQUER E PROTEÇÃO 429)
        // ==========================================================================================
        public async Task<List<string>> UpdateLiveGamesAsync(List<SportsEvent> liveGames, AppDbContext context)
        {
            var endedGameIds = new List<string>();
            if (liveGames == null || !liveGames.Any()) return endedGameIds;

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5); // Timeout reduzido para agilidade

            var batches = liveGames.Chunk(10).ToList();

            foreach (var batch in batches)
            {
                // Atualiza Timestamp Local (Heartbeat)
                foreach (var game in batch)
                {
                    game.LastUpdate = DateTime.UtcNow;
                }

                var gameIds = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
                var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={gameIds}";

                bool needsDivideAndConquer = false;

                // ✅ Usa o Gatekeeper Distribuído
                await _gatekeeper.WaitAsync(isLivePriority: true);

                try
                {
                    var response = await client.GetAsync(url);

                    // 🚨 PROTEÇÃO ANTI-429 (RATE LIMIT)
                    if ((int)response.StatusCode == 429)
                    {
                        Console.WriteLine($"⛔ [LIMIT] 429 Detectado! Pausando 5s para acalmar a API...");
                        await Task.Delay(5000);
                        // Não ativamos o Divide & Conquer aqui para não piorar o bloqueio
                        needsDivideAndConquer = false;
                    }
                    else if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();

                        // SE A API DE ODDS REJEITAR O LOTE, TEMOS UM JOGO ZUMBI NO MEIO
                        if (jsonString.Contains("PARAM_INVALID") || jsonString.Contains("failure"))
                        {
                            Console.WriteLine($"⚠️ [ECONOMIA] Lote contaminado. Iniciando Divisão Inteligente (Divide & Conquer)...");
                            needsDivideAndConquer = true;
                        }
                        else if (!string.IsNullOrWhiteSpace(jsonString))
                        {
                            var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
                            // Processa mesmo se Results for nulo para garantir limpeza de quem sumiu
                            await DispatchUpdates(data?.Results, batch.ToList(), context, endedGameIds);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro Update API: {ex.Message}");
                }

                // Se falhou (e não foi 429), usamos a estratégia econômica
                if (needsDivideAndConquer)
                {
                    await ProcessBatchWithDivideAndConquer(client, batch.ToList(), context, endedGameIds);
                }

                await Task.Delay(500); // Delay curto entre lotes
            }
            return endedGameIds;
        }

        // ==========================================================================================
        // 🧠 ALGORITMO DIVIDIR E CONQUISTAR (NOVA ESTRATÉGIA ECONÔMICA)
        // Substitui o antigo ProcessIndividualFallbackAsync
        // ==========================================================================================
        private async Task ProcessBatchWithDivideAndConquer(HttpClient client, List<SportsEvent> games, AppDbContext context, List<string> endedGameIds)
        {
            if (games.Count == 0) return;

            // CASO BASE: Se for apenas 1 jogo e falhou, ele é o culpado.
            if (games.Count == 1)
            {
                var game = games.First();
                Console.WriteLine($"🗑️ [CULPADO ENCONTRADO] Removendo ID inválido: {game.ExternalId}");

                // Marca como encerrado para sair do loop
                game.Status = "Ended";
                endedGameIds.Add(game.ExternalId);

                // Salva e remove imediatamente
                context.Entry(game).State = EntityState.Modified;
                await context.SaveChangesAsync();
                await ForceRemoveGameAsync(game.ExternalId);
                await _hubContext.Clients.All.SendAsync("RemoveGames", new List<string> { game.ExternalId });

                return;
            }

            // DIVISÃO: Quebra a lista no meio
            int mid = games.Count / 2;
            var leftBatch = games.Take(mid).ToList();
            var rightBatch = games.Skip(mid).ToList();

            // Tenta processar a METADE ESQUERDA
            bool leftSuccess = await TryProcessBatch(client, leftBatch, context, endedGameIds);

            // Se falhou, recorre (divide a esquerda novamente)
            if (!leftSuccess)
            {
                await ProcessBatchWithDivideAndConquer(client, leftBatch, context, endedGameIds);
            }

            // Tenta processar a METADE DIREITA
            bool rightSuccess = await TryProcessBatch(client, rightBatch, context, endedGameIds);

            // Se falhou, recorre (divide a direita novamente)
            if (!rightSuccess)
            {
                await ProcessBatchWithDivideAndConquer(client, rightBatch, context, endedGameIds);
            }
        }

        // Helper para tentar processar um sub-lote
        private async Task<bool> TryProcessBatch(HttpClient client, List<SportsEvent> batch, AppDbContext context, List<string> endedGameIds)
        {
            if (!batch.Any()) return true;

            var ids = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
            var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={ids}";

            try
            {
                // Pequeno delay para não estourar rate limit na recursão
                await Task.Delay(250);

                var response = await client.GetAsync(url);

                // SE DER 429 AQUI, PARAMOS IMEDIATAMENTE (MENTIMOS SUCESSO PARA PARAR A RECURSÃO)
                if ((int)response.StatusCode == 429)
                {
                    Console.WriteLine("⛔ [LIMIT RECURSIVO] 429 na divisão. Abortando para proteger a conta...");
                    await Task.Delay(2000);
                    return true;
                }

                var jsonString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && !jsonString.Contains("failure") && !jsonString.Contains("PARAM_INVALID"))
                {
                    var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
                    await DispatchUpdates(data?.Results, batch, context, endedGameIds);
                    return true; // SUCESSO! Não precisa dividir mais.
                }
            }
            catch { }

            return false; // FALHOU! Precisa dividir mais.
        }

        // ==========================================================================================
        // 🚦 DISPATCHER HÍBRIDO BLINDADO (REDIS-FIRST)
        // ==========================================================================================
        private async Task DispatchUpdates(List<List<B365Packet>>? results, List<SportsEvent> batchGames, AppDbContext context, List<string> endedGameIds)
        {
            var updatesToSend = new List<object>();
            var idsToRemoveNow = new HashSet<string>();

            // Mapa para acesso rápido (Key = FI/ExternalId)
            var packetsMap = new Dictionary<string, List<B365Packet>>();
            if (results != null)
            {
                foreach (var list in results)
                {
                    var fi = list.FirstOrDefault(p => !string.IsNullOrEmpty(p.Fi))?.Fi;
                    if (!string.IsNullOrEmpty(fi)) packetsMap[fi] = list;
                }
            }

            // ITERA SOBRE OS JOGOS DO BANCO
            foreach (var gameDb in batchGames)
            {
                // 1. Recarrega status do banco (para evitar conflitos de concorrência)
                try
                {
                    if (context.Entry(gameDb).State == EntityState.Detached) context.Attach(gameDb);
                }
                catch { continue; }

                // 2. ZOMBIE GUARD 1: Se já acabou, limpa e pula
                if (gameDb.Status == "Ended" || gameDb.Status == "Completed" || endedGameIds.Contains(gameDb.ExternalId))
                {
                    await ForceRemoveGameAsync(gameDb.ExternalId);
                    idsToRemoveNow.Add(gameDb.ExternalId);
                    endedGameIds.Add(gameDb.ExternalId);
                    continue;
                }

                // 3. Tenta pegar dados da API
                if (!packetsMap.TryGetValue(gameDb.ExternalId, out var packets))
                {
                    continue;
                }

                var ev = packets.FirstOrDefault(p => p.Type == "EV");
                var logs = packets.Where(p => p.Type == "ST").ToList();

                if (ev != null)
                {
                    // 4. VERIFICAÇÃO DE FIM PELA API DE EVENTOS
                    bool isEnded = ev.TimeStatus == "3" || ev.Status == "3";

                    // Proteção contra bugs da API (Tempo > 100min parado)
                    if (int.TryParse(ev.Tm, out int min) && min > 100 && ev.Tt == "0") isEnded = true;

                    // Proteção contra status 0 com 90min (falso pré-jogo)
                    if ((ev.TimeStatus == "0" || ev.Status == "0") && ev.Tm == "90") isEnded = true;

                    if (isEnded)
                    {
                        Console.WriteLine($"🏁 [FIM VIA EVENT] {gameDb.HomeTeam} (ID: {gameDb.ExternalId})");
                        gameDb.Status = "Ended";
                        gameDb.GameTime = "FT";
                        if (!string.IsNullOrEmpty(ev.Ss)) gameDb.Score = ev.Ss;

                        endedGameIds.Add(gameDb.ExternalId);
                        idsToRemoveNow.Add(gameDb.ExternalId);

                        // 💾 SALVA NO SQL IMEDIATAMENTE (CRÍTICO - JOGO ACABOU)
                        context.Entry(gameDb).State = EntityState.Modified;
                        await context.SaveChangesAsync();

                        await ForceRemoveGameAsync(gameDb.ExternalId);
                    }
                    else
                    {
                        // 5. ATUALIZAÇÃO NORMAL (JOGO VIVO)

                        string sportKey = (gameDb.SportKey ?? "").ToLower();
                        bool changed = false;

                        if (sportKey.Contains("soccer") || sportKey.Contains("futebol")) changed = UpdateSoccer(gameDb, ev, logs);
                        else if (sportKey.Contains("basket")) changed = UpdateBasketball(gameDb, ev);
                        else if (sportKey.Contains("tennis")) changed = UpdateTennis(gameDb, ev);
                        else if (sportKey.Contains("volley")) changed = UpdateVolleyball(gameDb, ev);
                        else changed = UpdateGenericSport(gameDb, ev);

                        ProcessOddsSequential(gameDb, packets);

                        // =========================================================================
                        // 🚀 REDIS-FIRST IMPLEMENTATION
                        // =========================================================================

                        // A. Salva no Redis (Verdade Absoluta para o Frontend)
                        var cacheKey = $"live_game:{gameDb.ExternalId.Trim()}";
                        await _redisDb.StringSetAsync(cacheKey, JsonSerializer.Serialize(gameDb, _jsonOptions), TimeSpan.FromHours(1));

                        // B. Marca como "Sujo" para o PersistenceWorker salvar no SQL depois
                        // Isso remove a pressão do banco de dados!
                        if (changed || gameDb.LastUpdate > DateTime.UtcNow.AddSeconds(-30))
                        {
                            await _redisDb.SetAddAsync("dirty_live_games", gameDb.ExternalId.Trim());
                        }

                        var statDb = await context.Set<LiveGameStat>().FirstOrDefaultAsync(s => s.GameId == gameDb.ExternalId);

                        updatesToSend.Add(new
                        {
                            id = gameDb.ExternalId,
                            time = gameDb.GameTime,
                            score = gameDb.Score,
                            status = gameDb.Status ?? "Live",
                            homeOdd = gameDb.RawOddsHome,
                            drawOdd = gameDb.RawOddsDraw,
                            awayOdd = gameDb.RawOddsAway,
                            homeYellowCards = statDb?.HomeYellowCards ?? 0,
                            awayYellowCards = statDb?.AwayYellowCards ?? 0,
                            homeRedCards = statDb?.HomeRedCards ?? 0,
                            awayRedCards = statDb?.AwayRedCards ?? 0
                        });
                    }
                }
            }

            // 6. ENVIOS SIGNALR (Prioridade para remoção)
            if (idsToRemoveNow.Any())
            {
                var uniqueIds = idsToRemoveNow.ToList();
                await _hubContext.Clients.All.SendAsync("RemoveGames", uniqueIds);
            }

            if (updatesToSend.Any())
            {
                await _hubContext.Clients.All.SendAsync("LiveOddsUpdate", updatesToSend);
            }
        }

        private async Task ForceRemoveGameAsync(string gameId)
        {
            // Limpeza blindada com Trim
            var cleanId = gameId.Trim();
            await _redisDb.KeyDeleteAsync($"live_game:{cleanId}");

            // Remove da lista de sujos também, pois se acabou já salvamos ou removemos
            await _redisDb.SetRemoveAsync("dirty_live_games", cleanId);
        }

        // ==========================================================================================
        // MÉTODOS DE PARSE (MANTIDOS E OTIMIZADOS)
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

            if (!string.IsNullOrEmpty(ev.Tm))
            {
                int minutosApi = int.Parse(ev.Tm);

                if (minutosApi == 45 && ev.TimeStatus == "2")
                {
                    novoTempo = "HT";
                }
                else if (ev.Tt == "1" && !string.IsNullOrEmpty(ev.Tu) && ev.Tu.Length == 14)
                {
                    try
                    {
                        var dataAtualizacao = DateTime.ParseExact(ev.Tu, "yyyyMMddHHmmss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

                        var agora = DateTime.UtcNow;
                        var diferenca = agora - dataAtualizacao;

                        if (diferenca.TotalSeconds > 0 && diferenca.TotalMinutes < 120)
                        {
                            int segundosApi = !string.IsNullOrEmpty(ev.Ts) ? int.Parse(ev.Ts) : 0;
                            var totalSegundosDoJogo = (minutosApi * 60) + segundosApi + diferenca.TotalSeconds;
                            int minutoReal = (int)(totalSegundosDoJogo / 60);

                            if (minutosApi < 45 && minutoReal > 45) minutoReal = 45;
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

        private void ProcessOddsSequential(SportsEvent game, List<B365Packet> packets)
        {
            try
            {
                string sportKey = (game.SportKey ?? "").ToLower();
                if (sportKey.Contains("basket") || sportKey == "18") return;

                bool isReadingMainMarket = false;

                foreach (var p in packets)
                {
                    if (p.Type == "MG" || p.Type == "MA")
                    {
                        string name = (p.Na ?? "").ToLower().Trim();
                        if (name == "fulltime result" || name == "match winner" || name == "to win" || name == "1x2" || name == "vencedor" || name == "resultado final")
                        {
                            isReadingMainMarket = true;
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(name)) isReadingMainMarket = false;
                        }
                    }

                    if ((p.Type == "PA") && isReadingMainMarket && !string.IsNullOrEmpty(p.Od))
                    {
                        decimal valor = ConvertFraction(p.Od);
                        string name = (p.Na ?? "").ToLower().Trim();
                        string n2 = (p.N2 ?? "").ToLower().Trim();

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

        // ==========================================================================================
        // HOTZONE (CLIENT UNIFICADO PARA EVITAR SOCKET EXHAUSTION)
        // ==========================================================================================
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

            foreach (var c in candidates)
            {
                if (context.Entry(c).State == EntityState.Detached)
                    context.Attach(c);
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var batches = candidates.Chunk(10).ToList();

            foreach (var batch in batches)
            {
                var gameIds = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
                var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={gameIds}";
                bool batchFailed = false;

                // ✅ Usa o Gatekeeper Distribuído
                await _gatekeeper.WaitAsync(isLivePriority: true);

                try
                {
                    var response = await client.GetAsync(url);

                    // 🚨 PROTEÇÃO ANTI-429 NA HOTZONE
                    if ((int)response.StatusCode == 429)
                    {
                        Console.WriteLine($"⛔ [HOTZONE] 429 Detectado! Pausando 5s...");
                        await Task.Delay(5000);
                        continue; // Pula este lote para não agravar
                    }

                    var jsonString = await response.Content.ReadAsStringAsync();

                    if (jsonString.Contains("PARAM_INVALID") || jsonString.Contains("failure"))
                    {
                        batchFailed = true;
                        Console.WriteLine($"⚠️ [HOTZONE] Lote contaminado. Iniciando verificação individual...");
                    }
                    else if (!string.IsNullOrWhiteSpace(jsonString))
                    {
                        await ProcessHotZonePacket(jsonString, batch.ToList(), context, idsToRemoveFromPreMatch);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ HotZone Check Erro: {ex.Message}");
                    batchFailed = true;
                }

                if (batchFailed)
                {
                    await ProcessHotZoneIndividualAsync(client, batch.ToList(), context, idsToRemoveFromPreMatch);
                }

                await context.SaveChangesAsync();
                await Task.Delay(1000);
            }

            return idsToRemoveFromPreMatch;
        }

        private async Task ProcessHotZoneIndividualAsync(HttpClient client, List<SportsEvent> batch, AppDbContext context, List<string> idsToRemoveFromPreMatch)
        {
            foreach (var game in batch)
            {
                try
                {
                    var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={game.ExternalId.Trim()}";
                    await Task.Delay(200);

                    var response = await client.GetAsync(url);

                    // Se der 429 aqui, aborta o loop individual
                    if ((int)response.StatusCode == 429)
                    {
                        await Task.Delay(2000);
                        return;
                    }

                    var jsonString = await response.Content.ReadAsStringAsync();

                    if (jsonString.Contains("PARAM_INVALID") || jsonString.Contains("failure"))
                    {
                        Console.WriteLine($"🗑️ [HOTZONE RESGATE] Removendo jogo inválido: {game.ExternalId}");
                        game.Status = "Ended";
                        idsToRemoveFromPreMatch.Add(game.ExternalId);
                        context.Entry(game).State = EntityState.Modified;
                        await ForceRemoveGameAsync(game.ExternalId);
                    }
                    else
                    {
                        await ProcessHotZonePacket(jsonString, new List<SportsEvent> { game }, context, idsToRemoveFromPreMatch);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro Individual HotZone: {ex.Message}");
                }
            }
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
                        Console.WriteLine($"✅ GO LIVE: {gameDb.HomeTeam} (ID: {gameDb.ExternalId})");
                        gameDb.Status = "Live";
                        if (string.IsNullOrEmpty(gameDb.Score)) gameDb.Score = "0-0";
                        if (string.IsNullOrEmpty(gameDb.GameTime)) gameDb.GameTime = "0'";
                        gameDb.LastUpdate = DateTime.UtcNow.AddMinutes(-10);

                        context.Entry(gameDb).State = EntityState.Modified;
                        idsToRemoveFromPreMatch.Add(gameDb.ExternalId);

                        // 🚀 ATUALIZA REDIS IMEDIATAMENTE AO VIRAR LIVE
                        var cacheKey = $"live_game:{gameDb.ExternalId.Trim()}";
                        await _redisDb.StringSetAsync(cacheKey, JsonSerializer.Serialize(gameDb, _jsonOptions), TimeSpan.FromHours(24));
                    }
                    else if (isDead)
                    {
                        gameDb.Status = "Delayed";
                        if (ts == "3") gameDb.Status = "Ended";

                        context.Entry(gameDb).State = EntityState.Modified;
                        idsToRemoveFromPreMatch.Add(gameDb.ExternalId);

                        var cacheKey = $"live_game:{gameDb.ExternalId.Trim()}";
                        await _redisDb.KeyDeleteAsync(cacheKey);
                    }
                }
            }
        }
    }
}