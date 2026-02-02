using MassTransit;
using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Events;
using Magic_casino_sportbook.Services;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_sportbook.Consumers
{
    public class BetWonConsumer : IConsumer<BetWonEvent>
    {
        private readonly ILogger<BetWonConsumer> _logger;
        private readonly CoreWalletService _walletService;
        private readonly IServiceProvider _serviceProvider;

        public BetWonConsumer(
            ILogger<BetWonConsumer> logger,
            CoreWalletService walletService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _walletService = walletService;
            _serviceProvider = serviceProvider;
        }

        public async Task Consume(ConsumeContext<BetWonEvent> context)
        {
            var msg = context.Message;
            _logger.LogInformation($"🏆 [RABBITMQ] Processando pagamento da Aposta ID: {msg.BetId} | Valor: {msg.PayoutAmount:C}");

            // 1. Chama a API do Core para creditar o dinheiro
            var result = await _walletService.CreditFundsAsync(msg.UserCpf, msg.PayoutAmount);

            if (result.Success)
            {
                // 2. Se deu certo, salva o histórico no banco de dados
                // Precisamos criar um escopo novo porque Consumidores são Singleton/Scoped
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    try
                    {
                        var sql = "INSERT INTO \"transactions\" (\"user_cpf\", \"amount\", \"external_reference\", \"status\", \"created_at\", \"paid_at\", \"description\", \"source\", \"type\") VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})";

                        await dbContext.Database.ExecuteSqlRawAsync(sql,
                            msg.UserCpf,
                            msg.PayoutAmount,
                            msg.BetId,
                            "COMPLETED",
                            DateTime.UtcNow,
                            DateTime.UtcNow,
                            "Prêmio Aposta Esportiva",
                            "Sportbook",
                            "prize"
                        );

                        _logger.LogInformation($"✅ [PAGAMENTO SUCESSO] Saldo creditado e histórico salvo para CPF {msg.UserCpf}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"⚠️ [ERRO HISTÓRICO] Dinheiro foi pago, mas falhou ao salvar log: {ex.Message}");
                    }
                }
            }
            else
            {
                _logger.LogError($"❌ [FALHA PAGAMENTO] Core recusou crédito: {result.Message}");
                // Opcional: Lançar exceção aqui faria o RabbitMQ tentar de novo (Retry Policy)
                // throw new Exception("Erro ao processar pagamento. Tentando novamente...");
            }
        }
    }
}