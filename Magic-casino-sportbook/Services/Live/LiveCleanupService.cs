using Magic_casino_sportbook.Hubs;
using Magic_casino_sportbook.Models;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Magic_casino_sportbook.Services.Live
{
    public class LiveCleanupService
    {
        private readonly IDatabase _redisDb;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly ILogger<LiveCleanupService> _logger;

        public LiveCleanupService(
            IConnectionMultiplexer redis,
            IHubContext<GameHub> hubContext,
            ILogger<LiveCleanupService> logger)
        {
            _redisDb = redis.GetDatabase();
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task CleanupZombiesAsync(List<SportsEvent> endedGames)
        {
            if (endedGames == null || !endedGames.Any()) return;

            var idsToRemove = new List<string>();

            foreach (var game in endedGames)
            {
                // 🔥 GARANTIA DE TRIM: Remove espaços que quebram a chave do Redis
                var cleanId = game.ExternalId.Trim();
                var cacheKey = $"live_game:{cleanId}";

                // 1. Deleta do Redis explicitamente
                await _redisDb.KeyDeleteAsync(cacheKey);

                // 2. Remove da lista de persistência (para não salvar de novo)
                await _redisDb.SetRemoveAsync("dirty_live_games", cleanId);

                idsToRemove.Add(cleanId);
            }

            // 3. Força envio do sinal de remoção para o Frontend
            if (idsToRemove.Any())
            {
                _logger.LogInformation($"🧹 [ZOMBIE] Removendo {idsToRemove.Count} jogos encerrados do cache/front.");
                await _hubContext.Clients.All.SendAsync("RemoveGames", idsToRemove);
            }
        }
    }
}