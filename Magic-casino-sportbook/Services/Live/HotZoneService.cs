using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Services.Gateways;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Magic_casino_sportbook.Services.Live
{
    public class HotZoneService
    {
        private readonly BetsApiHttpService _api;
        private readonly IDatabase _redisDb;
        private readonly ILogger<HotZoneService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public HotZoneService(
            BetsApiHttpService api,
            IConnectionMultiplexer redis,
            ILogger<HotZoneService> logger)
        {
            _api = api;
            _redisDb = redis.GetDatabase();
            _logger = logger;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
        }

        public async Task<List<string>> VerifyKickoffWithApiAsync(List<SportsEvent> candidates, IServiceProvider sp)
        {
            var idsToRemoveFromPreMatch = new List<string>();
            if (!candidates.Any()) return idsToRemoveFromPreMatch;

            using var scope = sp.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Anexa os objetos ao contexto se estiverem desconectados
            foreach (var c in candidates)
            {
                if (context.Entry(c).State == EntityState.Detached)
                    context.Attach(c);
            }

            // Processa em lotes pequenos
            var batches = candidates.Chunk(10).ToList();

            foreach (var batch in batches)
            {
                // ==============================================================================
                // ✅ FIX CRÍTICO: ROTAÇÃO DE FILA
                // ==============================================================================
                // Atualizamos a data de TODOS os jogos do lote agora.
                // Isso garante que o GameStatusWorker (que filtra por LastUpdate < -2min)
                // IGNORE esses jogos na próxima execução, permitindo que a fila ande.
                foreach (var game in batch)
                {
                    game.LastUpdate = DateTime.UtcNow;
                    if (context.Entry(game).State == EntityState.Unchanged)
                        context.Entry(game).State = EntityState.Modified;
                }
                // ==============================================================================

                var gameIds = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
                var url = $"/v1/bet365/event?FI={gameIds}";
                bool batchFailed = false;

                try
                {
                    // Prioridade alta pois define quem entra no ao vivo
                    var response = await _api.GetAsync(url, isLivePriority: true);

                    if (response == null)
                    {
                        _logger.LogWarning("⚠️ [HOTZONE] Falha na API (response null). Tentando fallback...");
                        batchFailed = true; // Força a ir para o processamento individual
                    }
                    else if ((int)response.StatusCode == 429)
                    {
                        _logger.LogWarning("⛔ [HOTZONE] 429 Detectado! Pausando 5s...");
                        await Task.Delay(5000);
                        continue; // Pula este lote, mas como já atualizamos o LastUpdate acima, ele vai para o fim da fila
                    }
                    else
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();

                        if (jsonString.Contains("PARAM_INVALID") || jsonString.Contains("failure"))
                        {
                            batchFailed = true;
                            _logger.LogWarning("⚠️ [HOTZONE] Lote contaminado. Iniciando verificação individual...");
                        }
                        else if (!string.IsNullOrWhiteSpace(jsonString))
                        {
                            await ProcessHotZonePacket(jsonString, batch.ToList(), context, idsToRemoveFromPreMatch);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"⚠️ HotZone Check Erro: {ex.Message}");
                    batchFailed = true;
                }

                // Lógica de Resgate (Fallback Individual)
                if (batchFailed)
                {
                    foreach (var game in batch)
                    {
                        try
                        {
                            var singleUrl = $"/v1/bet365/event?FI={game.ExternalId.Trim()}";
                            await Task.Delay(200); // Delay para não estourar rate limit

                            var resp = await _api.GetAsync(singleUrl, isLivePriority: true);

                            if (resp == null) continue;

                            if ((int)resp.StatusCode == 429)
                            {
                                await Task.Delay(2000);
                                continue;
                            }

                            var json = await resp.Content.ReadAsStringAsync();

                            if (json.Contains("PARAM_INVALID") || json.Contains("failure"))
                            {
                                _logger.LogWarning($"🗑️ [HOTZONE RESGATE] Removendo jogo inválido: {game.ExternalId}");
                                game.Status = "Ended";
                                idsToRemoveFromPreMatch.Add(game.ExternalId);

                                // Limpa do Redis se existir
                                await _redisDb.KeyDeleteAsync($"live_game:{game.ExternalId.Trim()}");
                            }
                            else
                            {
                                await ProcessHotZonePacket(json, new List<SportsEvent> { game }, context, idsToRemoveFromPreMatch);
                            }
                        }
                        catch { }
                    }
                }

                // Salva as alterações (incluindo o LastUpdate atualizado lá em cima)
                await context.SaveChangesAsync();
                await Task.Delay(1000);
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
                        _logger.LogInformation($"✅ GO LIVE: {gameDb.HomeTeam} (ID: {gameDb.ExternalId})");
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