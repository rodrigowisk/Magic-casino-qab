using Magic_casino_sportbook.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

public class LiveUpdateWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    // ✅ A CORREÇÃO ESTÁ AQUI: O Construtor que faltava!
    public LiveUpdateWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("🚀 [LiveUpdate] Serviço de atualização ao vivo INICIADO.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    // Obtém o serviço de dentro do escopo (Isso evita memory leaks)
                    var service = scope.ServiceProvider.GetRequiredService<LiveSportService>();

                    // Executa a sincronização
                    await service.SyncLiveFeed();
                }
            }
            catch (Exception ex)
            {
                // 🛡️ BLINDAGEM: Se der erro, não derruba o servidor inteiro!
                Console.WriteLine($"⚠️ [LiveUpdate] Erro ao sincronizar: {ex.Message}");
            }

            // Espera 10 segundos antes da próxima atualização
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}