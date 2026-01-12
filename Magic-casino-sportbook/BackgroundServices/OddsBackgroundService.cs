using Magic_casino_sportbook.Services;
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
            _logger.LogInformation("Servidor de Odds Global iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var oddsService = scope.ServiceProvider.GetRequiredService<OddsService>();
                        _logger.LogInformation("Iniciando sincronização de TODOS os esportes ativos...");

                        // Chama o novo método que varre toda a API
                        await oddsService.SyncAllSportsToDatabase();

                        _logger.LogInformation("Sincronização global concluída com sucesso no banco AWS!");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Erro na sincronização global: {ex.Message}");
                }

                // Espera 10 minutos antes de atualizar tudo de novo
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}