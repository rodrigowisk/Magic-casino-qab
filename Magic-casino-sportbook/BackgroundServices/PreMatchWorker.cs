using Magic_casino_sportbook.Services;

public class PreMatchWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public PreMatchWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 🛡️ SEGURANÇA: Espera 15 segundos antes de iniciar para não travar o boot da API
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        Console.WriteLine("🚀 [PreMatchWorker] Robô de Ingestão Iniciado (30min).");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    // Tenta obter o serviço. Se falhar, loga e continua (não derruba o app)
                    var service = scope.ServiceProvider.GetService<PreMatchService>();

                    if (service != null)
                    {
                        await service.SyncUpcomingGames();
                    }
                    else
                    {
                        Console.WriteLine("⚠️ [PreMatchWorker] Serviço PreMatchService não encontrado/registrado.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro no PreMatchWorker: {ex.Message}");
            }

            // Roda a cada 30 minutos
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }
}