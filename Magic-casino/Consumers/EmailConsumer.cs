using MassTransit;
using Magic_casino.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System;

namespace Magic_casino.Consumers
{
    public class EmailConsumer : IConsumer<UserRegisteredEvent>
    {
        private readonly ILogger<EmailConsumer> _logger;
        private readonly IConfiguration _configuration;

        public EmailConsumer(ILogger<EmailConsumer> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
        {
            var user = context.Message;

            _logger.LogInformation($"📧 [SMTP] Iniciando processo de envio para: {user.Email}");

            try
            {
                var host = _configuration["SmtpSettings:Host"];
                var port = int.Parse(_configuration["SmtpSettings:Port"]);
                var emailUser = _configuration["SmtpSettings:User"];
                var emailPass = _configuration["SmtpSettings:Password"];

                // 🔹 URL DA LOGO (Esta já estava certa)
                var logoUrl = "https://quebrandoabanca.bet/logo.png";

                // 🔹 LINK DE ATIVAÇÃO CORRIGIDO (Baseado no seu Nginx)
                // O Nginx pega /api/ e joga para http://core:8080/api/
                var activationLink = $"https://quebrandoabanca.bet/api/user/activate-account?token={user.VerificationToken}";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(emailUser, "Quebrando a Banca"),
                    Subject = "Bem-vindo ao Quebrando a Banca! 🎰",
                    Body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif; margin: 0; padding: 0;'>
                            <div style='background-color: #f4f4f4; padding: 40px 20px;'>
                                <div style='background-color: #fff; padding: 30px; border-radius: 12px; max-width: 600px; margin: 0 auto; box-shadow: 0 4px 6px rgba(0,0,0,0.05);'>
                                    
                                    <div style='text-align: center; margin-bottom: 30px; border-bottom: 1px solid #eee; padding-bottom: 20px;'>
                                        <img src='{logoUrl}' alt='Quebrando a Banca' style='max-width: 180px; height: auto; display: block; margin: 0 auto;' />
                                    </div>

                                    <h1 style='color: #2c3e50; font-size: 24px; margin-top: 0; text-align: center;'>Olá, {user.Name}! 👋</h1>
                                    
                                    <p style='color: #555; font-size: 16px; line-height: 1.6; text-align: center;'>
                                        Seja muito bem-vindo ao <b>Quebrando a Banca</b>.
                                        <br>Você iniciou sua jornada como nível <b style='color: #cd7f32;'>BRONZE 🥉</b>.
                                    </p>
                                    
                                    <p style='color: #555; font-size: 16px; line-height: 1.6; text-align: center;'>
                                        Para subir para o nível <b style='color: #95a5a6;'>PRATA 🥈</b> e desbloquear benefícios exclusivos, clique no botão abaixo para confirmar seu e-mail:
                                    </p>

                                    <div style='text-align: center; margin: 30px 0;'>
                                        <a href='{activationLink}' style='background-color: #f39c12; color: #fff; padding: 15px 30px; text-decoration: none; border-radius: 50px; font-weight: bold; font-size: 16px; display: inline-block; box-shadow: 0 4px 6px rgba(243, 156, 18, 0.4);'>
                                            ATIVAR CONTA 🚀
                                        </a>
                                    </div>
                                    
                                    <div style='background-color: #e8f5e9; border-left: 4px solid #4caf50; padding: 15px; margin: 20px 0; border-radius: 4px;'>
                                        <p style='margin: 0; color: #2e7d32; font-size: 14px;'>
                                            🏆 <b>Dica Ouro:</b> Participe de 20 torneios para alcançar o nível <b>OURO</b> e ser um mestre das apostas!
                                        </p>
                                    </div>
                                    
                                    <br>
                                    <p style='color: #999; font-size: 14px; text-align: center; border-top: 1px solid #eee; padding-top: 20px;'>
                                        Atenciosamente,<br>
                                        <strong>Equipe Quebrando a Banca</strong>
                                        <br>
                                        <a href='https://quebrandoabanca.bet' style='color: #4caf50; text-decoration: none;'>acessar plataforma</a>
                                    </p>
                                </div>
                                <div style='text-align: center; margin-top: 20px; color: #aaa; font-size: 12px;'>
                                    © {DateTime.Now.Year} Quebrando a Banca. Todos os direitos reservados.
                                </div>
                            </div>
                        </body>
                        </html>
                    ",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(user.Email);

                using (var smtpClient = new SmtpClient(host, port))
                {
                    smtpClient.Credentials = new NetworkCredential(emailUser, emailPass);
                    smtpClient.EnableSsl = true;

                    await smtpClient.SendMailAsync(mailMessage);

                    _logger.LogInformation($"✅ [SMTP] E-mail enviado com sucesso para {user.Email}!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ [SMTP] Erro ao enviar e-mail para {user.Email}");
            }
        }
    }
}