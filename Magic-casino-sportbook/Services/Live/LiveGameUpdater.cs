using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Hubs;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Services.Gateways;
using Magic_casino_sportbook.Events;
using Microsoft.AspNetCore.SignalR;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Magic_casino_sportbook.Services.Live
{
    public class LiveGameUpdater
    {
        private readonly BetsApiHttpService _api; // ✅ Gateway Centralizado
        private readonly IDatabase _redisDb;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly ILogger<LiveGameUpdater> _logger;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly JsonSerializerOptions _jsonOptions;

        public LiveGameUpdater(
            BetsApiHttpService api,
            IConnectionMultiplexer redis,
            IHubContext<GameHub> hubContext,
            ILogger<LiveGameUpdater> logger,
            IPublishEndpoint publishEndpoint)
        {
            _api = api;
            _hubContext = hubContext;
            _logger = logger;
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
        // 🚀 ATUALIZAR LIVE (COM DIVIDE & CONQUER E PROTEÇÃO 429)
        // ==========================================================================================
        public async Task<List<string>> UpdateLiveGamesAsync(List<SportsEvent> liveGames, AppDbContext context, bool saveToSql = false)
        {
            var endedGameIds = new List<string>();
            if (liveGames == null || !liveGames.Any()) return endedGameIds;

            // Divide em lotes de 10 para não sobrecarregar a URL
            var batches = liveGames.Chunk(10).ToList();

            foreach (var batch in batches)
            {
                var gameIds = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
                var url = $"/v1/bet365/event?FI={gameIds}";

                bool needsDivideAndConquer = false;

                try
                {
                    // ✅ Usa o Gateway com prioridade ALTA (Live)
                    var response = await _api.GetAsync(url, isLivePriority: true);

                    if (response == null)
                    {
                        _logger.LogWarning($"⚠️ [LiveUpdater] Resposta nula do Gateway para URL: {url}");
                        needsDivideAndConquer = true; // Assume erro para tentar recuperar depois
                        continue; // Pula para o próximo
                    }

                    if ((int)response.StatusCode == 429)
                    {
                        _logger.LogWarning($"⛔ [LiveUpdater] 429 Detectado! Pausando 5s...");
                        await Task.Delay(5000);
                        needsDivideAndConquer = false; // Não divide agora para não piorar
                    }
                    else if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();

                        // SE A API REJEITAR O LOTE, TEMOS UM JOGO ZUMBI NO MEIO
                        if (jsonString.Contains("PARAM_INVALID") || jsonString.Contains("failure"))
                        {
                            _logger.LogWarning($"⚠️ [LiveUpdater] Lote contaminado. Iniciando Divisão Inteligente...");
                            needsDivideAndConquer = true;
                        }
                        else if (!string.IsNullOrWhiteSpace(jsonString))
                        {
                            var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
                            await DispatchUpdates(data?.Results, batch.ToList(), context, endedGameIds, saveToSql);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Erro Update API: {ex.Message}");
                }

                // Se falhou, usamos a estratégia econômica para achar o culpado
                if (needsDivideAndConquer)
                {
                    await ProcessBatchWithDivideAndConquer(batch.ToList(), context, endedGameIds);
                }

                await Task.Delay(500); // Delay curto entre lotes
            }
            return endedGameIds;
        }

        // ==========================================================================================
        // 🧠 ALGORITMO DIVIDIR E CONQUISTAR
        // ==========================================================================================
        private async Task ProcessBatchWithDivideAndConquer(List<SportsEvent> games, AppDbContext context, List<string> endedGameIds)
        {
            if (games.Count == 0) return;

            // CASO BASE: Se for apenas 1 jogo e falhou, ele é o culpado.
            if (games.Count == 1)
            {
                var game = games.First();
                _logger.LogWarning($"🗑️ [CULPADO ENCONTRADO] Removendo ID inválido: {game.ExternalId}");

                game.Status = "Ended";
                endedGameIds.Add(game.ExternalId);

                context.Entry(game).State = EntityState.Modified;
                await context.SaveChangesAsync();

                await ForceRemoveGameAsync(game.ExternalId);
                await _hubContext.Clients.All.SendAsync("RemoveGames", new List<string> { game.ExternalId });

                return;
            }

            // DIVISÃO
            int mid = games.Count / 2;
            var leftBatch = games.Take(mid).ToList();
            var rightBatch = games.Skip(mid).ToList();

            // Tenta processar a METADE ESQUERDA
            bool leftSuccess = await TryProcessBatch(leftBatch, context, endedGameIds);
            if (!leftSuccess)
            {
                await ProcessBatchWithDivideAndConquer(leftBatch, context, endedGameIds);
            }

            // Tenta processar a METADE DIREITA
            bool rightSuccess = await TryProcessBatch(rightBatch, context, endedGameIds);
            if (!rightSuccess)
            {
                await ProcessBatchWithDivideAndConquer(rightBatch, context, endedGameIds);
            }
        }

        private async Task<bool> TryProcessBatch(List<SportsEvent> batch, AppDbContext context, List<string> endedGameIds)
        {
            if (!batch.Any()) return true;

            var ids = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
            var url = $"/v1/bet365/event?FI={ids}";

            try
            {
                await Task.Delay(250);
                var response = await _api.GetAsync(url, isLivePriority: true);

                if ((int)response.StatusCode == 429)
                {
                    await Task.Delay(2000);
                    return true; // Mentimos sucesso para abortar recursão em caso de rate limit
                }

                var jsonString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && !jsonString.Contains("failure") && !jsonString.Contains("PARAM_INVALID"))
                {
                    var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
                    await DispatchUpdates(data?.Results, batch, context, endedGameIds, false);
                    return true; // SUCESSO!
                }
            }
            catch { }

            return false; // FALHOU!
        }

        // ==========================================================================================
        // 🚦 DISPATCHER HÍBRIDO BLINDADO
        // ==========================================================================================
        private async Task DispatchUpdates(List<List<B365Packet>>? results, List<SportsEvent> batchGames, AppDbContext context, List<string> endedGameIds, bool saveToSql)
        {
            var updatesToSend = new List<object>();
            var idsToRemoveNow = new HashSet<string>();

            var packetsMap = new Dictionary<string, List<B365Packet>>();
            if (results != null)
            {
                foreach (var list in results)
                {
                    if (list == null) continue;                   
                    var fi = list.FirstOrDefault(p => !string.IsNullOrEmpty(p.Fi))?.Fi;
                    if (!string.IsNullOrEmpty(fi)) packetsMap[fi] = list;
                    
                }
            }

            foreach (var gameDb in batchGames)
            {
                try
                {
                    if (context.Entry(gameDb).State == EntityState.Detached) context.Attach(gameDb);
                }
                catch { continue; }

                if (gameDb.Status == "Ended" || gameDb.Status == "Completed" || endedGameIds.Contains(gameDb.ExternalId))
                {
                    await ForceRemoveGameAsync(gameDb.ExternalId);
                    idsToRemoveNow.Add(gameDb.ExternalId);
                    endedGameIds.Add(gameDb.ExternalId);
                    continue;
                }

                // ==============================================================================
                // 🔥 CORREÇÃO CRÍTICA AQUI (ZOMBIE KILLER)
                // ==============================================================================
                if (!packetsMap.TryGetValue(gameDb.ExternalId, out var packets))
                {
                    // Se o jogo sumiu da API e faz tempo que não atualiza (3 min), assumimos que acabou.
                    if (gameDb.LastUpdate < DateTime.UtcNow.AddMinutes(-3))
                    {
                        _logger.LogWarning($"💀 [ZOMBIE KILLER] O jogo {gameDb.ExternalId} sumiu da API. Encerrando...");

                        // Marca como encerrado no banco
                        gameDb.Status = "Ended";
                        gameDb.GameTime = "FT";

                        // Salva imediatamente
                        context.Entry(gameDb).State = EntityState.Modified;
                        await context.SaveChangesAsync();

                        // Limpa do Redis e avisa o Front para remover
                        await ForceRemoveGameAsync(gameDb.ExternalId);

                        idsToRemoveNow.Add(gameDb.ExternalId);
                        endedGameIds.Add(gameDb.ExternalId);
                    }
                    continue; // Agora sim pode continuar
                }
                // ==============================================================================
                gameDb.LastUpdate = DateTime.UtcNow; // <--- ADICIONE ESTA LINHA AQUI

                var ev = packets.FirstOrDefault(p => p.Type == "EV");
                var logs = packets.Where(p => p.Type == "ST").ToList();

                if (ev != null)
                {
                    // (Resto do código mantido igual...)
                    // VERIFICAÇÃO DE FIM
                    bool isEnded = ev.TimeStatus == "3" || ev.Status == "3";
                    if (int.TryParse(ev.Tm, out int min) && min > 100 && ev.Tt == "0") isEnded = true;
                    if ((ev.TimeStatus == "0" || ev.Status == "0") && ev.Tm == "90") isEnded = true;

                    if (isEnded)
                    {
                        gameDb.Status = "Ended";
                        gameDb.GameTime = "FT";
                        if (!string.IsNullOrEmpty(ev.Ss)) gameDb.Score = ev.Ss;

                        endedGameIds.Add(gameDb.ExternalId);
                        idsToRemoveNow.Add(gameDb.ExternalId);

                        context.Entry(gameDb).State = EntityState.Modified;
                        await context.SaveChangesAsync();
                        await ForceRemoveGameAsync(gameDb.ExternalId);
                    }
                    else
                    {
                        // JOGO VIVO
                        string sportKey = (gameDb.SportKey ?? "").ToLower();
                        bool changed = false;

                        if (sportKey.Contains("soccer") || sportKey.Contains("futebol")) changed = UpdateSoccer(gameDb, ev, logs);
                        else if (sportKey.Contains("basket")) changed = UpdateBasketball(gameDb, ev);
                        else if (sportKey.Contains("tennis")) changed = UpdateTennis(gameDb, ev);
                        else if (sportKey.Contains("volley")) changed = UpdateVolleyball(gameDb, ev);
                        else changed = UpdateGenericSport(gameDb, ev);

                        ProcessOddsSequential(gameDb, packets);

                        // A. Salva no Redis
                        var cacheKey = $"live_game:{gameDb.ExternalId.Trim()}";
                        await _redisDb.StringSetAsync(cacheKey, JsonSerializer.Serialize(gameDb, _jsonOptions), TimeSpan.FromHours(1));

                        // B. Marca como "Sujo" para persistência posterior
                        if (changed || gameDb.LastUpdate > DateTime.UtcNow.AddSeconds(-30))
                        {
                            await _redisDb.SetAddAsync("dirty_live_games", gameDb.ExternalId.Trim());
                        }

                        // Busca stats apenas para envio visual (não bloqueante)
                        var statDb = await context.Set<LiveGameStat>().AsNoTracking().FirstOrDefaultAsync(s => s.GameId == gameDb.ExternalId);

                        updatesToSend.Add(new
                        {
                            id = gameDb.ExternalId,
                            time = gameDb.GameTime,
                            score = gameDb.Score,
                            //status = gameDb.Status ?? "Live",
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

            // SignalR Updates
            if (idsToRemoveNow.Any())
            {
                await _hubContext.Clients.All.SendAsync("RemoveGames", idsToRemoveNow.ToList());
            }

            if (updatesToSend.Any())
            {
                await _hubContext.Clients.All.SendAsync("LiveOddsUpdate", updatesToSend);
            }
        }


        private async Task ForceRemoveGameAsync(string gameId)
        {
            var cleanId = gameId.Trim();
            await _redisDb.KeyDeleteAsync($"live_game:{cleanId}");
            await _redisDb.SetRemoveAsync("dirty_live_games", cleanId);
        }

        // ==========================================================================================
        // PARSERS (LÓGICA PURA)
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
                        else novoTempo = $"{minutosApi}'";
                    }
                    catch { novoTempo = $"{minutosApi}'"; }
                }
                else novoTempo = $"{minutosApi}'";
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
                        else if (!string.IsNullOrEmpty(name)) isReadingMainMarket = false;
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
                _logger.LogError($"Erro parsing odds: {ex.Message}");
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