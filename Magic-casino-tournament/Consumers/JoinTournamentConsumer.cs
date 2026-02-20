using Magic_casino_tournament.Data;
using Magic_casino_tournament.Events;
using Magic_casino_tournament.Hubs;
using Magic_casino_tournament.Models;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RedLockNet; // 👈 Necessário

namespace Magic_casino_tournament.Consumers
{
    public class JoinTournamentConsumer : IConsumer<JoinTournamentCommand>
    {
        private readonly TournamentDbContext _context;
        private readonly IHubContext<TournamentHub> _hubContext;
        private readonly ILogger<JoinTournamentConsumer> _logger;
        private readonly IDistributedLockFactory _lockFactory; // 👈 Injeção do Lock

        public JoinTournamentConsumer(
            TournamentDbContext context,
            IHubContext<TournamentHub> hubContext,
            ILogger<JoinTournamentConsumer> logger,
            IDistributedLockFactory lockFactory) // 👈 Recebe a factory
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
            _lockFactory = lockFactory;
        }

        public async Task Consume(ConsumeContext<JoinTournamentCommand> context)
        {
            var msg = context.Message;

            // 🔒 Chave única para travar APENAS este torneio específico
            var lockKey = $"lock:tournament:join:{msg.TournamentId}";

            // Tenta pegar a vez: Espera até 10s na fila, e solta o lock em 5s se travar
            using (var redLock = await _lockFactory.CreateLockAsync(lockKey, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200)))
            {
                if (redLock.IsAcquired)
                {
                    // ✅ ENTROU NA FILA: Só um por vez executa esse bloco
                    try
                    {
                        // 1. Busca o torneio no banco (Leitura segura dentro do lock)
                        var tournament = await _context.Tournaments
                            .Include(t => t.Participants)
                            .FirstOrDefaultAsync(t => t.Id == msg.TournamentId);

                        if (tournament == null)
                        {
                            await NotifyUser(msg.UserId, "Error", "Torneio não encontrado.");
                            return;
                        }

                        // 2. VERIFICAÇÃO DE SEGURANÇA (Agora 100% segura por causa do Lock)
                        if (tournament.ParticipantsCount >= tournament.MaxParticipants)
                        {
                            await NotifyUser(msg.UserId, "Error", $"O torneio lotou! Limite: {tournament.MaxParticipants}");
                            return;
                        }

                        // 3. Verifica se já está dentro
                        if (tournament.Participants.Any(p => p.UserId == msg.UserId))
                        {
                            await NotifyUser(msg.UserId, "Success", "Você já está inscrito.");
                            return;
                        }

                        // 4. Insere o Participante (Sua lógica original mantida)
                        var participant = new TournamentParticipant
                        {
                            TournamentId = msg.TournamentId,
                            UserId = msg.UserId,
                            UserName = msg.UserName,
                            Avatar = msg.Avatar,
                            JoinedAt = DateTime.UtcNow,
                            FantasyBalance = tournament.InitialFantasyBalance
                        };

                        _context.TournamentParticipants.Add(participant);

                        // Atualiza contador e prêmio (Sua lógica de pagamento original)
                        tournament.ParticipantsCount++;

                        if (tournament.EntryFee > 0 && (tournament.FixedPrize == null || tournament.FixedPrize == 0))
                        {
                            var feePercent = tournament.HouseFeePercent > 0 ? tournament.HouseFeePercent : 10;
                            var houseCut = tournament.EntryFee * (feePercent / 100);
                            tournament.PrizePool += (tournament.EntryFee - houseCut);
                        }

                        await _context.SaveChangesAsync();

                        // 5. Notifica SUCESSO
                        await NotifyUser(msg.UserId, "Success", "Inscrição realizada com sucesso!");

                        // 6. Atualiza o card para TODOS no Lobby
                        await _hubContext.Clients.All.SendAsync("UpdateParticipantCount", tournament.Id, tournament.ParticipantsCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Erro ao processar inscrição: {ex.Message}");
                        await NotifyUser(msg.UserId, "Error", "Erro interno ao processar inscrição.");
                    }
                }
                else
                {
                    // ❌ Fila muito cheia ou timeout
                    _logger.LogWarning($"Lock timeout para user {msg.UserId} no torneio {msg.TournamentId}");
                    await NotifyUser(msg.UserId, "Error", "Muitas tentativas simultâneas. Tente novamente.");
                }
            }
        }

        private async Task NotifyUser(string userId, string status, string message)
        {
            await _hubContext.Clients.Group(userId).SendAsync("JoinResult", new
            {
                Status = status,
                Message = message,
                IsSuccess = status == "Success"
            });
        }
    }
}