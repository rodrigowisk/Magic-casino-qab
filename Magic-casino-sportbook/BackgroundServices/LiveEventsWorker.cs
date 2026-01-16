using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class LiveEventsWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LiveEventsWorker> _logger;

        public LiveEventsWorker(IServiceProvider serviceProvider, ILogger<LiveEventsWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Live Events Worker INICIADO.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var provider = Environment.GetEnvironmentVariable("ODDS_PROVIDER");

                        if (provider == "BetsApi")
                        {
                            var service = scope.ServiceProvider.GetRequiredService<BetsApiService>();
                            await service.SyncLiveFeed();
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro no ciclo do LiveEventsWorker");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }
    }
}