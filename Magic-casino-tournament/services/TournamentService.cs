using Magic_casino_tournament.Data;
using Magic_casino_tournament.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

namespace Magic_casino_tournament.Services
{
    public class TournamentService : ITournamentService
    {
        private readonly TournamentDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public TournamentService(TournamentDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<List<Tournament>> GetActiveTournamentsAsync()
        {
            return await _context.Tournaments
                                 .Where(t => t.IsActive && !t.IsFinished)
                                 .OrderBy(t => t.StartDate)
                                 .ToListAsync();
        }

        public async Task<Tournament?> CreateTournamentAsync(Tournament tournament)
        {
            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();
            return tournament;
        }

        public async Task<string> JoinTournamentAsync(int tournamentId, string userId)
        {
            // 1. Busca o torneio
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null) return "Torneio não encontrado.";

            // 2. Verifica se já está inscrito
            bool alreadyJoined = await _context.TournamentParticipants
                .AnyAsync(p => p.TournamentId == tournamentId && p.UserId == userId);

            if (alreadyJoined) return "Usuário já inscrito.";

            // 3. (MODO DESENVOLVEDOR) - BYPASS DE PAGAMENTO
            // Pulamos a chamada ao Core para evitar erros de rede/saldo durante o teste.
            // O código original de cobrança fica comentado abaixo para uso futuro.

            /* var client = _httpClientFactory.CreateClient();
            var paymentPayload = new
            {
                UserId = userId,
                Amount = tournament.EntryFee,
                Description = $"Inscrição Torneio: {tournament.Name}"
            };

            // URL interna do Docker (Core roda na porta 8080)
            try 
            {
                var coreResponse = await client.PostAsJsonAsync("http://core:8080/api/wallet/debit", paymentPayload);
                if (!coreResponse.IsSuccessStatusCode)
                {
                    return "Saldo insuficiente ou erro ao processar pagamento.";
                }
            }
            catch
            {
                // Ignora erro de conexão por enquanto (DEV MODE)
            }
            */

            // 4. Inscreve o jogador (Agora funciona sempre!)
            var participant = new TournamentParticipant
            {
                TournamentId = tournamentId,
                UserId = userId,
                FantasyBalance = tournament.InitialFantasyBalance, // Ganha fichas fictícias (ex: 1000)
                Rank = 0
            };

            // 5. Atualiza o prêmio acumulado (PrizePool)
            decimal houseCut = tournament.EntryFee * (tournament.HouseFeePercent / 100m);
            tournament.PrizePool += (tournament.EntryFee - houseCut);

            _context.TournamentParticipants.Add(participant);
            await _context.SaveChangesAsync();

            return "Success"; // Retorna sucesso
        }

        // NOVO MÉTODO: Realizar Aposta no Torneio
        public async Task<string> PlaceBetAsync(int tournamentId, string userId, TournamentBet bet)
        {
            // 1. Busca o Participante dentro deste torneio específico
            var participant = await _context.TournamentParticipants
                .FirstOrDefaultAsync(p => p.TournamentId == tournamentId && p.UserId == userId);

            if (participant == null)
                return "Você não está inscrito neste torneio. Faça a inscrição primeiro.";

            // 2. Validações Básicas
            if (bet.Amount <= 0) return "O valor da aposta deve ser maior que zero.";

            // 3. Verifica se tem saldo fictício (FantasyBalance) suficiente
            if (participant.FantasyBalance < bet.Amount)
                return $"Saldo insuficiente no torneio. Você tem {participant.FantasyBalance:F2} fichas.";

            // 4. Executa a Aposta (Debita a Carteira Fictícia)
            participant.FantasyBalance -= bet.Amount;

            // 5. Prepara os dados da aposta para salvar
            bet.ParticipantId = participant.Id;
            bet.PotentialWin = bet.Amount * bet.Odds; // Calcula quanto pode ganhar
            bet.Status = "Pending";
            bet.PlacedAt = DateTime.UtcNow;

            _context.TournamentBets.Add(bet);

            // Salva tudo (Aposta + Atualização de Saldo) em uma transação
            await _context.SaveChangesAsync();

            return "Success";
        }
    }
}