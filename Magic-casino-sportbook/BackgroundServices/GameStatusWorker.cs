using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Hubs;
using Magic_casino_sportbook.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class GameStatusWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GameStatusWorker> _logger;

        // Mantemos 15s para ser rápido
        private const int DELAY_HOT_MS = 15000;

        public GameStatusWorker(IServiceProvider serviceProvider, ILogger<GameStatusWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚦 [GAME STATUS WORKER] Iniciado v5.0 (Lógica WaitingLive).");

            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessHotZone(stoppingToken);
                await Task.Delay(DELAY_HOT_MS, stoppingToken);
            }
        }

        private async Task ProcessHotZone(CancellationToken ct)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var liveService = scope.ServiceProvider.GetRequiredService<LiveSportService>();
                    var hub = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();

                    // 🔥 ALTERAÇÃO 1: Busca jogos Prematch (para iniciar) OU WaitingLive (que estão no limbo de 5min)
                    var gamesToCheck = await context.SportsEvents
                        .AsNoTracking()
                        .Where(g => (g.Status == "Prematch" || g.Status == "WaitingLive") &&
                                    g.CommenceTime <= DateTime.UtcNow.AddMinutes(5) && // Próximos 5 min
                                    g.CommenceTime > DateTime.UtcNow.AddHours(-3))     // Janela de segurança
                        .OrderBy(g => g.CommenceTime)
                        .Take(60) // Aumentei um pouco o lote
                        .ToListAsync(ct);

                    if (gamesToCheck.Any())
                    {
                        // O método agora retorna uma lista de IDs para REMOVER DA TELA DE PRÉ-JOGO
                        // (Seja porque virou Live, ou porque virou WaitingLive)
                        var idsToRemoveFromPreMatch = await liveService.VerifyKickoffWithApiAsync(gamesToCheck, scope.ServiceProvider);

                        if (idsToRemoveFromPreMatch.Any())
                        {
                            _logger.LogInformation($"🔥 [HOTZONE] Processado: {idsToRemoveFromPreMatch.Count} jogos saíram do Pré-Jogo.");

                            // 1. Notifica quem virou REALMENTE LIVE para aparecer na aba "Ao Vivo"
                            var reallyLiveGames = await context.SportsEvents
                                .AsNoTracking()
                                .Where(g => idsToRemoveFromPreMatch.Contains(g.ExternalId) && g.Status == "Live")
                                .ToListAsync(ct);

                            if (reallyLiveGames.Any())
                            {
                                await hub.Clients.All.SendAsync("GameWentLive", reallyLiveGames, ct);
                            }

                            // 2. Remove TODOS da lista de Pré-Jogo (Live, WaitingLive, Delayed, Ended)
                            await hub.Clients.All.SendAsync("RemoveGames", idsToRemoveFromPreMatch, ct);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro GameStatusWorker: {ex.Message}");
            }
        }
    }
}