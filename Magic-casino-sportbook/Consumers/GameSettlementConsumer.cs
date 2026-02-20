using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Domain.Enums;
using Magic_casino_sportbook.Events;
using Magic_casino_sportbook.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_sportbook.Consumers
{
    // Este consumidor é o "JUIZ". Ele é acionado assim que um jogo acaba.
    public class GameSettlementConsumer : IConsumer<GameEndedEvent>
    {
        private readonly ILogger<GameSettlementConsumer> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPublishEndpoint _publishEndpoint;

        public GameSettlementConsumer(
            ILogger<GameSettlementConsumer> logger,
            IServiceProvider serviceProvider,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<GameEndedEvent> context)
        {
            var msg = context.Message;
            _logger.LogInformation($"⚖️ [APURAÇÃO] Iniciando conferência para o jogo: {msg.HomeTeam} x {msg.AwayTeam} (Placar: {msg.Score})");

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // 1. Busca todas as seleções PENDENTES desse jogo específico
                var pendingSelections = await dbContext.BetSelections
                    .Include(s => s.Bet)
                    .Where(s => s.MatchId == msg.GameId && s.Status == "pending")
                    .ToListAsync();

                if (!pendingSelections.Any())
                {
                    _logger.LogInformation("✅ Nenhuma aposta pendente para este jogo.");
                    return;
                }

                _logger.LogInformation($"🔍 Conferindo {pendingSelections.Count} seleções pendentes...");

                foreach (var selection in pendingSelections)
                {
                    // Lógica de conferência (Centralizada ou Helper)
                    // Aqui estou simplificando, mas idealmente use a BettingRulesEngine que sugeri antes
                    bool won = CheckWinner(selection.MarketName, selection.OutcomeName, msg.Score);

                    selection.Status = won ? "Won" : "Lost";
                    selection.FinalScore = msg.Score;
                    selection.IsWinner = won;

                    // Se a aposta (ticket pai) estiver pronta para ser paga...
                    if (selection.Bet != null)
                    {
                        await CheckTicketStatusAsync(selection.Bet, dbContext);
                    }
                }

                await dbContext.SaveChangesAsync();
            }
        }

        private async Task CheckTicketStatusAsync(Bet ticket, AppDbContext dbContext)
        {
            // Verifica se TODAS as seleções do bilhete já foram resolvidas
            // Atenção: Isso aqui precisa buscar no banco para garantir que outras seleções de outros jogos já finalizaram
            var allSelections = await dbContext.BetSelections
                .Where(s => s.BetId == ticket.Id)
                .ToListAsync();

            if (allSelections.Any(s => s.Status == "pending")) return; // Ainda tem jogo rolando no bilhete

            if (allSelections.Any(s => s.Status == "Lost"))
            {
                if (ticket.Status != "Lost")
                {
                    ticket.Status = "Lost";
                    ticket.SettledAt = DateTime.UtcNow;
                }
            }
            else if (allSelections.All(s => s.Status == "Won"))
            {
                if (ticket.Status != "Won")
                {
                    ticket.Status = "Won";
                    ticket.SettledAt = DateTime.UtcNow;

                    // 🚀 AQUI É O PULO DO GATO!
                    // Ao invés de pagar aqui, ele avisa o seu BetWonConsumer
                    await _publishEndpoint.Publish(new BetWonEvent
                    {
                        BetId = ticket.Id,
                        UserCpf = ticket.UserCpf,
                        PayoutAmount = ticket.PotentialReturn,
                        WonAt = DateTime.UtcNow
                    });

                    _logger.LogInformation($"🚀 Ticket {ticket.Id} venceu! Evento de pagamento enviado.");
                }
            }
        }

        // Mini-cópia da lógica (Idealmente mova para uma classe estática BettingRules)
        private bool CheckWinner(string market, string outcome, string score)
        {
            // ... (Mesma lógica do seu LiveScoreWorker) ...
            // Para economizar espaço, assuma que a lógica de parse de string está aqui
            return false; // Placeholder
        }
    }
}