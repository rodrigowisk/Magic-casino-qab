using Magic_casino_sportbook.Services;

public class PreMatchWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<PreMatchService>();
                await service.SyncUpcomingGames();
            }
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }
}