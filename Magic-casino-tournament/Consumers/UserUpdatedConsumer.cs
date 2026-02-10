using MassTransit;
using Magic_casino_tournament.Data;
using Microsoft.EntityFrameworkCore;
// 👇 1. Adicionado para manipular o Redis
using Microsoft.Extensions.Caching.Distributed;
// 👇 2. Adicionado para "casar" com a mensagem do Core
using Magic_casino.Events;

namespace Magic_casino_tournament.Consumers
{
    public class UserUpdatedConsumer : IConsumer<UserUpdatedEvent>
    {
        private readonly TournamentDbContext _context;
        // 👇 3. Injeção do Cache
        private readonly IDistributedCache _cache;

        public UserUpdatedConsumer(TournamentDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task Consume(ConsumeContext<UserUpdatedEvent> context)
        {
            var data = context.Message;
            Console.WriteLine($"🔄 [SYNC] Recebi atualização: {data.Name} ({data.UserId})");

            // 1. Busca TODAS as inscrições desse usuário em qualquer torneio
            var participants = await _context.TournamentParticipants
                .Where(p => p.UserId == data.UserId)
                .ToListAsync();

            if (participants.Any())
            {
                // 2. Atualiza o nome e a foto em todas elas
                foreach (var p in participants)
                {
                    p.UserName = data.Name;
                    p.Avatar = data.Avatar;

                    // 👇 4. IMPORTANTE: Remove o cache do ranking desse torneio
                    // Assim, quando o usuário der F5, a foto nova já aparece na hora!
                    await _cache.RemoveAsync($"ranking:{p.TournamentId}");
                }

                // 3. Salva no banco
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ [SYNC] Perfil atualizado em {participants.Count} torneios e cache limpo.");
            }
        }
    }
}