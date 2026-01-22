using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Hubs;
using Magic_casino_sportbook.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class LiveUpdateWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LiveUpdateWorker> _logger;

        // 📉 AJUSTE DE FLUXO: Aumentamos os tempos para economizar requisições
        // Antes: 10000 (10s) -> Agora: 25000 (25s)
        // Isso reduz a carga na API em mais de 50%, evitando o erro 429/409.
        private const int DELAY_LIVE_MS = 25000;

        // Antes: 15000 (15s) -> Agora: 60000 (1 minuto)
        // Verificar se jogo começou não precisa ser tão frequente.
        private const int DELAY_HOT_MS = 60000;

        public LiveUpdateWorker(IServiceProvider serviceProvider, ILogger<LiveUpdateWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔥 [LIVE WORKER] Iniciado. Modo Econômico (Intervalo 25s).");
            await Task.WhenAll(ProcessLiveZone(stoppingToken), ProcessHotZone(stoppingToken));
        }

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

                        // Filtra jogos ativos (Soccer ou Categoria Nula)
                        // Ordena por LastUpdate para garantir rotação da fila
                        var liveGames = await context.SportsEvents
                            .Where(g => g.Status == "Live" &&
                                       (g.SportCategory == "Soccer" || g.SportCategory == null || g.SportCategory == ""))
                            .OrderBy(g => g.LastUpdate)
                            .Take(10) // Lote de 10 jogos por ciclo
                            .ToListAsync(ct);

                        if (liveGames.Any())
                        {
                            // Atualiza os dados na API
                            var endedGameIds = await liveService.UpdateLiveGamesAsync(liveGames, context);

                            // Salva no banco (O Heartbeat atualiza o LastUpdate aqui)
                            await context.SaveChangesAsync(ct);

                            // Envia para o Frontend
                            await hub.Clients.All.SendAsync("LiveOddsUpdate", liveGames.Select(g => new
                            {
                                id = g.ExternalId,
                                score = g.Score,
                                time = !string.IsNullOrEmpty(g.GameTime) ? g.GameTime : "Live",
                                status = g.Status,
                                homeOdd = g.RawOddsHome,
                                drawOdd = g.RawOddsDraw,
                                awayOdd = g.RawOddsAway
                            }), ct);

                            // Remove jogos que a API sinalizou como encerrados
                            if (endedGameIds != null && endedGameIds.Any())
                            {
                                await hub.Clients.All.SendAsync("RemoveGames", endedGameIds, ct);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Erro LiveLoop: {ex.Message}");
                }

                // Aguarda 25 segundos antes do próximo ciclo
                await Task.Delay(DELAY_LIVE_MS, ct);
            }
        }

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
                        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();

                        // Verifica jogos que deveriam ter começado (PreMatch -> Live)
                        var gamesToKickoff = await context.SportsEvents
                            .Where(g => g.Status == "Prematch" &&
                                        (g.SportCategory == "Soccer" || g.SportCategory == null || g.SportCategory == "") &&
                                        g.CommenceTime <= DateTime.UtcNow &&
                                        g.CommenceTime > DateTime.UtcNow.AddHours(-3))
                            .OrderBy(g => g.CommenceTime)
                            .Take(50)
                            .ToListAsync(ct);

                        if (gamesToKickoff.Any())
                        {
                            var newLiveIds = await liveService.CheckForKickoffAsync(gamesToKickoff, context);
                            if (newLiveIds.Any())
                            {
                                await hub.Clients.All.SendAsync("RemoveGames", newLiveIds, ct);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error HotZone: {ex.Message}");
                }
                // Aguarda 1 minuto antes de verificar novos inícios
                await Task.Delay(DELAY_HOT_MS, ct);
            }
        }
    }
}