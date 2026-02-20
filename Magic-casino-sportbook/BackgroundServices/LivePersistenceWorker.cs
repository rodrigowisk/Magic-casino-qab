using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class LivePersistenceWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<LivePersistenceWorker> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public LivePersistenceWorker(IServiceProvider serviceProvider, IConnectionMultiplexer redis, ILogger<LivePersistenceWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _redis = redis;
            _logger = logger;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 [PERSISTENCE] Worker Bulk-Update (SQL Puro) iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Roda a cada 45 segundos
                    await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
                    await SyncRedisToSqlBulkAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Erro no LivePersistenceWorker: {ex.Message}");
                }
            }
        }

        private async Task SyncRedisToSqlBulkAsync()
        {
            var db = _redis.GetDatabase();

            // 1. Pega lista de IDs que mudaram
            var dirtyIdsRedis = await db.SetMembersAsync("dirty_live_games");
            if (dirtyIdsRedis.Length == 0) return;

            var gamesToUpdate = new List<SportsEvent>();
            var processedIds = new List<RedisValue>();

            // 2. Coleta dados do Redis
            foreach (var redisValue in dirtyIdsRedis)
            {
                var gameId = redisValue.ToString();
                var json = await db.StringGetAsync($"live_game:{gameId}");

                if (json.HasValue)
                {
                    try
                    {
                        var game = JsonSerializer.Deserialize<SportsEvent>(json!, _jsonOptions);
                        if (game != null)
                        {
                            // 🛡️ SANITIZAÇÃO DE MEMÓRIA 🛡️
                            // Se o Redis disser que o tempo é "Ended" ou "FT", força o status para "Ended"
                            // Isso corrige dados contraditórios vindos do updater
                            if (game.GameTime == "FT" || game.GameTime == "Ended" || game.GameTime == "90+")
                            {
                                if (game.Status == "Live")
                                {
                                    game.Status = "Ended";
                                }
                            }

                            gamesToUpdate.Add(game);
                            processedIds.Add(redisValue);
                        }
                    }
                    catch { /* Ignora JSON corrompido */ }
                }
                else
                {
                    // Se não tá no Redis mas tá na lista de sujos, removemos da lista de sujos
                    processedIds.Add(redisValue);
                }
            }

            if (gamesToUpdate.Any())
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // 3. EXECUTAR BULK UPDATE (RAW SQL)
                    await ExecutePostgresBulkUpdate(context, gamesToUpdate);
                }

                // 4. Limpa a lista de sujos no Redis
                if (processedIds.Any())
                {
                    await db.SetRemoveAsync("dirty_live_games", processedIds.ToArray());
                }

                _logger.LogInformation($"⚡ [BULK UPDATE] {gamesToUpdate.Count} jogos atualizados em uma única transação.");
            }
        }

        private async Task ExecutePostgresBulkUpdate(AppDbContext context, List<SportsEvent> games)
        {
            var sb = new StringBuilder();
            var parameters = new List<object>();
            var paramIndex = 0;

            sb.Append("UPDATE \"SportsEvents\" AS s SET ");
            sb.Append("\"Score\" = v.score, ");
            sb.Append("\"GameTime\" = v.time, ");

            // 🔥🔥🔥 AQUI ESTÁ A PROTEÇÃO NO BANCO DE DADOS 🔥🔥🔥
            // A lógica CASE WHEN diz: 
            // "Se o jogo no banco JÁ ESTÁ 'Ended', MANTENHA 'Ended'. Caso contrário, use o valor novo (v.status)."
            sb.Append("\"Status\" = CASE WHEN s.\"Status\" = 'Ended' THEN 'Ended' ELSE v.status END, ");

            sb.Append("\"RawOddsHome\" = CAST(v.home AS decimal), ");
            sb.Append("\"RawOddsDraw\" = CAST(v.draw AS decimal), ");
            sb.Append("\"RawOddsAway\" = CAST(v.away AS decimal), ");
            sb.Append("\"LastUpdate\" = CAST(v.updated AS timestamp with time zone) ");
            sb.Append("FROM (VALUES ");

            for (int i = 0; i < games.Count; i++)
            {
                var g = games[i];
                if (i > 0) sb.Append(", ");

                sb.Append($"(@p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++}, @p{paramIndex++})");

                parameters.Add(g.ExternalId);
                parameters.Add(g.Score ?? "0-0");
                parameters.Add(g.GameTime ?? "0'");
                parameters.Add(g.Status ?? "Live");
                parameters.Add(g.RawOddsHome);
                parameters.Add(g.RawOddsDraw);
                parameters.Add(g.RawOddsAway);
                parameters.Add(DateTime.UtcNow);
            }

            sb.Append(") AS v(id, score, time, status, home, draw, away, updated) ");
            sb.Append("WHERE s.\"ExternalId\" = v.id");

            await context.Database.ExecuteSqlRawAsync(sb.ToString(), parameters.ToArray());
        }
    }
}