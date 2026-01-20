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

        private const int DELAY_LIVE_MS = 3000;   // 3 segundos (Rápido para ao vivo)
        private const int DELAY_HOT_MS = 15000;   // 15 segundos (Para checar pré-jogos)

        public LiveUpdateWorker(IServiceProvider serviceProvider, ILogger<LiveUpdateWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔥 [LIVE WORKER] Iniciado. Gerenciando Zonas Quente e Ao Vivo.");

            // Roda as duas tarefas em paralelo
            var liveTask = ProcessLiveZone(stoppingToken);
            var hotTask = ProcessHotZone(stoppingToken);

            await Task.WhenAll(liveTask, hotTask);
        }

        // ==============================================================================
        // ⚡ ZONA 1: JOGOS AO VIVO (Atualiza Placar e Odds)
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

                        // Busca jogos marcados como 'Live', priorizando os que não atualizamos há mais tempo
                        var liveGames = await context.SportsEvents
                            .Where(g => g.Status == "Live")
                            .OrderBy(g => g.LastUpdate)
                            .Take(20) // Lote menor para ser mais rápido
                            .ToListAsync(ct);

                        if (liveGames.Any())
                        {
                            // Chama o serviço que AGORA preenche a tabela LiveGameStat corretamente
                            var endedGameIds = await liveService.UpdateLiveGamesAsync(liveGames, context);
                            await context.SaveChangesAsync(ct);

                            // Notifica o Frontend via SignalR
                            // NOTA: O Frontend precisa receber o 'gameTime' e 'score' atualizados
                            await hub.Clients.All.SendAsync("LiveOddsUpdate", liveGames.Select(g => new
                            {
                                id = g.ExternalId, // ID que o Vue usa
                                score = g.Score,
                                time = g.Status == "Live" ? "Live" : g.Status, // Ou pegue de LiveGameStat se tiver acesso aqui
                                status = g.Status,
                                homeOdd = g.RawOddsHome,
                                drawOdd = g.RawOddsDraw,
                                awayOdd = g.RawOddsAway
                            }), ct);

                            // Se algum jogo acabou, avisa para remover da lista ao vivo
                            if (endedGameIds != null && endedGameIds.Any())
                            {
                                await hub.Clients.All.SendAsync("RemoveGames", endedGameIds, ct);
                                _logger.LogInformation($"🏁 [FIM DE JOGO] {endedGameIds.Count} jogos finalizados.");
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
        // 🧹 ZONA 2: VARREDURA DE PRÉ-JOGO (Muda Status para Live)
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

                        // Pega jogos que deveriam ter começado (CommenceTime passado) mas ainda estão 'Prematch'
                        var gamesToKickoff = await context.SportsEvents
                            .Where(g => g.Status == "Prematch" &&
                                        g.CommenceTime <= DateTime.UtcNow &&
                                        g.CommenceTime > DateTime.UtcNow.AddHours(-3)) // Janela de segurança
                            .OrderBy(g => g.CommenceTime)
                            .Take(50)
                            .ToListAsync(ct);

                        if (gamesToKickoff.Any())
                        {
                            _logger.LogInformation($"⚽ [KICKOFF] Verificando {gamesToKickoff.Count} jogos iniciando...");
                            await liveService.CheckForKickoffAsync(gamesToKickoff, context);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Erro na Varredura HotZone: {ex.Message}");
                }

                await Task.Delay(DELAY_HOT_MS, ct);
            }
        }
    }
}