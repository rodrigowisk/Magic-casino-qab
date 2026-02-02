using MassTransit;
using Magic_casino.Events;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Magic_casino.Consumers
{
    public class EmailConsumer : IConsumer<UserRegisteredEvent>
    {
        private readonly ILogger<EmailConsumer> _logger;

        public EmailConsumer(ILogger<EmailConsumer> logger)
        {
            _logger = logger;
        }

        public Task Consume(ConsumeContext<UserRegisteredEvent> context)
        {
            var user = context.Message;

            // =================================================================
            // 🎨 AQUI É ONDE VOCÊ CONFIGURA A MENSAGEM DE BOAS-VINDAS
            // =================================================================
            string assunto = "Bem-vindo ao Magic Casino!";

            string corpoEmail = $@"
                <html>
                <body>
                    <h1>Olá, {user.Name}! 👋</h1>
                    <p>Seja muito bem-vindo ao <b>Magic Casino</b>.</p>
                    <p>Seu cadastro foi realizado com sucesso em {user.RegisteredAt:dd/MM/yyyy}.</p>
                    <br>
                    <p>Dica: Faça seu primeiro depósito via PIX e aproveite nossos bônus!</p>
                    <br>
                    <p>Atenciosamente,<br>Equipe Magic Casino</p>
                </body>
                </html>
            ";

            // =================================================================
            // 🚀 SIMULAÇÃO DO ENVIO (MOCK)
            // =================================================================
            // Num sistema real, aqui você chamaria: _emailService.Send(user.Email, assunto, corpoEmail);

            _logger.LogInformation("📧 [EMAIL SERVICE - MOCK] =================================");
            _logger.LogInformation($"PARA: {user.Email}");
            _logger.LogInformation($"ASSUNTO: {assunto}");
            _logger.LogInformation("CONTEÚDO:");
            _logger.LogInformation(corpoEmail); // Vai imprimir o HTML no terminal
            _logger.LogInformation("📧 [EMAIL SERVICE - MOCK] =================================");

            return Task.CompletedTask;
        }
    }
}