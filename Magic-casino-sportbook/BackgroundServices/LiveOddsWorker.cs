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

                    // 🔥 CORREÇÃO 1: Janela de tempo
                    // Busca jogos Live ou Ended recentemente (últimos 10 min) para garantir que o update final seja enviado
                    var lookbackTime = DateTime.UtcNow.AddMinutes(-10);

                    var liveGames = await context.SportsEvents
                        .Where(g => g.Status == "Live" || (g.Status == "Ended" && g.LastUpdate > lookbackTime))
                        .OrderBy(g => g.LastUpdate)
                        .Take(40)
                        .ToListAsync(ct);

                    if (liveGames.Any())
                    {
                        // Atualiza na API
                        var endedGameIds = await liveService.UpdateLiveGamesAsync(liveGames, context);
                        await context.SaveChangesAsync(ct);

                        // 3. 🔥 CORREÇÃO CRÍTICA AQUI 🔥
                        // AQUI ESTAVA O ERRO: .Where(g => g.Status == "Live")
                        // MUDAMOS PARA: Incluir "Ended" e "Completed".
                        // Isso permite que o Frontend receba a atualização de status e remova o jogo visualmente.
                        var activeLiveGames = liveGames
                            .Where(g => g.Status == "Live" || g.Status == "Ended" || g.Status == "Completed")
                            .ToList();

                        if (activeLiveGames.Any())
                        {
                            await hub.Clients.All.SendAsync("LiveOddsUpdate", activeLiveGames.Select(g => new
                            {
                                id = g.ExternalId,
                                score = g.Score,
                                time = !string.IsNullOrEmpty(g.GameTime) ? g.GameTime : "Live",
                                status = g.Status, // Agora enviará "Ended", acionando a remoção no Vue
                                homeOdd = g.RawOddsHome,
                                drawOdd = g.RawOddsDraw,
                                awayOdd = g.RawOddsAway
                            }), ct);
                        }

                        // 4. Reforço de Remoção (RemoveGames)
                        if (endedGameIds != null && endedGameIds.Any())
                        {
                            await hub.Clients.All.SendAsync("RemoveGames", endedGameIds, ct);
                        }
                    }

                    // 5. ASPIRADOR DE PÓ (Limpeza profunda para jogos antigos travados)
                    // Aumentei para 24h para garantir que seu jogo zumbi atual seja pego
                    var recentlyEnded = await context.SportsEvents
                        .AsNoTracking()
                        .Where(g => g.Status == "Ended" && g.LastUpdate > DateTime.UtcNow.AddHours(-24))
                        .ToListAsync(ct);

                    if (recentlyEnded.Any())
                    {
                        await liveService.CleanupZombiesAsync(recentlyEnded);
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