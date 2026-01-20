using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Hubs;
using Magic_casino_sportbook.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class LiveUpdateWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LiveUpdateWorker> _logger;

        private const int DELAY_LIVE_MS = 3000;   // 3 segundos
        private const int DELAY_HOT_MS = 15000;   // 15 segundos

        public LiveUpdateWorker(IServiceProvider serviceProvider, ILogger<LiveUpdateWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔥 [LIVE WORKER] Iniciado. Gerenciando Zonas Quente e Ao Vivo.");

            var liveTask = ProcessLiveZone(stoppingToken);
            var hotTask = ProcessHotZone(stoppingToken);

            await Task.WhenAll(liveTask, hotTask);
        }

        // ==============================================================================
        // ⚡ ZONA 1: JOGOS AO VIVO
        // ==============================================================================
        private async Task ProcessLiveZone(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var liveService = scope.ServiceProvider.GetRequiredService<LiveSportService>();
                        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();

                        var liveGames = await context.SportsEvents
                            .Where(g => g.Status == "Live")
                            .OrderBy(g => g.LastUpdate)
                            .Take(50)
                            .ToListAsync(ct);

                        if (liveGames.Any())
                        {
                            var endedGameIds = await liveService.UpdateLiveGamesAsync(liveGames, context);
                            await context.SaveChangesAsync(ct);

                            if (liveGames.Any())
                            {
                                await hub.Clients.All.SendAsync("LiveOddsUpdate", liveGames.Select(g => new
                                {
                                    id = g.ExternalId,
                                    score = g.Score,
                                    time = g.GameTime,
                                    status = g.Status,
                                    homeOdd = g.RawOddsHome,
                                    drawOdd = g.RawOddsDraw,
                                    awayOdd = g.RawOddsAway
                                }), ct);
                            }

                            if (endedGameIds != null && endedGameIds.Any())
                            {
                                await hub.Clients.All.SendAsync("RemoveGames", endedGameIds, ct);
                                _logger.LogInformation($"🏁 [FIM DE JOGO] {endedGameIds.Count} jogos finalizados e removidos da tela.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Erro no Loop Live: {ex.Message}");
                }

                await Task.Delay(DELAY_LIVE_MS, ct);
            }
        }

        // ==============================================================================
        // 🔥 ZONA 2: ZONA QUENTE (CheckForKickoff)
        // ==============================================================================
        private async Task ProcessHotZone(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var liveService = scope.ServiceProvider.GetRequiredService<LiveSportService>();

                        // 🛑 AUMENTADO PARA -720 MINUTOS (12 HORAS) PARA PEGAR JOGOS ATRASADOS
                        var now = DateTime.UtcNow;
                        var hotGames = await context.SportsEvents
                            .Where(g => g.Status == "Prematch" &&
                                        g.CommenceTime >= now.AddMinutes(-720) &&
                                        g.CommenceTime <= now.AddMinutes(60))
                            .ToListAsync(ct);

                        if (hotGames.Any())
                        {
                            // Console.WriteLine($"🔥 HOT ZONE: Verificando {hotGames.Count} jogos...");
                            await liveService.CheckForKickoffAsync(hotGames, context);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Erro no Loop Hot Zone: {ex.Message}");
                }

                await Task.Delay(DELAY_HOT_MS, ct);
            }
        }
    }
}