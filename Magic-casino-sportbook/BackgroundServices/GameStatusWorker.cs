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

        // Diminuí levemente o delay para ele ser mais ágil na virada do minuto
        private const int DELAY_HOT_MS = 10000;

        public GameStatusWorker(IServiceProvider serviceProvider, ILogger<GameStatusWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚦 [GAME STATUS WORKER] Iniciado v6.0 (Tolerância 1min).");

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

                    // ====================================================================================
                    // 🔥 LÓGICA CORRIGIDA - TOLERÂNCIA DE 1 MINUTO
                    // ====================================================================================
                    // 1. Prematch: Só pegamos se JÁ PASSOU 1 minuto do início (UtcNow.AddMinutes(-1)).
                    //    Isso evita marcar como "Delayed" jogos que começaram agora (ex: 18:00:05).
                    //    Damos 60 segundos para o LiveOddsWorker detectar o "In Play" primeiro.
                    //
                    // 2. WaitingLive: Pegamos sempre para continuar monitorando se virou Live ou encerrou.
                    // ====================================================================================

                    var toleranceThreshold = DateTime.UtcNow.AddMinutes(-1); // 1 minuto ATRÁS
                    var lookbackLimit = DateTime.UtcNow.AddHours(-4); // Não olhar jogos muito velhos

                    var gamesToCheck = await context.SportsEvents
                        .AsNoTracking()
                        .Where(g =>
                            // CASO A: Jogos que já estão no limbo "WaitingLive" (verificar sempre)
                            (g.Status == "WaitingLive" && g.CommenceTime > lookbackLimit)
                            ||
                            // CASO B: Jogos Prematch que DEVERIAM ter começado há mais de 1 minuto
                            (g.Status == "Prematch" && g.CommenceTime <= toleranceThreshold && g.CommenceTime > lookbackLimit)
                        )
                        .OrderBy(g => g.CommenceTime)
                        .Take(60)
                        .ToListAsync(ct);

                    if (gamesToCheck.Any())
                    {
                        // Verifica na API. Se não estiver Live, o serviço deve marcar como WaitingLive/Delayed.
                        var idsToRemoveFromPreMatch = await liveService.VerifyKickoffWithApiAsync(gamesToCheck, scope.ServiceProvider);

                        if (idsToRemoveFromPreMatch.Any())
                        {
                            _logger.LogInformation($"🔥 [HOTZONE] Atualizado: {idsToRemoveFromPreMatch.Count} jogos processados (Live ou Delay).");

                            // 1. Busca quem virou REALMENTE LIVE para avisar o Frontend (Aparecer na aba Ao Vivo)
                            var reallyLiveGames = await context.SportsEvents
                                .AsNoTracking()
                                .Where(g => idsToRemoveFromPreMatch.Contains(g.ExternalId) && g.Status == "Live")
                                .ToListAsync(ct);

                            if (reallyLiveGames.Any())
                            {
                                await hub.Clients.All.SendAsync("GameWentLive", reallyLiveGames, ct);
                            }

                            // 2. Remove TODOS da lista de Pré-Jogo (Seja porque virou Live ou Delayed)
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