using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class LiveOddsWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LiveOddsWorker> _logger;

        // Tempo entre ciclos (10 segundos)
        private const int DELAY_LIVE_MS = 10000;

        public LiveOddsWorker(IServiceProvider serviceProvider, ILogger<LiveOddsWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ [LIVE ODDS WORKER] Iniciado. Monitorando jogos ao vivo (Redis-First).");

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

                    // 1. Pega APENAS os jogos que estão marcados como AO VIVO no banco
                    // (Essa leitura é rápida pois usa índice no Status)
                    var liveGames = await context.SportsEvents
                        .Where(g => g.Status == "Live")
                        .ToListAsync(ct);

                    if (liveGames.Any())
                    {
                        // 2. Atualiza Odds/Placar usando a API Externa
                        // O Serviço agora salva no Redis e marca o jogo como "dirty"
                        // NÃO SALVAMOS NO SQL AQUI! (Isso remove o gargalo)
                        var idsToRemove = await liveService.UpdateLiveGamesAsync(liveGames, context);

                        // 3. Se algum jogo acabou ou sumiu, avisamos o Frontend para remover da tela
                        if (idsToRemove != null && idsToRemove.Any())
                        {
                            await hub.Clients.All.SendAsync("RemoveGames", idsToRemove, ct);
                        }
                    }

                    // 4. ASPIRADOR DE PÓ (Limpeza de segurança)
                    // Pega jogos que estão como "Ended" no banco mas ainda podem estar ocupando memória
                    // Rodamos isso apenas se houver folga, ou a cada ciclo, pois é uma query leve com índice.
                    // Para performance extrema, poderíamos rodar isso em outro worker, mas aqui está ok.
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