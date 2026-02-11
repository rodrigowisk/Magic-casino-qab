using Magic_casino_tournament.Controllers;
using Magic_casino_tournament.Data;
using Magic_casino_tournament.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Text.RegularExpressions;
// 👇 1. Imports do SignalR adicionados
using Microsoft.AspNetCore.SignalR;
using Magic_casino_tournament.Hubs;

namespace Magic_casino_tournament.Services
{
    public class TournamentService : ITournamentService
    {
        private readonly TournamentDbContext _context;
        private readonly ICoreGateway _coreGateway;
        private readonly IDistributedCache _cache;
        // 👇 2. Variável do Hub adicionada
        private readonly IHubContext<TournamentHub> _hubContext;

        // 👇 3. Construtor atualizado com injeção do Hub
        public TournamentService(
            TournamentDbContext context,
            ICoreGateway coreGateway,
            IDistributedCache cache,
            IHubContext<TournamentHub> hubContext)
        {
            _context = context;
            _coreGateway = coreGateway;
            _cache = cache;
            _hubContext = hubContext;
        }

        // ✅ VERSÃO OTIMIZADA (1 Consulta SQL apenas)
        public async Task<List<Tournament>> GetActiveTournamentsAsync(string? userId)
        {
            var query = _context.Tournaments
                .Where(t => t.IsActive && !t.IsFinished)
                .OrderBy(t => t.StartDate)
                .Select(t => new Tournament
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    Sport = t.Sport,
                    Category = t.Category,
                    CoverImage = t.CoverImage,
                    EntryFee = t.EntryFee,
                    HouseFeePercent = t.HouseFeePercent,
                    InitialFantasyBalance = t.InitialFantasyBalance,
                    PrizePool = t.PrizePool,
                    FixedPrize = t.FixedPrize,
                    MaxParticipants = t.MaxParticipants,
                    IsActive = t.IsActive,
                    IsFinished = t.IsFinished,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    FilterRules = t.FilterRules,
                    PrizeRuleId = t.PrizeRuleId,

                    // O EF Core traduz isso para COUNT() direto no SQL
                    ParticipantsCount = t.Participants.Count(),

                    // O EF Core traduz isso para EXISTS() direto no SQL
                    IsJoined = !string.IsNullOrEmpty(userId) && t.Participants.Any(p => p.UserId == userId)
                });

            return await query.ToListAsync();
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
            if (tournament.FixedPrize.HasValue && tournament.FixedPrize.Value > 0)
            {
                tournament.PrizePool = tournament.FixedPrize.Value;
            }
            else
            {
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

            if (await _context.TournamentParticipants.AnyAsync(p => p.TournamentId == tournamentId && p.UserId == userId))
                return "Usuário já inscrito.";

            if (tournament.MaxParticipants.HasValue && tournament.MaxParticipants.Value > 0)
            {
                int currentCount = await _context.TournamentParticipants.CountAsync(p => p.TournamentId == tournamentId);
                if (currentCount >= tournament.MaxParticipants.Value)
                {
                    return "Torneio lotado! Limite de participantes atingido.";
                }
            }

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
                Rank = 0,
                UserName = userName,
                Avatar = avatar
            };
            _context.TournamentParticipants.Add(participant);

            if (tournament.EntryFee > 0 && (tournament.FixedPrize == null || tournament.FixedPrize == 0))
            {
                decimal houseCut = tournament.EntryFee * (tournament.HouseFeePercent / 100m);
                tournament.PrizePool += (tournament.EntryFee - houseCut);
            }

            await _context.SaveChangesAsync();

            // Invalida cache ao entrar
            await _cache.RemoveAsync($"ranking:{tournamentId}");

            // 👇 Dispara atualização do ranking para quem estiver na tela (Novo Player)
            var novoRanking = await GetTournamentRankingAsync(tournamentId);
            await _hubContext.Clients.Group(tournamentId.ToString()).SendAsync("ReceiveRankingUpdate", novoRanking);

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

            // =================================================================
            // 👇 MÁGICA DO REAL-TIME AQUI
            // =================================================================
            try
            {
                // 1. Remove o cache antigo para forçar recálculo
                await _cache.RemoveAsync($"ranking:{tournamentId}");

                // 2. Busca o ranking atualizado (agora virá do banco com saldo descontado)
                var novoRanking = await GetTournamentRankingAsync(tournamentId);

                // 3. Envia para todos conectados no grupo deste torneio
                await _hubContext.Clients.Group(tournamentId.ToString())
                    .SendAsync("ReceiveRankingUpdate", novoRanking);

                Console.WriteLine($"📡 [SIGNALR] Ranking do torneio {tournamentId} atualizado com sucesso!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [SIGNALR ERROR] Falha ao enviar ranking: {ex.Message}");
            }
            // =================================================================

            return (true, "Aposta múltipla realizada com sucesso!");
        }

        public async Task<List<TournamentRankingDto>> GetTournamentRankingAsync(int tournamentId)
        {
            string cacheKey = $"ranking:{tournamentId}";

            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                try
                {
                    return JsonSerializer.Deserialize<List<TournamentRankingDto>>(cachedData) ?? new List<TournamentRankingDto>();
                }
                catch { }
            }

            var participants = await _context.TournamentParticipants
                .Where(p => p.TournamentId == tournamentId)
                .Include(p => p.Bets)
                .OrderByDescending(p => p.FantasyBalance)
                .ToListAsync();

