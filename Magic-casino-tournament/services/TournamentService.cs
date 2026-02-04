using Magic_casino_tournament.Data;
using Magic_casino_tournament.Models;
using Magic_casino_tournament.Controllers;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_tournament.Services
{
    public class TournamentService : ITournamentService
    {
        private readonly TournamentDbContext _context;
        private readonly ICoreGateway _coreGateway;

        public TournamentService(TournamentDbContext context, ICoreGateway coreGateway)
        {
            _context = context;
            _coreGateway = coreGateway;
        }

        public async Task<List<Tournament>> GetActiveTournamentsAsync(string? userId)
        {
            var tournaments = await _context.Tournaments
                .Where(t => t.IsActive && !t.IsFinished)
                .OrderBy(t => t.StartDate)
                .ToListAsync();

            if (!string.IsNullOrEmpty(userId))
            {
                foreach (var t in tournaments)
                {
                    t.ParticipantsCount = await _context.TournamentParticipants.CountAsync(p => p.TournamentId == t.Id);
                    t.IsJoined = await _context.TournamentParticipants.AnyAsync(p => p.TournamentId == t.Id && p.UserId == userId);
                }
            }
            return tournaments;
        }

        public async Task<object?> GetTournamentByIdAsync(int id, string? userId)
        {
            var tournament = await _context.Tournaments.FindAsync(id);
            if (tournament == null) return null;

            tournament.ParticipantsCount = await _context.TournamentParticipants.CountAsync(p => p.TournamentId == id);

            bool isJoined = false;
            decimal currentBalance = tournament.InitialFantasyBalance;
            int rank = 0;

            if (!string.IsNullOrEmpty(userId))
            {
                var participant = await _context.TournamentParticipants
                    .FirstOrDefaultAsync(p => p.TournamentId == id && p.UserId == userId);

                if (participant != null)
                {
                    isJoined = true;
                    currentBalance = participant.FantasyBalance;
                    rank = participant.Rank;
                }
            }

            return new
            {
                tournament.Id,
                tournament.Name,
                tournament.Description,
                tournament.Sport,
                tournament.EntryFee,
                tournament.PrizePool,
                tournament.StartDate,
                tournament.EndDate,
                tournament.IsActive,
                tournament.IsFinished,
                tournament.ParticipantsCount,
                tournament.HouseFeePercent,
                tournament.InitialFantasyBalance,
                tournament.FilterRules,
                IsJoined = isJoined,
                CurrentFantasyBalance = currentBalance,
                Rank = rank
            };
        }

        public async Task<Tournament> CreateTournamentAsync(Tournament tournament)
        {
            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();
            return tournament;
        }

        public async Task<string> JoinTournamentAsync(int tournamentId, string userId, string token)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null) return "Torneio não encontrado.";

            if (await _context.TournamentParticipants.AnyAsync(p => p.TournamentId == tournamentId && p.UserId == userId))
                return "Usuário já inscrito.";

            if (tournament.EntryFee > 0)
            {
                var response = await _coreGateway.DeductFundsAsync(userId, tournament.EntryFee, token);
                if (!response.Success) return response.Message ?? "Saldo insuficiente.";

                var transaction = new TournamentTransaction
                {
                    TournamentId = tournamentId,
                    UserId = userId,
                    Amount = tournament.EntryFee,
                    Type = "EntryFee",
                    Status = "Completed",
                    CoreTransactionId = response.TransactionId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.TournamentTransactions.Add(transaction);
            }

            var participant = new TournamentParticipant
            {
                TournamentId = tournamentId,
                UserId = userId,
                FantasyBalance = tournament.InitialFantasyBalance,
                Rank = 0
            };
            _context.TournamentParticipants.Add(participant);

            if (tournament.EntryFee > 0)
            {
                decimal houseCut = tournament.EntryFee * (tournament.HouseFeePercent / 100m);
                tournament.PrizePool += (tournament.EntryFee - houseCut);
            }

            await _context.SaveChangesAsync();
            return "Success";
        }

        // Método legado adaptado (cria um bilhete de 1 seleção)
        public async Task<string> PlaceBetAsync(int tournamentId, string userId, TournamentBet bet)
        {
            // Este método é apenas para compatibilidade, o ideal é usar o Batch
            // Como o objeto 'bet' antigo não casa com a nova estrutura, vamos apenas retornar erro
            // ou redirecionar se possível. Por segurança, retornamos erro pedindo update.
            return "Use o método PlaceBatchBetsAsync para suportar a nova estrutura.";
        }

        // ✅ LÓGICA CORRIGIDA: CRIA 1 APOSTA COM N SELEÇÕES
        public async Task<(bool Success, string Message)> PlaceBatchBetsAsync(int tournamentId, string userId, PlaceTournamentBetRequest request)
        {
            var participant = await _context.TournamentParticipants
                .FirstOrDefaultAsync(p => p.TournamentId == tournamentId && p.UserId == userId);

            if (participant == null)
                return (false, "Participante não encontrado ou não inscrito.");

            // ✅ CORREÇÃO 1: Custo é o valor do bilhete único, NÃO multiplica pela quantidade
            decimal totalCost = request.Amount;

            if (participant.FantasyBalance < totalCost)
                return (false, "Saldo de fichas insuficiente.");

            // 1. Deduz Saldo
            participant.FantasyBalance -= totalCost;

            // 2. Calcula Odd Total (Multiplicação)
            decimal totalOdds = request.Selections.Any()
                ? request.Selections.Aggregate(1m, (acc, sel) => acc * sel.Odds)
                : 0;

            // 3. Cria O BILHETE (Header)
            var bet = new TournamentBet
            {
                TournamentId = tournamentId,
                UserId = userId,
                ParticipantId = participant.Id,
                Amount = totalCost,
                TotalOdds = totalOdds,
                PotentialWin = totalCost * totalOdds,
                Status = "Pending",
                PlacedAt = DateTime.UtcNow,
                Selections = new List<Models.TournamentBetSelection>()
            };

            // 4. Adiciona as SELEÇÕES (Items)
            foreach (var sel in request.Selections)
            {
                bet.Selections.Add(new Models.TournamentBetSelection
                {
                    GameId = sel.GameId,
                    SportKey = "soccer",
                    HomeTeam = sel.HomeTeam,
                    AwayTeam = sel.AwayTeam,
                    SelectionName = sel.SelectionName,
                    MarketName = sel.MarketName,
                    Odds = sel.Odds,
                    Status = "Pending"
                });
            }

            _context.TournamentBets.Add(bet);
            await _context.SaveChangesAsync();

            return (true, "Aposta múltipla realizada com sucesso!");
        }

        public async Task<List<TournamentParticipant>> GetTournamentRankingAsync(int tournamentId)
        {
            return await _context.TournamentParticipants
                .Where(p => p.TournamentId == tournamentId)
                .OrderByDescending(p => p.FantasyBalance)
                .ToListAsync();
        }

        public async Task<decimal> GetUserFantasyBalance(int tournamentId, string userId)
        {
            var p = await _context.TournamentParticipants
                .FirstOrDefaultAsync(p => p.TournamentId == tournamentId && p.UserId == userId);
            return p?.FantasyBalance ?? 0;
        }
        public async Task<List<TournamentBet>> GetUserBetsAsync(int tournamentId, string userId)
        {
            return await _context.TournamentBets
                .Include(b => b.Selections) // Traz os jogos junto
                .Where(b => b.TournamentId == tournamentId && b.UserId == userId)
                .OrderByDescending(b => b.PlacedAt)
                .ToListAsync();
        }

        public async Task DeductFantasyBalance(int tournamentId, string userId, decimal amount)
        {
            var p = await _context.TournamentParticipants
                .FirstOrDefaultAsync(p => p.TournamentId == tournamentId && p.UserId == userId);

            if (p != null)
            {
                p.FantasyBalance -= amount;
                await _context.SaveChangesAsync();
            }
        }
    }
}