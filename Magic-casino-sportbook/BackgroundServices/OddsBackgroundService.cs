using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Services.Prematch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class OddsBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OddsBackgroundService> _logger;

        public OddsBackgroundService(IServiceProvider serviceProvider, ILogger<OddsBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🤖 Servidor de Odds (SYNC BASE) iniciado.");

            // Roda ao iniciar
            await RunSync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
                catch (TaskCanceledException) { break; }

                await RunSync(stoppingToken);
            }
        }

        private async Task RunSync(CancellationToken stoppingToken)
        {
            if (stoppingToken.IsCancellationRequested) return;

            try
            {
                using var scope = _serviceProvider.CreateScope();

                // ✅ Versão Limpa: Apenas BetsAPI
                _logger.LogInformation("🔄 Iniciando Sync com BETS API (Via ScheduleService)...");

                var service = scope.ServiceProvider.GetRequiredService<IScheduleService>();
                await service.SyncEventsSchedule();

                _logger.LogInformation("✅ Sincronização concluída.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro crítico na sincronização BASE");
            }
        }
    }
}