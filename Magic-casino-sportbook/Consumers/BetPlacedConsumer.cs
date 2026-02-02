using MassTransit;
using Magic_casino_sportbook.Events;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Magic_casino_sportbook.Consumers
{
    // Este "cara" é quem fica ouvindo a fila do RabbitMQ.
    // Assim que chega uma mensagem "BetPlacedEvent", este método Consume é acionado.
    public class BetPlacedConsumer : IConsumer<BetPlacedEvent>
    {
        private readonly ILogger<BetPlacedConsumer> _logger;

        public BetPlacedConsumer(ILogger<BetPlacedConsumer> logger)
        {
            _logger = logger;
        }

        public Task Consume(ConsumeContext<BetPlacedEvent> context)
        {
            var aposta = context.Message;

            // Aqui é onde a mágica acontece. No futuro, você pode mandar e-mail,
            // atualizar ranking, etc. Por enquanto, vamos apenas logar.

            _logger.LogInformation("🐰 [RABBITMQ - SUCESSO] Nova Aposta Recebida na Fila!");
            _logger.LogInformation($"📝 ID: {aposta.BetId} | CPF: {aposta.UserCpf} | Valor: {aposta.Amount:C}");

            return Task.CompletedTask;
        }
    }
}