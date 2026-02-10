using MassTransit;
using Magic_casino.Contracts; // ✅ Usando o namespace padronizado
using Magic_casino_tournament.Services;

namespace Magic_casino_tournament.Consumers
{
    public class GameEndedConsumer : IConsumer<GameEndedEvent>
    {
        private readonly ILogger<GameEndedConsumer> _logger;
        private readonly ITournamentService _tournamentService;

        public GameEndedConsumer(ILogger<GameEndedConsumer> logger, ITournamentService tournamentService)
        {
            _logger = logger;
            _tournamentService = tournamentService;
        }

        public async Task Consume(ConsumeContext<GameEndedEvent> context)
        {
            var message = context.Message;
            _logger.LogInformation($"⚡ [RABBITMQ] Jogo Encerrado recebido: {message.HomeTeam} vs {message.AwayTeam} (Placar: {message.Score})");

            try
            {
                await _tournamentService.ProcessGameResultAsync(message.GameId, message.Score);
                _logger.LogInformation($"✅ [RABBITMQ] Pontuação calculada para o jogo {message.GameId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ [ERRO] Falha ao processar resultado: {ex.Message}");
            }
        }
    }
}