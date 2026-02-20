using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Services.Live;
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

        // Mantemos os 10s que você já configurou
        private const int DELAY_HOT_MS = 10000;

        public GameStatusWorker(IServiceProvider serviceProvider, ILogger<GameStatusWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚦 [GAME STATUS] Worker Iniciado (Limpeza Imediata + Verificação).");

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
                    var hotZoneService = scope.ServiceProvider.GetRequiredService<HotZoneService>();
                    var hub = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();

                    var agora = DateTime.UtcNow;

                    // ==============================================================================
                    // 1. A GUILHOTINA (Limpeza de Pré-Jogo Vencido)
                    // ==============================================================================
                    var jogosVencidos = await context.SportsEvents
                        .Where(g => g.Status == "Prematch" && g.CommenceTime <= agora)
                        .Take(100)
                        .ToListAsync(ct);

                    if (jogosVencidos.Any())
                    {
                        var idsRemover = new List<string>();

                        foreach (var jogo in jogosVencidos)
                        {
                            jogo.Status = "WaitingLive";
                            jogo.LastUpdate = DateTime.UtcNow;
                            idsRemover.Add(jogo.ExternalId);
                        }

                        await context.SaveChangesAsync(ct);
                        await hub.Clients.All.SendAsync("RemoveGames", idsRemover, ct);
                        _logger.LogInformation($"🔪 [LIMPEZA] {idsRemover.Count} jogos vencidos removidos da tela.");
                    }

                    // ==============================================================================
                    // 2. A VERIFICAÇÃO (Com Rotação de Fila)
                    // ==============================================================================

                    var lookbackLimit = DateTime.UtcNow.AddHours(-4);

                    // ✅ FIX DO ERRO DE BUILD: Definição das variáveis de tempo
                    var toleranceThreshold = DateTime.UtcNow.AddMinutes(-1);
                    var cooldownLimit = DateTime.UtcNow.AddMinutes(-2);

                    var gamesToCheck = await context.SportsEvents
                        .AsNoTracking()
                        .Where(g =>
                            // 👇 MÁGICA: Ignora quem foi verificado recentemente (Rotação de Fila)
                            g.LastUpdate < cooldownLimit
                            &&
                            (
                                // CASO A: Jogos que já estão no limbo "WaitingLive"
                                (g.Status == "WaitingLive" && g.CommenceTime > lookbackLimit)
                                ||
                                // CASO B: Jogos Prematch atrasados
                                (g.Status == "Prematch" && g.CommenceTime <= toleranceThreshold && g.CommenceTime > lookbackLimit)
                            )
                        )
                        .OrderBy(g => g.CommenceTime)
                        .Take(50)
                        .ToListAsync(ct);

                    if (gamesToCheck.Any())
                    {
                        // O HotZoneService atualiza o LastUpdate de todos, garantindo a rotação
                        var idsProcessados = await hotZoneService.VerifyKickoffWithApiAsync(gamesToCheck, scope.ServiceProvider);

                        var reallyLiveGames = await context.SportsEvents
                            .AsNoTracking()
                            .Where(g => idsProcessados.Contains(g.ExternalId) && g.Status == "Live")
                            .ToListAsync(ct);

                        if (reallyLiveGames.Any())
                        {
                            await hub.Clients.All.SendAsync("GameWentLive", reallyLiveGames, ct);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Erro GameStatusWorker: {ex.Message}");
            }
        }
    }
}