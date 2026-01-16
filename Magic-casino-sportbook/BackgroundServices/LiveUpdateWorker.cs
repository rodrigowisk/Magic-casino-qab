using Magic_casino_sportbook.Services;

public class LiveUpdateWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<LiveSportService>();
                await service.SyncLiveFeed();
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}