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

        // Intervalos ajustados
        private const int DELAY_LIVE_MS = 10000; // 10s (Atualiza placar\odd)
        private const int DELAY_HOT_MS = 60000;  // 60s (Verifica novos jogos)

        public LiveUpdateWorker(IServiceProvider serviceProvider, ILogger<LiveUpdateWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔥 [LIVE WORKER] Iniciado v4.1 (Correção Contexto + Transição Suave).");
            await Task.WhenAll(ProcessLiveZone(stoppingToken), ProcessHotZone(stoppingToken));
        }

        // ==============================================================================
        // 🔄 ZONA 1: JOGOS QUE JÁ ESTÃO AO VIVO (Atualiza Odds/Placar)
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

                        // Busca jogos que já estão rolando
                        var liveGames = await context.SportsEvents
                            .Where(g => g.Status == "Live")
                            .OrderBy(g => g.LastUpdate)
                            .Take(20) // Lote seguro
                            .ToListAsync(ct);

                        if (liveGames.Any())
                        {
                            // 1. Atualiza dados na API (Odds, Placar, Tempo)
                            // Retorna IDs dos jogos que acabaram de encerrar
                            var endedGameIds = await liveService.UpdateLiveGamesAsync(liveGames, context);

                            // 2. Persiste alterações (Importante para salvar placar final ou status Ended)
                            await context.SaveChangesAsync(ct);

                            // 3. Envia o pacote leve de atualização para o Front (Só o que mudou)
                            if (liveGames.Any(g => g.Status == "Live"))
                            {
                                await hub.Clients.All.SendAsync("LiveOddsUpdate", liveGames.Where(g => g.Status == "Live").Select(g => new
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

                            // 4. Remove da tela os jogos que REALMENTE acabaram (Status virou Ended ou Cancelled)
                            if (endedGameIds != null && endedGameIds.Any())
                            {
                                Console.WriteLine($"🏁 [SIGNALR] Removendo {endedGameIds.Count} jogos encerrados da tela.");
                                await hub.Clients.All.SendAsync("RemoveGames", endedGameIds, ct);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Erro LiveLoop: {ex.Message}");
                }

                await Task.Delay(DELAY_LIVE_MS, ct);
            }
        }

        // ==============================================================================
        // 🚦 ZONA 2: JOGOS PRESTES A COMEÇAR (Transição Pré -> Live)
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
                        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();

                        // 🔍 CORREÇÃO CRÍTICA 1: .AsNoTracking()
                        // Necessário porque o LiveSportService abre um NOVO escopo/contexto internamente.
                        // Sem isso, o EF trava dizendo que o objeto já está sendo rastreado.
                        var gamesToKickoff = await context.SportsEvents
                            .AsNoTracking() // ✅ O PULO DO GATO
                            .Where(g => g.Status == "Prematch" &&
                                    g.CommenceTime <= DateTime.UtcNow.AddMinutes(5) && // Checa jogos de agora
                                    g.CommenceTime > DateTime.UtcNow.AddHours(-3))     // Janela de segurança
                            .OrderBy(g => g.CommenceTime)
                            .Take(50)
                            .ToListAsync(ct);

                        if (gamesToKickoff.Any())
                        {
                            // Verifica na API 365 se o jogo realmente começou (Status InPlay = 1)
                            // Retorna a lista de IDs que viraram "Live"
                            var newLiveIds = await liveService.VerifyKickoffWithApiAsync(gamesToKickoff, scope.ServiceProvider);

                            if (newLiveIds.Any())
                            {
                                _logger.LogInformation($"🔥 [HOTZONE] {newLiveIds.Count} jogos confirmados LIVE.");

                                // 🔍 CORREÇÃO CRÍTICA 2: Transição Suave
                                // Antes de mandar remover, vamos buscar os dados atualizados e mandar "GameWentLive"
                                // Assim o Front pode desenhar o card na aba "Ao Vivo" sem F5.

                                var freshLiveGames = await context.SportsEvents
                                    .AsNoTracking()
                                    .Where(g => newLiveIds.Contains(g.ExternalId))
                                    .ToListAsync(ct);

                                if (freshLiveGames.Any())
                                {
                                    // 1. Envia o objeto completo para o Front ADICIONAR na lista de Ao Vivo
                                    await hub.Clients.All.SendAsync("GameWentLive", freshLiveGames, ct);
                                }

                                // 2. Envia comando para REMOVER da lista de Pré-Jogo
                                await hub.Clients.All.SendAsync("RemoveGames", newLiveIds, ct);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error HotZone: {ex.Message}");
                }

                await Task.Delay(DELAY_HOT_MS, ct);
            }
        }
    }
}