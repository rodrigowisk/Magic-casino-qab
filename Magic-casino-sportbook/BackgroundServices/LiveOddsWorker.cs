using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Hubs;
using Magic_casino_sportbook.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class LiveOddsWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LiveOddsWorker> _logger;
        private const int DELAY_LIVE_MS = 10000; // 10s (Atualiza placar\odd)

        public LiveOddsWorker(IServiceProvider serviceProvider, ILogger<LiveOddsWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ [LIVE ODDS WORKER] Iniciado. Monitorando jogos ao vivo.");

            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessLiveZone(stoppingToken);
                await Task.Delay(DELAY_LIVE_MS, stoppingToken);
            }
        }

        private async Task ProcessLiveZone(CancellationToken ct)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var liveService = scope.ServiceProvider.GetRequiredService<LiveSportService>();
                    var hub = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();

                    // Busca jogos que já estão rolando
                    var liveGames = await context.SportsEvents
                        .Where(g => g.Status == "Live")
                        .OrderBy(g => g.LastUpdate)
                        .Take(20)
                        .ToListAsync(ct);

                    if (liveGames.Any())
                    {
                        // 1. Atualiza dados na API e obtém IDs encerrados
                        var endedGameIds = await liveService.UpdateLiveGamesAsync(liveGames, context);

                        // 2. Persiste alterações (Placar, Odds, Tempo)
                        await context.SaveChangesAsync(ct);

                        // 3. Envia atualização para o Front
                        var activeLiveGames = liveGames.Where(g => g.Status == "Live").ToList();
                        if (activeLiveGames.Any())
                        {
                            await hub.Clients.All.SendAsync("LiveOddsUpdate", activeLiveGames.Select(g => new
                            {
                                id = g.ExternalId,
                                score = g.Score,
                                time = !string.IsNullOrEmpty(g.GameTime) ? g.GameTime : "Live",
                                status = g.Status,
                                homeOdd = g.RawOddsHome,
                                drawOdd = g.RawOddsDraw,
                                awayOdd = g.RawOddsAway
                            }), ct);
                        }

                        // 4. Remove jogos encerrados
                        if (endedGameIds != null && endedGameIds.Any())
                        {
                            _logger.LogInformation($"🏁 Removendo {endedGameIds.Count} jogos encerrados.");
                            await hub.Clients.All.SendAsync("RemoveGames", endedGameIds, ct);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro LiveOddsWorker: {ex.Message}");
            }
        }
    }
}