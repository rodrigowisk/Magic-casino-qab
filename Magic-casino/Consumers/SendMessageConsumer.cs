using MassTransit;
using Microsoft.EntityFrameworkCore;
using Magic_casino.Data;
using Magic_casino.Models;
using Magic_casino.Events;

namespace Magic_casino.Consumers
{
    public class SendMessageConsumer : IConsumer<SendMessageEvent>
    {
        private readonly AppDbContext _context;

        public SendMessageConsumer(AppDbContext context)
        {
            _context = context;
        }

        public async Task Consume(ConsumeContext<SendMessageEvent> context)
        {
            var data = context.Message;

            // 1. Criar a mensagem principal
            var message = new Message
            {
                Subject = data.Subject,
                Body = data.Body,
                TargetType = data.Type,
                TargetValue = data.Value
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync(); // Salva para gerar o MessageId

            // 2. Identificar os destinatários
            IQueryable<string> targetUserIdsQuery = _context.Users.Select(u => u.Cpf);

            switch (data.Type.ToLower())
            {
                case "level":
                    if (!string.IsNullOrEmpty(data.Value))
                    {
                        targetUserIdsQuery = _context.Users
                            .Where(u => u.Level == data.Value)
                            .Select(u => u.Cpf);
                    }
                    break;

                case "user":
                    if (!string.IsNullOrEmpty(data.Value))
                    {
                        targetUserIdsQuery = _context.Users
                            .Where(u => u.Cpf == data.Value)
                            .Select(u => u.Cpf);
                    }
                    break;
            }

            var userCpfs = await targetUserIdsQuery.ToListAsync();

            // 3. Criar a relação (Recipients) em lote
            var recipients = userCpfs.Select(userCpf => new MessageRecipient
            {
                MessageId = message.Id,
                UserId = userCpf,
                IsRead = false
            });

            _context.MessageRecipients.AddRange(recipients);
            await _context.SaveChangesAsync();

            // Log opcional para debug
            Console.WriteLine($">>>>> [RABBITMQ] Mensagem '{data.Subject}' enviada para {userCpfs.Count} usuários.");
        }
    }
}