using Magic_casino_sportbook.Services;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class PrematchOddsWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public PrematchOddsWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("💰 [WORKER] Robô de Odds Prematch Iniciado (20min).");

            // Espera 1 minuto ao ligar para não travar o boot com muitas requisições
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var service = scope.ServiceProvider.GetRequiredService<IOddsService>();
                        await service.SyncPrematchOdds();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro no PrematchOddsWorker: {ex.Message}");
                }

                // Espera 20 minutos
                await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
            }
        }
    }
}