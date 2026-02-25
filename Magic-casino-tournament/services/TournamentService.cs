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
                    IsJoined = !string.IsNullOrEmpty(userId) && t.Participants.Any(p => p.UserId == userId),

                    IsFavorite = !string.IsNullOrEmpty(userId) && _context.TournamentFavorites.Any(f => f.TournamentId == t.Id && f.UserId == userId)

                });

            return await query.ToListAsync();
        }



        // Dentro de Magic_casino_tournament.Services.TournamentService

        public async Task<object?> GetTournamentByIdAsync(int id, string? userId)
        {
            var tournament = await _context.Tournaments.FindAsync(id);
            if (tournament == null) return null;

            tournament.ParticipantsCount = await _context.TournamentParticipants.CountAsync(p => p.TournamentId == id);

            bool isJoined = false;
            decimal currentBalance = tournament.InitialFantasyBalance;
            int rank = 0;
            decimal myPrize = 0; // ✅ NOVA VARIÁVEL

            if (!string.IsNullOrEmpty(userId))
            {
                // 1. Busca dados do participante (Join, Rank, Saldo)
                var participant = await _context.TournamentParticipants
                    .FirstOrDefaultAsync(p => p.TournamentId == id && p.UserId == userId);

                if (participant != null)
                {
                    isJoined = true;
                    currentBalance = participant.FantasyBalance;
                    rank = participant.Rank;
                }

                // 2. ✅ CORREÇÃO CRÍTICA: Busca se já existe pagamento de prêmio para este usuário neste torneio
                // Isso garante que o valor apareça mesmo se o torneio já acabou
                myPrize = await _context.TournamentTransactions
                    .Where(tx => tx.TournamentId == id
                              && tx.UserId == userId
                              && (tx.Type == "PRIZE" || tx.Type == "Prize")) // Verifica tipos possíveis
                    .SumAsync(tx => (decimal?)tx.Amount) ?? 0;
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

                // Dados do Usuário
                IsJoined = isJoined,
                CurrentFantasyBalance = currentBalance,
                Rank = rank,
                MyPrize = myPrize // ✅ AGORA O FRONTEND VAI RECEBER O VALOR!
            };
        }


        public async Task<object> GetUserHistoryAsync(string userId)
        {
            var history = await (from tp in _context.TournamentParticipants
                                 join t in _context.Tournaments on tp.TournamentId equals t.Id
                                 where tp.UserId == userId
                                 // Ordenar do mais recente para o mais antigo
                                 orderby t.EndDate descending
                                 select new
                                 {
                                     Id = t.Id,
                                     Name = t.Name,

                                     // 👇 AQUI ESTÁ A CORREÇÃO MÁGICA 👇
                                     Description = t.Description,
                                     Sport = t.Sport,
                                     PrizePool = t.PrizePool,
                                     PrizeRuleId = t.PrizeRuleId,
                                     ParticipantsCount = t.Participants.Count(),
                                     // 👆 ========================== 👆

                                     EntryFee = t.EntryFee,
                                     InitialFantasy = t.InitialFantasyBalance,
                                     StartDate = t.StartDate,
                                     EndDate = t.EndDate,
                                     IsActive = t.IsActive,
                                     IsFinish = t.IsFinished,
                                     IsJoined = true,

                                     MyScore = tp.FantasyBalance,

                                     RealPrize = _context.TournamentTransactions
                                         .Where(tx => tx.TournamentId == t.Id
                                                   && tx.UserId == userId
                                                   && (tx.Type == "PRIZE" || tx.Type == "Prize"))
                                         .Sum(tx => (decimal?)tx.Amount) ?? 0,

                                     Rank = tp.Rank,
                                     Status = t.IsFinished ? "FINISHED" : (t.IsActive ? "ACTIVE" : "")
                                 }).ToListAsync();

            return history;
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

            // 1. Invalida cache de ranking (para quem está DENTRO do torneio)
            await _cache.RemoveAsync($"ranking:{tournamentId}");

            // 2. Atualiza quem está na tela de RANKING (Sala específica do torneio)
            var novoRanking = await GetTournamentRankingAsync(tournamentId);
            await _hubContext.Clients.Group(tournamentId.ToString()).SendAsync("ReceiveRankingUpdate", novoRanking);

            // 👇 3. NOVO: Atualiza quem está no LOBBY (Lista de torneios)
            try
            {
                // Conta quantos tem agora
                int newCount = await _context.TournamentParticipants.CountAsync(p => p.TournamentId == tournamentId);

                // Manda o aviso: "O torneio X agora tem Y inscritos"
                await _hubContext.Clients.Group("Lobby").SendAsync("UpdateParticipantCount", tournamentId, newCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro SignalR Lobby: {ex.Message}");
            }

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
                    Status = "Pending",
                    CommenceTime = sel.CommenceTime
                });
            }

            _context.TournamentBets.Add(bet);
            await _context.SaveChangesAsync();

            // =================================================================
            // 👇 MÁGICA DO REAL-TIME AQUI (ATUALIZADA)
            // =================================================================
            try
            {
                // 1. Remove o cache antigo para forçar recálculo
                await _cache.RemoveAsync($"ranking:{tournamentId}");

                // 2. Busca o ranking atualizado (agora virá do banco com saldo descontado)
                var novoRanking = await GetTournamentRankingAsync(tournamentId);

                // 3. Envia para todos conectados no grupo deste torneio (RANKING)
                await _hubContext.Clients.Group(tournamentId.ToString())
                    .SendAsync("ReceiveRankingUpdate", novoRanking);

                // 4. Envia para o usuário específico recarregar a lista de apostas (MY BETS)
                await _hubContext.Clients.Group($"user_{userId}")
                    .SendAsync("RefreshMyBets");

                Console.WriteLine($"📡 [SIGNALR] Ranking e Apostas atualizados para {userId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [SIGNALR ERROR] Falha ao enviar notificações: {ex.Message}");
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
                try { return JsonSerializer.Deserialize<List<TournamentRankingDto>>(cachedData) ?? new List<TournamentRankingDto>(); } catch { }
            }

            // CONSULTA BLINDADA (Calcula o saldo na hora, ignorando erro dos bots)
            // 1. Fazemos um JOIN com a tabela de Torneio para pegar o Saldo Inicial correto
            var query = from p in _context.TournamentParticipants
                        join t in _context.Tournaments on p.TournamentId equals t.Id
                        where p.TournamentId == tournamentId
                        // Calculamos o total apostado por esse usuário nesse torneio
                        let totalApostado = _context.TournamentBets
                                            .Where(b => b.TournamentId == tournamentId && b.UserId == p.UserId)
                                            .Sum(b => (decimal?)b.Amount) ?? 0
                        // Calculamos o total ganho (apenas status 'Won')
                        let totalGanho = _context.TournamentBets
                                         .Where(b => b.TournamentId == tournamentId && b.UserId == p.UserId && b.Status == "Won")
                                         .Sum(b => (decimal?)b.PotentialWin) ?? 0
                        // 🔥 O PULO DO GATO: O saldo é calculado matematicamente aqui
                        let saldoReal = t.InitialFantasyBalance - totalApostado + totalGanho
                        select new TournamentRankingDto
                        {
                            UserId = p.UserId,
                            UserName = string.IsNullOrEmpty(p.UserName) ? "Jogador" : p.UserName,
                            Avatar = p.Avatar ?? "",

                            // ✅ Usamos o saldo calculado, não o do banco que pode estar errado
                            SaldoAtual = saldoReal,
                            Points = saldoReal,

                            // Contagens
                            BilhetesTotais = _context.TournamentBets
                                .Count(b => b.TournamentId == tournamentId && b.UserId == p.UserId),

                            MyTicketCount = _context.TournamentBets
                                .Count(b => b.TournamentId == tournamentId && b.UserId == p.UserId),

                            BilhetesFinalizados = _context.TournamentBets
                                .Count(b => b.TournamentId == tournamentId && b.UserId == p.UserId && b.Status != "Pending"),

                            MyFinishedTickets = _context.TournamentBets
                                .Count(b => b.TournamentId == tournamentId && b.UserId == p.UserId && b.Status != "Pending"),

                            // Saldo Possível = Saldo Real + Potencial das pendentes
                            SaldoPossivel = saldoReal + (_context.TournamentBets
                                .Where(b => b.TournamentId == tournamentId && b.UserId == p.UserId && b.Status == "Pending")
                                .Sum(b => (decimal?)b.PotentialWin) ?? 0),

                            ProgressoBilhetes = "0/0",
                            Rank = 0,
                            Posicao = 0
                        };

            // Ordena pelo saldo calculado
            var rankingList = await query.OrderByDescending(p => p.SaldoAtual).ToListAsync();

            // Formatação final (Rank e Progresso)
            for (int i = 0; i < rankingList.Count; i++)
            {
                var r = rankingList[i];
                r.Posicao = i + 1;
                r.Rank = i + 1;
                r.ProgressoBilhetes = $"{r.BilhetesFinalizados}/{r.BilhetesTotais}";
            }

            var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5) };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(rankingList), cacheOptions);

            return rankingList;
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

        // Dentro de Magic_casino_tournament.Services.TournamentService

        private async Task ProcessSingleTournamentEnd(Tournament tournament)
        {
            Console.WriteLine($"🏁 [ENCERRAMENTO] Verificando Torneio #{tournament.Id} - {tournament.Name}");

            // TRAVA: Se tiver aposta pendente, não finaliza (Aguarda o outro robô resolver)
            bool hasPendingBets = await _context.TournamentBets
                .AnyAsync(b => b.TournamentId == tournament.Id && b.Status == "Pending");

            if (hasPendingBets)
            {
                Console.WriteLine($"⏳ [AGUARDANDO] Torneio #{tournament.Id} possui apostas pendentes.");
                return;
            }

            // --- PROCESSAMENTO DO FIM ---
            Console.WriteLine($"✅ [PROCESSANDO] Fechando Torneio #{tournament.Id}...");

            var participants = await _context.TournamentParticipants
                .Where(p => p.TournamentId == tournament.Id)
                .OrderByDescending(p => p.FantasyBalance)
                .ThenBy(p => p.Id) // 👈 CRÍTICO: Desempate por antiguidade de inscrição se houver empate de fichas
                .ToListAsync();

            if (!participants.Any())
            {
                tournament.IsFinished = true;
                tournament.IsActive = false; // Garante que some das listas ativas
                await _context.SaveChangesAsync();

                await _hubContext.Clients.Group("Lobby").SendAsync("RemoveTournamentFromList", tournament.Id);
                return;
            }

            // Calcula Ranking Final
            for (int i = 0; i < participants.Count; i++) participants[i].Rank = i + 1;

            // Distribui Prêmios (Usando a calculadora que já ajustamos para fechar em 100%)
            List<decimal> distribution = PrizeCalculator.GetDistribution(tournament.PrizeRuleId, participants.Count, tournament.EntryFee, tournament.PrizePool);

            // O PrizePool já está líquido no banco (A taxa de 10% já foi descontada na inscrição)
            decimal prizePool = tournament.PrizePool;

            var winners = participants.Take(distribution.Count).ToList();

            for (int i = 0; i < winners.Count; i++)
            {
                var percentage = distribution[i];
                if (percentage <= 0) continue;

                // 👈 CRÍTICO: Arredondar para 2 casas decimais (ex: 33.33) para evitar erros na API de pagamento
                decimal prizeAmount = Math.Round(percentage * prizePool, 2);
                var winner = winners[i];

                if (prizeAmount > 0)
                {
                    var paymentResult = await _coreGateway.AddFundsAsync(winner.UserId, prizeAmount, $"Prêmio Torneio #{tournament.Id} ({i + 1}º Lugar)");

                    var transaction = new TournamentTransaction
                    {
                        TournamentId = tournament.Id,
                        UserId = winner.UserId,
                        Amount = prizeAmount,
                        Type = "PRIZE",
                        Status = paymentResult.Success ? "COMPLETED" : "FAILED",
                        CoreTransactionId = paymentResult.TransactionId ?? $"RANK-WIN-{tournament.Id}-{i}",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.TournamentTransactions.Add(transaction);
                    Console.WriteLine($"🏆 [PAGAMENTO] {winner.UserId} ganhou {prizeAmount:C} (Rank {i + 1})");
                }
            }

            // Marca como finalizado no banco de dados
            tournament.IsFinished = true;
            tournament.IsActive = false;
            await _context.SaveChangesAsync();

            // 1. Avisa quem está DENTRO do torneio que acabou
            await _hubContext.Clients.Group(tournament.Id.ToString()).SendAsync("TournamentFinished");

            // 2. Avisa o LOBBY para remover o card da lista
            try
            {
                await _hubContext.Clients.Group("Lobby").SendAsync("RemoveTournamentFromList", tournament.Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro SignalR Lobby (Remove): {ex.Message}");
            }
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

            // 1. ATUALIZAÇÃO DOS DADOS (Na Memória)
            foreach (var selection in betsContainingGame)
            {
                bool won = CheckWinner(selection.MarketName, selection.SelectionName, score);
                selection.Status = won ? "Won" : "Lost";
                selection.FinalScore = score;
            }

            // Identifica quais apostas (Tickets) precisam ser fechadas
            var distinctBetIds = betsContainingGame.Select(s => s.TournamentBetId).Distinct().ToList();

            // Listas para guardar quem precisamos avisar DEPOIS de salvar
            var affectedTournamentIds = new HashSet<int>();
            var usersToNotify = new HashSet<string>();

            foreach (var betId in distinctBetIds)
            {
                var bet = await _context.TournamentBets
                    .Include(b => b.Selections)
                    .Include(b => b.Participant)
                    .FirstOrDefaultAsync(b => b.Id == betId);

                if (bet == null || bet.Status != "Pending") continue;

                // Regra: Se um perdeu, o bilhete perdeu. Se todos ganharam, o bilhete ganhou.
                if (bet.Selections.Any(s => s.Status == "Lost"))
                {
                    bet.Status = "Lost";
                    if (!string.IsNullOrEmpty(bet.UserId)) usersToNotify.Add(bet.UserId);
                }
                else if (bet.Selections.All(s => s.Status == "Won"))
                {
                    bet.Status = "Won";

                    // Atualiza saldo do participante
                    if (bet.Participant != null)
                    {
                        bet.Participant.FantasyBalance += bet.PotentialWin;
                        Console.WriteLine($"🏆 Bilhete {bet.Id} VENCEU! Creditando {bet.PotentialWin} fichas.");
                    }

                    // Marca que esse torneio precisa de atualização de ranking
                    affectedTournamentIds.Add(bet.TournamentId);
                    if (!string.IsNullOrEmpty(bet.UserId)) usersToNotify.Add(bet.UserId);
                }
            }

            // 2. SALVA NO BANCO (AQUI O SALDO É EFETIVADO)
            await _context.SaveChangesAsync();

            // 3. AGORA SIM: NOTIFICA O MUNDO (Com os dados já atualizados no banco)

            // A) Atualiza Rankings (Um por torneio afetado)
            foreach (var tId in affectedTournamentIds)
            {
                try
                {
                    // Limpa o cache velho
                    await _cache.RemoveAsync($"ranking:{tId}");

                    // Busca o novo (Agora o banco vai retornar o saldo correto!)
                    var novoRanking = await GetTournamentRankingAsync(tId);

                    // Avisa o Front
                    await _hubContext.Clients.Group(tId.ToString()).SendAsync("ReceiveRankingUpdate", novoRanking);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao atualizar ranking {tId}: {ex.Message}");
                }
            }

            // B) Atualiza "Minhas Apostas" dos usuários
            foreach (var userId in usersToNotify)
            {
                try
                {
                    await _hubContext.Clients.Group($"user_{userId}").SendAsync("RefreshMyBets");
                }
                catch { /* Ignora erro de envio individual */ }
            }
        }

        public async Task<bool> ToggleFavoriteAsync(int tournamentId, string userId)
        {
            var favorite = await _context.TournamentFavorites
                .FirstOrDefaultAsync(f => f.TournamentId == tournamentId && f.UserId == userId);

            if (favorite != null)
            {
                // Se já existe, remove (Desfavoritar)
                _context.TournamentFavorites.Remove(favorite);
                await _context.SaveChangesAsync();
                return false; // Retorna false indicando que agora NÃO é favorito
            }
            else
            {
                // Se não existe, cria (Favoritar)
                var newFavorite = new TournamentFavorite
                {
                    UserId = userId,
                    TournamentId = tournamentId
                };
                _context.TournamentFavorites.Add(newFavorite);
                await _context.SaveChangesAsync();
                return true; // Retorna true indicando que agora É favorito
            }
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