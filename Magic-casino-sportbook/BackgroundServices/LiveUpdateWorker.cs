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

        // Configurações de Frequência (Profissional)
        private const int DELAY_LIVE_MS = 3000;   // 3 segundos (Atualização em tempo real)
        private const int DELAY_HOT_MS = 15000;   // 15 segundos (Zona Quente - Aumentei a frequência para pegar kickoff rápido)

        public LiveUpdateWorker(IServiceProvider serviceProvider, ILogger<LiveUpdateWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔥 [LIVE WORKER] Iniciado. Gerenciando Zonas Quente e Ao Vivo.");

            // Rodamos tarefas paralelas para não travar o loop
            var liveTask = ProcessLiveZone(stoppingToken);
            var hotTask = ProcessHotZone(stoppingToken);

            await Task.WhenAll(liveTask, hotTask);
        }

        // ==============================================================================
        // ⚡ ZONA 1: JOGOS AO VIVO (Atualização Rápida de Placar/Odds)
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

                        // 1. Busca jogos que ESTÃO marcados como LIVE no banco
                        // Ordena pelo LastUpdate para garantir que todos sejam atualizados ciclicamente
                        var liveGames = await context.SportsEvents
                            .Where(g => g.Status == "Live")
                            .OrderBy(g => g.LastUpdate)
                            .Take(50) // Pega em lotes maiores
                            .ToListAsync(ct);

                        if (liveGames.Any())
                        {
                            // Atualiza placar, tempo e odds na API
                            var endedGameIds = await liveService.UpdateLiveGamesAsync(liveGames);

                            // Salva as alterações no banco (importante para persistir o Placar novo!)
                            await context.SaveChangesAsync(ct);

                            // Notifica o Frontend via WebSocket (SignalR) com dados fresquinhos
                            if (liveGames.Any())
                            {
                                await hub.Clients.All.SendAsync("LiveOddsUpdate", liveGames.Select(g => new
                                {
                                    id = g.ExternalId,
                                    score = g.Score,      // Agora com o Score corrigido do Basquete!
                                    time = g.GameTime,
                                    status = g.Status,
                                    homeOdd = g.RawOddsHome,
                                    drawOdd = g.RawOddsDraw,
                                    awayOdd = g.RawOddsAway
                                }), ct);
                            }

                            // 🚨 LIMPEZA IMEDIATA: Se o jogo acabou, avisa o front para remover
                            if (endedGameIds != null && endedGameIds.Any())
                            {
                                await hub.Clients.All.SendAsync("RemoveGames", endedGameIds, ct);
                                _logger.LogInformation($"🏁 [FIM DE JOGO] {endedGameIds.Count} jogos finalizados e removidos da tela.");
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
        // 🔥 ZONA 2: ZONA QUENTE (Detecta Início de Jogo e Remove do Pré-Jogo)
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

                        // BUSCA INTELIGENTE (CORRIGIDA):
                        // Jogos 'Prematch' que:
                        // 1. Vão começar nos próximos 60 min (CommenceTime <= Now + 60)
                        // 2. OU JÁ DEVERIAM TER COMEÇADO há até 2 horas (CommenceTime >= Now - 120)
                        // Isso pega o caso do Criciúma que ficou "atrasado"

                        var now = DateTime.UtcNow;
                        var hotGames = await context.SportsEvents
                            .Where(g => g.Status == "Prematch" &&
                                        g.CommenceTime >= now.AddMinutes(-120) && // Olha 2h para trás (Segurança)
                                        g.CommenceTime <= now.AddMinutes(60))     // Olha 1h para frente
                            .ToListAsync(ct);

                        if (hotGames.Any())
                        {
                            // Verifica na API se eles já viraram LIVE
                            // Se sim, o serviço muda o status no banco E MANDA O SIGNALR "RemoveFromPrematch"
                            await liveService.CheckForKickoffAsync(hotGames);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Erro no Loop Hot Zone: {ex.Message}");
                }

                await Task.Delay(DELAY_HOT_MS, ct);
            }
        }
    }
}