            var ranking = participants.Select((p, index) =>
            {
                var apostasPendentes = p.Bets.Where(b => b.Status == "Pending").ToList();
                var apostasFinalizadas = p.Bets.Count(b => b.Status != "Pending");
                var totalApostas = p.Bets.Count;
                decimal potencialPendentes = apostasPendentes.Sum(b => b.PotentialWin);

                return new TournamentRankingDto
                {
                    Posicao = index + 1,
                    UserId = p.UserId,
                    UserName = string.IsNullOrEmpty(p.UserName) ? "Jogador" : p.UserName,
                    Avatar = p.Avatar ?? "",
                    SaldoAtual = p.FantasyBalance,
                    SaldoPossivel = p.FantasyBalance + potencialPendentes,
                    ProgressoBilhetes = $"{apostasFinalizadas}/{totalApostas}",
                    BilhetesFinalizados = apostasFinalizadas,
                    BilhetesTotais = totalApostas
                };
            }).ToList();

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

        public async Task ProcessGameResultAsync(string gameId, string score)
        {
            Console.WriteLine($"✅ [TOURNAMENT SERVICE] Processando resultado: Jogo {gameId} -> Placar {score}");

            var betsContainingGame = await _context.TournamentBetSelections
                .Include(s => s.TournamentBet)
                .ThenInclude(b => b.Participant)
                .Where(s => s.GameId == gameId && s.Status == "Pending")
                .ToListAsync();

            if (!betsContainingGame.Any()) return;

            Console.WriteLine($"🔍 Encontradas {betsContainingGame.Count} seleções pendentes para este jogo.");

            foreach (var selection in betsContainingGame)
            {
                bool won = CheckWinner(selection.MarketName, selection.SelectionName, score);
                selection.Status = won ? "Won" : "Lost";
            }

            await _context.SaveChangesAsync();

            var distinctBetIds = betsContainingGame.Select(s => s.TournamentBetId).Distinct().ToList();

            foreach (var betId in distinctBetIds)
            {
                var bet = await _context.TournamentBets
                    .Include(b => b.Selections)
                    .Include(b => b.Participant)
                    .FirstOrDefaultAsync(b => b.Id == betId);

                if (bet == null || bet.Status != "Pending") continue;

                if (bet.Selections.Any(s => s.Status == "Lost"))
                {
                    bet.Status = "Lost";
                    Console.WriteLine($"❌ Bilhete {bet.Id} PERDIDO.");
                }
                else if (bet.Selections.All(s => s.Status == "Won"))
                {
                    bet.Status = "Won";

                    if (bet.Participant != null)
                    {
                        bet.Participant.FantasyBalance += bet.PotentialWin;
                        Console.WriteLine($"🏆 Bilhete {bet.Id} VENCEU! Creditando {bet.PotentialWin} fichas para {bet.UserId}.");

                        // 👇 MÁGICA DO REAL-TIME TB NO FIM DO JOGO
                        // Atualiza ranking quando alguém ganha aposta
                        await _cache.RemoveAsync($"ranking:{bet.TournamentId}");
                        var novoRanking = await GetTournamentRankingAsync(bet.TournamentId);
                        await _hubContext.Clients.Group(bet.TournamentId.ToString()).SendAsync("ReceiveRankingUpdate", novoRanking);
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        private bool CheckWinner(string market, string outcome, string score)
        {
            try
            {
                if (string.IsNullOrEmpty(score) || !score.Contains("-")) return false;
                var cleanScore = score.Split(' ')[0].Trim();
                var parts = cleanScore.Split('-');
                if (parts.Length < 2) return false;
                if (!int.TryParse(parts[0], out int homeScore) || !int.TryParse(parts[1], out int awayScore)) return false;

                var mkt = market?.ToLower().Trim() ?? "";
                var sel = outcome?.ToLower().Trim() ?? "";

                if (mkt.Contains("result") || mkt.Contains("vencedor") || mkt == "1x2" || mkt.Contains("match winner"))
                {
                    if (sel == "1" || sel == "casa" || sel == "home") return homeScore > awayScore;
                    if (sel == "2" || sel == "fora" || sel == "away") return awayScore > homeScore;
                    if (sel == "x" || sel == "draw" || sel == "empate") return homeScore == awayScore;
                }
                if (mkt.Contains("double") || mkt.Contains("dupla"))
                {
                    if (sel == "1x" || (sel.Contains("casa") && sel.Contains("empate"))) return homeScore >= awayScore;
                    if (sel == "x2" || sel == "2x" || (sel.Contains("empate") && sel.Contains("fora"))) return awayScore >= homeScore;
                    if (sel == "12" || (sel.Contains("casa") && sel.Contains("fora"))) return homeScore != awayScore;
                }
                if (mkt.Contains("goal") || mkt.Contains("gols") || mkt.Contains("over") || mkt.Contains("under") || mkt == "total de gols")
                {
                    int totalGols = homeScore + awayScore;
                    var match = Regex.Match(sel.Replace(",", "."), @"(\d+(\.\d+)?)");
                    if (match.Success && double.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double line))
                    {
                        if (sel.Contains("over") || sel.Contains("mais") || sel.Contains("acima")) return totalGols > line;
                        if (sel.Contains("under") || sel.Contains("menos") || sel.Contains("abaixo")) return totalGols < line;
                    }
                }
                if (mkt.Contains("both teams") || mkt.Contains("ambos"))
                {
                    bool bttsYes = homeScore > 0 && awayScore > 0;
                    if (sel == "sim" || sel == "yes") return bttsYes;
                    if (sel == "não" || sel == "nao" || sel == "no") return !bttsYes;
                }
                return false;
            }
            catch { return false; }
        }
    }
}