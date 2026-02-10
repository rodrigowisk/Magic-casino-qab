using Magic_casino_tournament.Controllers;
using Magic_casino_tournament.Data;
using Magic_casino_tournament.Models;
using Microsoft.EntityFrameworkCore;
// 👇 1. ADICIONADO PARA O REDIS E JSON
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Magic_casino_tournament.Services
{
    public class TournamentService : ITournamentService
    {
        private readonly TournamentDbContext _context;
        private readonly ICoreGateway _coreGateway;
        // 👇 2. CACHE INJETADO
        private readonly IDistributedCache _cache;

        public TournamentService(TournamentDbContext context, ICoreGateway coreGateway, IDistributedCache cache)
        {
            _context = context;
            _coreGateway = coreGateway;
            _cache = cache; // <--- Inicializa o cache
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

            // ... (código existente de verificação de usuário) ...
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
                tournament.PrizeRuleId,
                tournament.FixedPrize,
                tournament.MaxParticipants,

                IsJoined = isJoined,
                CurrentFantasyBalance = currentBalance,
                Rank = rank
            };
        }


        public async Task<Tournament> CreateTournamentAsync(Tournament tournament)
        {
            // ✅ LÓGICA DE PREMIAÇÃO INICIAL
            if (tournament.FixedPrize.HasValue && tournament.FixedPrize.Value > 0)
            {
                // Se for prêmio fixo, o PrizePool começa e permanece com esse valor
                tournament.PrizePool = tournament.FixedPrize.Value;
            }
            else
            {
                // Se for acumulado, começa zerado
                tournament.PrizePool = 0;
            }

            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();
            return tournament;
        }

        public async Task<string> JoinTournamentAsync(int tournamentId, string userId, string token, string userName, string avatar)
        {
            var tournament = await _context.Tournaments.FindAsync(tournamentId);
            if (tournament == null) return "Torneio não encontrado.";

            // 1. Checa se o usuário já está inscrito
            if (await _context.TournamentParticipants.AnyAsync(p => p.TournamentId == tournamentId && p.UserId == userId))
                return "Usuário já inscrito.";

            // ✅ 2. NOVO: CHECAGEM DE LIMITE DE USUÁRIOS
            // Se MaxParticipants tiver valor (> 0) e a contagem atual for igual ou maior, bloqueia.
            if (tournament.MaxParticipants.HasValue && tournament.MaxParticipants.Value > 0)
            {
                int currentCount = await _context.TournamentParticipants.CountAsync(p => p.TournamentId == tournamentId);
                if (currentCount >= tournament.MaxParticipants.Value)
                {
                    return "Torneio lotado! Limite de participantes atingido.";
                }
            }

            // 3. Cobrança da Taxa de Entrada (Sem alterações)
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

            // 4. Adiciona o Participante (Sem alterações)
            var participant = new TournamentParticipant
            {
                TournamentId = tournamentId,
                UserId = userId,
                FantasyBalance = tournament.InitialFantasyBalance,
                Rank = 0,
                UserName = userName,
                Avatar = avatar
            };
            _context.TournamentParticipants.Add(participant);

            // ✅ 5. LÓGICA DE ATUALIZAÇÃO DO PRÊMIO (PRIZE POOL)
            // Só adiciona ao prêmio se NÃO for Fixo (FixedPrize == null)
            if (tournament.EntryFee > 0 && (tournament.FixedPrize == null || tournament.FixedPrize == 0))
            {
                decimal houseCut = tournament.EntryFee * (tournament.HouseFeePercent / 100m);
                tournament.PrizePool += (tournament.EntryFee - houseCut);
            }
            // Se for FixedPrize, o valor do PrizePool não muda (a casa absorve a entrada e paga o fixo)

            await _context.SaveChangesAsync();

            // Invalida cache do ranking se necessário
            await _cache.RemoveAsync($"ranking:{tournamentId}");

            return "Success";
        }


        public async Task<string> PlaceBetAsync(int tournamentId, string userId, TournamentBet bet)
        {
            return "Use o método PlaceBatchBetsAsync para suportar a nova estrutura.";
        }

        public async Task<(bool Success, string Message)> PlaceBatchBetsAsync(int tournamentId, string userId, PlaceTournamentBetRequest request)
        {
            var participant = await _context.TournamentParticipants
                .FirstOrDefaultAsync(p => p.TournamentId == tournamentId && p.UserId == userId);

            if (participant == null)
                return (false, "Participante não encontrado ou não inscrito.");

            decimal totalCost = request.Amount;

            if (participant.FantasyBalance < totalCost)
                return (false, "Saldo de fichas insuficiente.");

            participant.FantasyBalance -= totalCost;

            decimal totalOdds = request.Selections.Any()
                ? request.Selections.Aggregate(1m, (acc, sel) => acc * sel.Odds)
                : 0;

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

            // 🔥 Opcional: Invalidar o cache imediatamente após a aposta
            // await _cache.RemoveAsync($"ranking:{tournamentId}");

            return (true, "Aposta múltipla realizada com sucesso!");
        }

        // ============================================================
        // 🔄 MÉTODO ATUALIZADO COM REDIS + DTO
        // ============================================================
        public async Task<List<TournamentRankingDto>> GetTournamentRankingAsync(int tournamentId)
        {
            string cacheKey = $"ranking:{tournamentId}";

            // 1. TENTA LER DO REDIS (Rápido)
            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                try
                {
                    return JsonSerializer.Deserialize<List<TournamentRankingDto>>(cachedData) ?? new List<TournamentRankingDto>();
                }
                catch
                {
                    // Se falhar o deserialize, ignora e busca do banco
                }
            }

            // 2. SE NÃO ACHOU, VAI NO BANCO (Lento)
            var participants = await _context.TournamentParticipants
                .Where(p => p.TournamentId == tournamentId)
                .Include(p => p.Bets) // 👈 Essencial para calcular "Bilhetes" e "Saldo Possível"
                .OrderByDescending(p => p.FantasyBalance)
                .ToListAsync();

            // 3. Mapeamento para o DTO
            var ranking = participants.Select((p, index) =>
            {
                // Dados auxiliares das apostas
                var apostasPendentes = p.Bets.Where(b => b.Status == "Pending").ToList();
                var apostasFinalizadas = p.Bets.Count(b => b.Status != "Pending");
                var totalApostas = p.Bets.Count;

                // Soma do que ele PODE ganhar nas apostas abertas
                decimal potencialPendentes = apostasPendentes.Sum(b => b.PotentialWin);

                return new TournamentRankingDto
                {
                    Posicao = index + 1,
                    UserId = p.UserId,
                    UserName = string.IsNullOrEmpty(p.UserName) ? "Jogador" : p.UserName,
                    Avatar = p.Avatar ?? "",

                    SaldoAtual = p.FantasyBalance,

                    // Saldo Possível = O que tenho hoje + O que posso ganhar
                    SaldoPossivel = p.FantasyBalance + potencialPendentes,

                    // Formato "3/10" (Finalizadas/Total)
                    ProgressoBilhetes = $"{apostasFinalizadas}/{totalApostas}",

                    BilhetesFinalizados = apostasFinalizadas,
                    BilhetesTotais = totalApostas
                };
            }).ToList();

            // 4. SALVA NO REDIS POR 5 SEGUNDOS
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
            };

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(ranking), cacheOptions);

            return ranking;
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
                .Include(b => b.Selections)
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

        public async Task ProcessFinishedTournamentsAsync()
        {
            var finishedTournaments = await _context.Tournaments
                .Where(t => t.IsActive && !t.IsFinished && t.EndDate <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var tournament in finishedTournaments)
            {
                await ProcessSingleTournamentEnd(tournament);
            }
        }

        private async Task ProcessSingleTournamentEnd(Tournament tournament)
        {
            Console.WriteLine($"🏁 [ENCERRAMENTO] Processando Torneio #{tournament.Id} - {tournament.Name}");

            var participants = await _context.TournamentParticipants
                .Where(p => p.TournamentId == tournament.Id)
                .OrderByDescending(p => p.FantasyBalance)
                .ToListAsync();

            if (!participants.Any())
            {
                tournament.IsFinished = true;
                await _context.SaveChangesAsync();
                return;
            }

            for (int i = 0; i < participants.Count; i++)
            {
                participants[i].Rank = i + 1;
            }

            List<decimal> distribution = PrizeCalculator.GetDistribution(tournament.PrizeRuleId, participants.Count);
            decimal prizePool = tournament.PrizePool;
            var winners = participants.Take(distribution.Count).ToList();

            for (int i = 0; i < winners.Count; i++)
            {
                var percentage = distribution[i];
                if (percentage <= 0) continue;

                decimal prizeAmount = percentage * prizePool;
                var winner = winners[i];

                var paymentResult = await _coreGateway.AddFundsAsync(winner.UserId, prizeAmount, $"Prêmio Torneio #{tournament.Id} ({i + 1}º Lugar)");

                var transaction = new TournamentTransaction
                {
                    TournamentId = tournament.Id,
                    UserId = winner.UserId,
                    Amount = prizeAmount,
                    Type = "PRIZE",
                    Status = paymentResult.Success ? "COMPLETED" : "FAILED",
                    CoreTransactionId = paymentResult.TransactionId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.TournamentTransactions.Add(transaction);
                Console.WriteLine($"🏆 [PAGAMENTO] {winner.UserId} (Rank {i + 1}) ganhou {prizeAmount:C} ({percentage:P}) - Regra: {tournament.PrizeRuleId}");
            }

            tournament.IsFinished = true;
            await _context.SaveChangesAsync();
        }
    }
}