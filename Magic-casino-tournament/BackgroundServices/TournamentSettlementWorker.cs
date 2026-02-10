using Magic_casino_tournament.Data;
using Magic_casino_tournament.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Magic_casino_tournament.BackgroundServices
{
    public class TournamentSettlementWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TournamentSettlementWorker> _logger;

        private const string SPORTBOOK_API_URL = "http://sportbook:8080/api/sports/event";

        public TournamentSettlementWorker(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory, ILogger<TournamentSettlementWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🏆 [WORKER] Robô de Pagamento e Encerramento Iniciado...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();

                        // ETAPA 1: Processar apostas individuais (Atualiza FantasyBalance)
                        await ProcessPendingBets(context);

                        // ETAPA 2: Processar encerramento de torneios (Verifica fim e paga prêmio)
                        await ProcessTournamentClosures(context);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Erro no ciclo de pagamento: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }

        // --- ETAPA 1: PAGA AS APOSTAS INDIVIDUAIS ---
        private async Task ProcessPendingBets(TournamentDbContext context)
        {
            var pendingBets = await context.TournamentBets
                .Include(b => b.Selections)
                .Include(b => b.Participant)
                .Where(b => b.Status == "Pending")
                .ToListAsync();

            if (!pendingBets.Any()) return;

            // Otimização: Busca dados dos jogos apenas uma vez
            var gameIdsToCheck = pendingBets.SelectMany(b => b.Selections).Select(s => s.GameId).Distinct().ToList();
            var gameResults = new Dictionary<string, SportbookGameDto>();
            var client = _httpClientFactory.CreateClient();

            foreach (var gameId in gameIdsToCheck)
            {
                var gameData = await FetchGameFromSportbook(client, gameId);
                if (gameData != null) gameResults[gameId] = gameData;
            }

            foreach (var bet in pendingBets)
            {
                bool allGamesFinished = true;
                bool ticketWon = true;

                foreach (var selection in bet.Selections)
                {
                    if (!gameResults.ContainsKey(selection.GameId))
                    {
                        allGamesFinished = false;
                        break;
                    }

                    var game = gameResults[selection.GameId];

                    // Verifica se o jogo acabou
                    if (game.Status != "Ended" && game.Status != "Completed" && game.Status != "FT")
                    {
                        allGamesFinished = false;
                        break;
                    }

                    string score = game.Score;
                    bool won = CheckWinnerRobust(selection.MarketName, selection.SelectionName, score);

                    selection.Status = won ? "Won" : "Lost";
                    if (!won) ticketWon = false;
                }

                if (allGamesFinished)
                {
                    bet.Status = ticketWon ? "Won" : "Lost";
                    bet.SettledAt = DateTime.UtcNow;

                    if (ticketWon && bet.Participant != null)
                    {
                        // Atualiza Saldo Fantasy
                        bet.Participant.FantasyBalance += bet.PotentialWin;

                        // Cria Transação de Auditoria da aposta
                        var transaction = new TournamentTransaction
                        {
                            UserId = bet.UserId,
                            TournamentId = bet.TournamentId,
                            Amount = bet.PotentialWin,
                            Type = "BET_WIN",
                            Status = "COMPLETED",
                            CreatedAt = DateTime.UtcNow,
                            CoreTransactionId = $"BET-{bet.Id}"
                        };
                        context.Add(transaction);

                        _logger.LogInformation($"💰 APOSTA PAGA: Torneio {bet.TournamentId} | User {bet.UserId} | +{bet.PotentialWin}");
                    }
                    else
                    {
                        _logger.LogInformation($"🗑️ APOSTA PERDIDA: Aposta {bet.Id} finalizada.");
                    }
                }
            }

            await context.SaveChangesAsync();
        }

        // --- ETAPA 2: ENCERRA O TORNEIO E PAGA O RANKING ---
        private async Task ProcessTournamentClosures(TournamentDbContext context)
        {
            // 1. Busca torneios que já passaram da data final mas ainda não foram finalizados
            var tournamentsToClose = await context.Tournaments
                .Where(t => !t.IsFinished && t.EndDate <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var tournament in tournamentsToClose)
            {
                // 2. A VERIFICAÇÃO CRUCIAL QUE VOCÊ PEDIU:
                // Verifica se existe QUALQUER aposta neste torneio que ainda esteja "Pending"
                bool hasPendingBets = await context.TournamentBets
                    .AnyAsync(b => b.TournamentId == tournament.Id && b.Status == "Pending");

                if (hasPendingBets)
                {
                    // Se tiver aposta pendente, NÃO ENCERRA.
                    // O ProcessPendingBets precisa resolver os jogos pendentes primeiro.
                    _logger.LogInformation($"⏳ Torneio {tournament.Id} expirado, mas aguardando finalização de apostas pendentes...");
                    continue;
                }

                // 3. Se chegou aqui, o prazo acabou E não há apostas pendentes. Podemos encerrar.
                _logger.LogInformation($"🏁 Encerrando Torneio {tournament.Id}. Calculando Ranking...");

                // Pega os participantes ordenados pelo maior saldo (FantasyBalance)
                var rankings = await context.TournamentParticipants
                    .Where(p => p.TournamentId == tournament.Id)
                    .OrderByDescending(p => p.FantasyBalance)
                    .ToListAsync();

                if (rankings.Any())
                {
                    // --- LÓGICA DE DISTRIBUIÇÃO DE PRÊMIO ---
                    // Exemplo: 1º Lugar leva o PrizePool inteiro. 
                    // Você pode adaptar isso para dividir (ex: 50% 1º, 30% 2º, 20% 3º)

                    var winner = rankings.First();
                    decimal prizeAmount = tournament.PrizePool;

                    if (prizeAmount > 0)
                    {
                        // Cria registro do prêmio
                        var prizeTransaction = new TournamentTransaction
                        {
                            UserId = winner.UserId,
                            TournamentId = tournament.Id,
                            Amount = prizeAmount,
                            Type = "TOURNAMENT_PRIZE", // Tipo específico para prêmio final
                            Status = "COMPLETED", // Ou "PENDING" se outro serviço for processar o pagamento real
                            CreatedAt = DateTime.UtcNow,
                            CoreTransactionId = $"RANK-WIN-{tournament.Id}-{winner.UserId}"
                        };

                        context.Add(prizeTransaction);

                        _logger.LogInformation($"🏆 PRÊMIO DE TORNEIO: User {winner.UserId} venceu o torneio {tournament.Id}. Prêmio: {prizeAmount:C}");
                    }
                }

                // 4. Marca o torneio como finalizado
                tournament.IsFinished = true;
                tournament.IsActive = false; // Tira da lista de ativos
            }

            await context.SaveChangesAsync();
        }

        private async Task<SportbookGameDto?> FetchGameFromSportbook(HttpClient client, string gameId)
        {
            try
            {
                var response = await client.GetAsync($"{SPORTBOOK_API_URL}/{gameId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize<SportbookGameDto>(json, options);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Falha API Sportbook ({gameId}): {ex.Message}");
            }
            return null;
        }

        private bool CheckWinnerRobust(string market, string outcome, string score)
        {
            try
            {
                if (string.IsNullOrEmpty(score) || !score.Contains("-")) return false;

                // Limpeza básica do placar (remove espaços extras)
                var cleanScore = score.Split(' ')[0].Trim();
                var parts = cleanScore.Split('-');

                if (parts.Length < 2) return false;
                if (!int.TryParse(parts[0], out int homeScore) || !int.TryParse(parts[1], out int awayScore)) return false;

                var mkt = market?.ToLower().Trim() ?? "";
                var sel = outcome?.ToLower().Trim() ?? "";

                // 1x2 / Vencedor
                if (mkt.Contains("result") || mkt.Contains("vencedor") || mkt == "1x2" || mkt.Contains("match winner"))
                {
                    if (sel == "1" || sel == "casa" || sel == "home") return homeScore > awayScore;
                    if (sel == "2" || sel == "fora" || sel == "away") return awayScore > homeScore;
                    if (sel == "x" || sel == "draw" || sel == "empate") return homeScore == awayScore;
                }

                // Chance Dupla
                if (mkt.Contains("double") || mkt.Contains("dupla"))
                {
                    if (sel == "1x") return homeScore >= awayScore;
                    if (sel == "x2") return awayScore >= homeScore;
                    if (sel == "12") return homeScore != awayScore;
                }

                // Over/Under (Gols)
                if (mkt.Contains("goal") || mkt.Contains("gols") || mkt.Contains("over") || mkt.Contains("under"))
                {
                    int totalGols = homeScore + awayScore;
                    // Tenta extrair o número da aposta (ex: "Over 2.5" -> 2.5)
                    var match = Regex.Match(sel.Replace(",", "."), @"(\d+(\.\d+)?)");
                    if (match.Success && double.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double line))
                    {
                        if (sel.Contains("over") || sel.Contains("mais") || sel.Contains("acima")) return totalGols > line;
                        if (sel.Contains("under") || sel.Contains("menos") || sel.Contains("abaixo")) return totalGols < line;
                    }
                }

                // Ambos Marcam (BTTS)
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