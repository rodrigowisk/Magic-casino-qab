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

        // Intervalos ajustados para economia e performance
        private const int DELAY_LIVE_MS = 10000; // 10s (Atualiza placar\odd)
        private const int DELAY_HOT_MS = 60000;  // 60s (Verifica novos jogos)

        public LiveUpdateWorker(IServiceProvider serviceProvider, ILogger<LiveUpdateWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔥 [LIVE WORKER] Iniciado v4.0 (Multiesporte - API Check).");
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

                        // 🔍 ALTERAÇÃO 1: Removido filtro de "Soccer". Agora pega TUDO.
                        // O "LiveSportService" já sabe lidar com cada esporte.
                        var liveGames = await context.SportsEvents
                            .Where(g => g.Status == "Live")
                            .OrderBy(g => g.LastUpdate) // Garante rotação
                            .Take(15) // Aumentei levemente o lote para garantir cobertura
                            .ToListAsync(ct);

                        if (liveGames.Any())
                        {
                            // 1. Vai na API, atualiza placar e identifica quem ACABOU
                            // O método retorna a lista de IDs que viraram "Ended"
                            // Nota: Se você atualizou o UpdateLiveGamesAsync para pedir IServiceProvider, altere aqui.
                            // Mantive 'context' conforme seu código original para compatibilidade, 
                            // mas o ideal é usar o ServiceProvider para criar escopos isolados no serviço.
                            var endedGameIds = await liveService.UpdateLiveGamesAsync(liveGames, context);

                            // 2. Salva as alterações (Placar novo ou Status Ended) no banco
                            await context.SaveChangesAsync(ct);

                            // 3. Envia atualização de Odds/Placar para quem está na página
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

                            // 4. 🔥 O GRANDE FINAL: Remove da tela os jogos encerrados
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

                        // 🔍 ALTERAÇÃO 2: Busca candidatos a Live
                        // Adicionei .AddMinutes(5) para pegar jogos prestes a começar, permitindo
                        // que a API valide se o jogo começou na hora ou um pouco antes.
                        var gamesToKickoff = await context.SportsEvents
                            .Where(g => g.Status == "Prematch" &&
                                    g.CommenceTime <= DateTime.UtcNow.AddMinutes(5) &&
                                    g.CommenceTime > DateTime.UtcNow.AddHours(-3)) // Janela de segurança
                            .OrderBy(g => g.CommenceTime)
                            .Take(50)
                            .ToListAsync(ct);

                        if (gamesToKickoff.Any())
                        {
                            // AQUI ESTÁ A CORREÇÃO SOLICITADA:
                            // Não muda mais o status cegamente (CheckForKickoffAsync).
                            // Consulta a API (VerifyKickoffWithApiAsync) para ver se está InPlay (1).
                            // Passamos o scope.ServiceProvider para o serviço criar seu próprio cliente isolado.

                            var newLiveIds = await liveService.VerifyKickoffWithApiAsync(gamesToKickoff, scope.ServiceProvider);

                            if (newLiveIds.Any())
                            {
                                _logger.LogInformation($"🔥 [HOTZONE] {newLiveIds.Count} jogos confirmados LIVE pela API.");
                                // Remove da lista "Pré-Jogo" do front (para o usuário dar F5 ou navegar pro Live)
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