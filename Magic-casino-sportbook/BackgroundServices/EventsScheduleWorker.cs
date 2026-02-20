using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Services.Prematch;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class EventsScheduleWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public EventsScheduleWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("📅 [WORKER] Robô de Calendário Iniciado (6h).");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var service = scope.ServiceProvider.GetRequiredService<IScheduleService>();
                        await service.SyncEventsSchedule();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro no EventsScheduleWorker: {ex.Message}");
                }

                // Espera 6 horas
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }
    }
}