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
                            .Take(20) // Lote para processamento
                            .ToListAsync(ct);

                        if (liveGames.Any())
                        {
                            // Atualiza dados na API e banco
                            var endedGameIds = await liveService.UpdateLiveGamesAsync(liveGames, context);
                            await context.SaveChangesAsync(ct);

                            // Notifica o Frontend via SignalR (Odds e Placar)
                            await hub.Clients.All.SendAsync("LiveOddsUpdate", liveGames.Select(g => new
                            {
                                id = g.ExternalId,
                                score = g.Score,
                                time = g.Status == "Live" ? "Live" : g.Status,
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
        // 🧹 ZONA 2: VARREDURA DE PRÉ-JOGO (Muda Status para Live e Avisa Front)
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

                        // Injetamos o Hub para avisar a remoção
                        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();

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
                            // O serviço verifica, muda status no banco e retorna QUEM mudou
                            var newLiveIds = await liveService.CheckForKickoffAsync(gamesToKickoff, context);

                            // Se a lista não estiver vazia, DISPARA O SIGNALR
                            if (newLiveIds.Any())
                            {
                                // Remove da tela de Pré-Jogo (Pois agora são Live)
                                await hub.Clients.All.SendAsync("RemoveGames", newLiveIds, ct);

                                _logger.LogInformation($"⚽ [KICKOFF] {newLiveIds.Count} jogos viraram AO VIVO. Signal enviado para remoção.");
                            }
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