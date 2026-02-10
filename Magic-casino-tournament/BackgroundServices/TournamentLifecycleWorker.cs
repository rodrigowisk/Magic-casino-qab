using Magic_casino_tournament.Services;

namespace Magic_casino_tournament.BackgroundServices
{
    public class TournamentLifecycleWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public TournamentLifecycleWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Espera 30 segundos no boot para não sobrecarregar
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            Console.WriteLine("🏆 [WORKER] Robô de Encerramento de Torneios Iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var service = scope.ServiceProvider.GetRequiredService<ITournamentService>();

                        // Verifica se tem torneio vencido e paga
                        await service.ProcessFinishedTournamentsAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [ERRO WORKER] Falha ao processar torneios: {ex.Message}");
                }

                // Verifica a cada 2 minutos
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }
    }
}