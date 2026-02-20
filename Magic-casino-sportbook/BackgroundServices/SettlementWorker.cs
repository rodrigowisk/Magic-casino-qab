using Magic_casino_sportbook.Services;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class SettlementWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SettlementWorker> _logger;

        public SettlementWorker(IServiceProvider serviceProvider, ILogger<SettlementWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Aguarda 15s para não competir com o boot inicial dos outros serviços
            await Task.Delay(15000, stoppingToken);

            _logger.LogInformation("⚖️ [SETTLEMENT] Worker de Apuração Iniciado (Independente).");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var service = scope.ServiceProvider.GetRequiredService<BetSettlementService>();
                        await service.UpdatePendingBetsAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Erro na Apuração: {ex.Message}");
                }

                // Roda a cada 30 segundos
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}