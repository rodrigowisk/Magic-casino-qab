using Magic_casino_tournament.Data;
using Magic_casino_tournament.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using Magic_casino_tournament.Services;
using Microsoft.AspNetCore.SignalR;
using Magic_casino_tournament.Hubs;
using Microsoft.Extensions.Caching.Distributed; // 👈 Adicionado para manipulação de Cache

namespace Magic_casino_tournament.BackgroundServices
{
    public class TournamentSettlementWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TournamentSettlementWorker> _logger;
        private readonly IHubContext<TournamentHub> _hubContext;

        private const string SPORTBOOK_API_URL = "http://sportbook:8080/api/sports/event";

        public TournamentSettlementWorker(
            IServiceProvider serviceProvider,
            IHttpClientFactory httpClientFactory,
            ILogger<TournamentSettlementWorker> logger,
            IHubContext<TournamentHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🎯 [WORKER] Robô de Liquidação de Apostas Iniciado...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
                        // 👇 Serviços adicionados no escopo para atualizar o Front em tempo real
                        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
                        var tournamentService = scope.ServiceProvider.GetRequiredService<ITournamentService>();

                        // 1. Pagar Apostas dos Torneios (Com lógica de Void/Cancelamento)
                        await ProcessPendingBets(context, cache, tournamentService);

                        // 2. Avisar Front-end sobre torneios acabando
                        await NotifyEndingTournaments(context);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Erro no ciclo de liquidação de apostas: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task NotifyEndingTournaments(TournamentDbContext context)
        {
            var now = DateTime.UtcNow;
            var oneMinuteFromNow = now.AddMinutes(1);

            var endingSoon = await context.Tournaments
                .Where(t => !t.IsFinished && t.IsActive && t.EndDate <= oneMinuteFromNow && t.EndDate > now)
                .ToListAsync();

            foreach (var t in endingSoon)
            {
                var remainingSeconds = (t.EndDate - now).TotalSeconds;
                await _hubContext.Clients.Group(t.Id.ToString())
                    .SendAsync("TournamentTimerSync", remainingSeconds);
            }
        }

        // ==============================================================================
        // 1. PROCESSAMENTO DE APOSTAS APENAS (SEM PAGAR O PRÊMIO FINAL DO TORNEIO)
        // ==============================================================================
        private async Task ProcessPendingBets(TournamentDbContext context, IDistributedCache cache, ITournamentService tournamentService)
        {
            var pendingBets = await context.TournamentBets
                .Include(b => b.Selections)
                .Include(b => b.Participant)
                .Where(b => b.Status == "Pending")
                .ToListAsync();

            if (!pendingBets.Any()) return;

            var gameIdsToCheck = pendingBets.SelectMany(b => b.Selections).Select(s => s.GameId).Distinct().ToList();
            var gameResults = new Dictionary<string, SportbookGameDto>();
            var client = _httpClientFactory.CreateClient();

            foreach (var gameId in gameIdsToCheck)
            {
                var gameData = await FetchGameFromSportbook(client, gameId);
                if (gameData != null) gameResults[gameId] = gameData;
            }

            // 👇 Listas para rastrear quais torneios e usuários precisam ser avisados da atualização
            var affectedTournamentIds = new HashSet<int>();
            var usersToNotify = new HashSet<string>();

            foreach (var bet in pendingBets)
            {
                bool allGamesFinished = true;
                bool ticketWon = true;
                bool isVoid = false;

                foreach (var selection in bet.Selections)
                {
                    // 🚀 1. INTERVENÇÃO MANUAL: Se você preencheu o placar no DBeaver, ele ignora a API e aceita o seu placar!
                    if (!string.IsNullOrEmpty(selection.FinalScore) && selection.FinalScore.Contains("-") && (selection.Status == "Pending" || string.IsNullOrEmpty(selection.Status)))
                    {
                        bool isWinner = CheckWinnerRobust(selection.MarketName, selection.SelectionName, selection.FinalScore);
                        selection.Status = isWinner ? "Won" : "Lost";
                        if (!isWinner) ticketWon = false;
                        continue; // Pula a busca na API e vai para a próxima seleção
                    }

                    // Se a seleção já foi resolvida (ex: numa passada anterior do worker), só atualiza as flags
                    if (selection.Status == "Won" || selection.Status == "Lost" || selection.Status == "Void")
                    {
                        if (selection.Status == "Lost") ticketWon = false;
                        if (selection.Status == "Void") isVoid = true;
                        continue;
                    }

                    if (!gameResults.ContainsKey(selection.GameId)) { allGamesFinished = false; break; }
                    var game = gameResults[selection.GameId];

                    if (game.Status == "Cancelled" || game.Status == "Abandoned" || game.Status == "Void")
                    {
                        selection.Status = "Void";
                        selection.FinalScore = "Cancelado"; // 🚀 Salva automaticamente o aviso no banco
                        isVoid = true;
                        continue;
                    }

                    if (game.Status != "Ended" && game.Status != "Completed" && game.Status != "FT" && game.Status != "3")
                    {
                        allGamesFinished = false;
                        break;
                    }

                    string score = game.Score;

                    // 🚀 2. SALVAR PLACAR DA API: Agora ele salva o resultado real no banco de dados sozinho!
                    selection.FinalScore = score;

                    bool won = CheckWinnerRobust(selection.MarketName, selection.SelectionName, score);
                    selection.Status = won ? "Won" : "Lost";
                    if (!won) ticketWon = false;
                }

                if (allGamesFinished)
                {
                    if (isVoid)
                    {
                        bet.Status = "Void";
                        bet.SettledAt = DateTime.UtcNow.AddHours(-3);

                        if (bet.Participant != null)
                        {
                            bet.Participant.FantasyBalance += bet.Amount;

                            var transaction = new TournamentTransaction
                            {
                                UserId = bet.UserId,
                                TournamentId = bet.TournamentId,
                                Amount = bet.Amount,
                                Type = "BET_REFUND",
                                Status = "COMPLETED",
                                CreatedAt = DateTime.UtcNow.AddHours(-3),
                                CoreTransactionId = $"REFUND-{bet.Id}"
                            };
                            context.Add(transaction);
                        }
                    }
                    else
                    {
                        bet.Status = ticketWon ? "Won" : "Lost";
                        bet.SettledAt = DateTime.UtcNow.AddHours(-3);

                        if (ticketWon && bet.Participant != null)
                        {
                            bet.Participant.FantasyBalance += bet.PotentialWin;

                            var transaction = new TournamentTransaction
                            {
                                UserId = bet.UserId,
                                TournamentId = bet.TournamentId,
                                Amount = bet.PotentialWin,
                                Type = "BET_WIN",
                                Status = "COMPLETED",
                                CreatedAt = DateTime.UtcNow.AddHours(-3),
                                CoreTransactionId = $"BET-{bet.Id}"
                            };
                            context.Add(transaction);
                        }
                    }

                    // 👇 Adiciona nas listas para atualização em tempo real após o SaveChanges
                    affectedTournamentIds.Add(bet.TournamentId);
                    if (!string.IsNullOrEmpty(bet.UserId)) usersToNotify.Add(bet.UserId);
                }
            }
            await context.SaveChangesAsync(); // AQUI ELE SALVA TUDO (Status e FinalScore) NO BANCO!

            // =================================================================
            // 🚀 MÁGICA DO TEMPO REAL: Atualiza a tela dos jogadores imediatamente
            // =================================================================
            foreach (var tId in affectedTournamentIds)
            {
                try
                {
                    // 1. Limpa o cache antigo
                    await cache.RemoveAsync($"ranking:{tId}");
                    // 2. Busca o novo ranking (com o bilhete que acabou de ser processado)
                    var novoRanking = await tournamentService.GetTournamentRankingAsync(tId);
                    // 3. Atualiza a tela de todos na aba de Ranking
                    await _hubContext.Clients.Group(tId.ToString()).SendAsync("ReceiveRankingUpdate", novoRanking);
                }
                catch (Exception ex) { _logger.LogError($"Erro ao notificar ranking: {ex.Message}"); }
            }

            foreach (var userId in usersToNotify)
            {
                try
                {
                    // 4. Atualiza a tela de Minhas Apostas do usuário afetado
                    await _hubContext.Clients.Group($"user_{userId}").SendAsync("RefreshMyBets");
                }
                catch { }
            }
        }

        private async Task<SportbookGameDto?> FetchGameFromSportbook(HttpClient client, string gameId)
        {
            try
            {
                var response = await client.GetAsync($"{SPORTBOOK_API_URL}/{gameId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<SportbookGameDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch { }
            return null;
        }

        private bool CheckWinnerRobust(string market, string outcome, string score)
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
                    if (sel == "1x") return homeScore >= awayScore;
                    if (sel == "x2") return awayScore >= homeScore;
                    if (sel == "12") return homeScore != awayScore;
                }
                if (mkt.Contains("goal") || mkt.Contains("gols") || mkt.Contains("over") || mkt.Contains("under"))
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