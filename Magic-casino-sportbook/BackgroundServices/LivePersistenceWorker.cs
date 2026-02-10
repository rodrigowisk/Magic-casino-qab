using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class LivePersistenceWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<LivePersistenceWorker> _logger;

        public LivePersistenceWorker(IServiceProvider serviceProvider, IConnectionMultiplexer redis, ILogger<LivePersistenceWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _redis = redis;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("💾 [PERSISTENCE] Worker de Sincronização Redis->SQL iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Roda a cada 45 segundos (tempo suficiente para não gargalar o banco)
                    await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
                    await SyncRedisToSql();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Erro no LivePersistenceWorker: {ex.Message}");
                }
            }
        }

        private async Task SyncRedisToSql()
        {
            var db = _redis.GetDatabase();

            // 1. Pega a lista de IDs "sujos" (que mudaram desde a última gravação)
            // Vamos implementar a lógica de adicionar nesse Set no LiveSportService
            var dirtyIds = await db.SetMembersAsync("dirty_live_games");

            if (dirtyIds.Length == 0) return;

            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                foreach (var redisValue in dirtyIds)
                {
                    string gameId = redisValue.ToString();

                    // 2. Busca o dado fresquinho no Redis
                    var json = await db.StringGetAsync($"live_game:{gameId}");
                    if (!json.HasValue) continue;

                    var gameRedis = JsonSerializer.Deserialize<SportsEvent>(json!);

                    // 3. Busca o dado no Banco (apenas para anexar ao contexto)
                    var gameDb = await context.SportsEvents.FirstOrDefaultAsync(g => g.ExternalId == gameId);

                    if (gameDb != null && gameRedis != null)
                    {
                        // 4. Atualiza apenas o essencial
                        gameDb.Score = gameRedis.Score;
                        gameDb.GameTime = gameRedis.GameTime;
                        gameDb.Status = gameRedis.Status;
                        gameDb.RawOddsHome = gameRedis.RawOddsHome;
                        gameDb.RawOddsDraw = gameRedis.RawOddsDraw;
                        gameDb.RawOddsAway = gameRedis.RawOddsAway;
                        gameDb.LastUpdate = DateTime.UtcNow;
                    }
                }

                // 5. Salva tudo de uma vez no SQL
                await context.SaveChangesAsync();

                // 6. Limpa a lista de sujos (apenas os que processamos)
                await db.SetRemoveAsync("dirty_live_games", dirtyIds);

                _logger.LogInformation($"💾 [PERSISTENCE] {dirtyIds.Length} jogos sincronizados com o SQL.");
            }
        }
    }
}