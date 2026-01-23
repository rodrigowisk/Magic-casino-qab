using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class LiveScoreWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _betsApiToken;
        private const string BASE_URL = "https://api.b365api.com";

        public LiveScoreWorker(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
            _betsApiToken = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("\n-------------------------------------------------------------");
            Console.WriteLine("🏁 [LIVE SCORE] WORKER 6.1: VALIDAÇÃO PADRÃO (1X2) 🏁");
            Console.WriteLine("-------------------------------------------------------------\n");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var walletService = scope.ServiceProvider.GetRequiredService<CoreWalletService>();

                        await SettleBetsAsync(context, walletService);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [WORKER ERRO]: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task SettleBetsAsync(AppDbContext context, CoreWalletService walletService)
        {
            // 1. Busca apostas pendentes
            var pendingSelections = await context.BetSelections
                .Include(s => s.Bet)
                .Where(s => s.Status == "pending")
                .ToListAsync();

            if (!pendingSelections.Any()) return;

            // 2. Mapeia os jogos necessários
            var matchIds = pendingSelections.Select(b => b.MatchId).Distinct().ToList();
            var dbGames = await context.SportsEvents
                .Where(g => matchIds.Contains(g.ExternalId))
                .ToListAsync();

            var gamesMap = dbGames.ToDictionary(g => g.ExternalId);
            var gamesToForceUpdate = new List<string>();
            bool dbChanged = false;

            // 3. Processa Resultados
            foreach (var selection in pendingSelections)
            {
                if (gamesMap.TryGetValue(selection.MatchId, out var game))
                {
                    // Verifica se o jogo acabou (Ended ou Status 3) E tem placar válido
                    if ((game.Status == "Ended" || game.Status == "3") && !string.IsNullOrEmpty(game.Score) && game.Score.Contains("-"))
                    {
                        // Removemos a necessidade de passar nomes dos times, usamos a lógica pura 1x2
                        ProcessSelectionResult(selection, game.Score);

                        if (selection.Bet != null)
                        {
                            await CheckAndSettleTicketAsync(selection.Bet, context, walletService);
                        }

                        dbChanged = true;
                    }
                    // Fallback: Se o jogo é antigo (> 4h) e ainda não finalizou, força check na API
                    else if (game.CommenceTime < DateTime.UtcNow.AddHours(-4))
                    {
                        if (!gamesToForceUpdate.Contains(game.ExternalId)) gamesToForceUpdate.Add(game.ExternalId);
                    }
                }
                else
                {
                    if (!gamesToForceUpdate.Contains(selection.MatchId)) gamesToForceUpdate.Add(selection.MatchId);
                }
            }

            if (dbChanged) await context.SaveChangesAsync();

            // 4. Resgate na API para jogos travados/velhos
            if (gamesToForceUpdate.Any() && !string.IsNullOrEmpty(_betsApiToken))
            {
                var batches = gamesToForceUpdate.Chunk(10);
                foreach (var batch in batches)
                {
                    await FetchAndSettleFromApi(batch.ToList(), context, walletService);
                }
            }
        }

        private void ProcessSelectionResult(BetSelection selection, string score)
        {
            selection.FinalScore = score;

            // Validação direta baseada em códigos (1, X, 2)
            bool won = CheckWinner(selection.MarketName, selection.OutcomeName, score);

            selection.Status = won ? "Won" : "Lost";
            selection.IsWinner = won;

            if (won) Console.WriteLine($"   ✅ Aposta VENCEDORA: {selection.MarketName} -> {selection.OutcomeName} (Placar: {score})");
        }

        // 🔥 LÓGICA CORRIGIDA: Validação Padrão (1, X, 2)
        private bool CheckWinner(string market, string outcome, string score)
        {
            try
            {
                if (string.IsNullOrEmpty(score) || !score.Contains("-")) return false;
                var parts = score.Split('-');
                if (!int.TryParse(parts[0], out int homeScore) || !int.TryParse(parts[1], out int awayScore)) return false;

                var mkt = market?.ToLower().Trim() ?? "";
                var sel = outcome?.ToLower().Trim() ?? "";

                // --- 1. MERCADO: VENCEDOR (1X2) ---
                if (mkt.Contains("result") || mkt.Contains("vencedor") || mkt == "1x2" || mkt.Contains("match winner"))
                {
                    // 1 = Casa Vence
                    if (sel == "1") return homeScore > awayScore;

                    // 2 = Fora Vence
                    if (sel == "2") return awayScore > homeScore;

                    // X = Empate
                    if (sel == "x" || sel == "draw" || sel == "empate") return homeScore == awayScore;
                }

                // --- 2. MERCADO: DUPLA HIPÓTESE ---
                if (mkt.Contains("double") || mkt.Contains("dupla"))
                {
                    if (sel == "1x") return homeScore >= awayScore; // Casa ou Empate
                    if (sel == "x2" || sel == "2x") return awayScore >= homeScore; // Fora ou Empate
                    if (sel == "12") return homeScore != awayScore; // Casa ou Fora (sem empate)
                }

                // --- 3. MERCADO: GOLS (OVER/UNDER) ---
                if (mkt.Contains("goal") || mkt.Contains("gols") || mkt.Contains("over") || mkt.Contains("under"))
                {
                    int totalGols = homeScore + awayScore;

                    // Extrai o número da aposta (ex: "Over 2.5" -> 2.5)
                    var match = Regex.Match(sel, @"(\d+(\.\d+)?)");
                    if (match.Success && double.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double line))
                    {
                        if (sel.Contains("over") || sel.Contains("mais") || sel.Contains("acima"))
                            return totalGols > line;

                        if (sel.Contains("under") || sel.Contains("menos") || sel.Contains("abaixo"))
                            return totalGols < line;
                    }
                }

                // --- 4. MERCADO: AMBOS MARCAM (BTTS) ---
                if (mkt.Contains("both teams") || mkt.Contains("ambos"))
                {
                    bool bttsYes = homeScore > 0 && awayScore > 0;
                    if (sel == "sim" || sel == "yes") return bttsYes;
                    if (sel == "não" || sel == "no") return !bttsYes;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task CheckAndSettleTicketAsync(Bet ticket, AppDbContext context, CoreWalletService walletService)
        {
            var allSelections = await context.BetSelections.Where(s => s.BetId == ticket.Id).ToListAsync();

            if (allSelections.Any(s => s.Status == "pending")) return; // Ainda tem jogo rolando

            // Se perdeu alguma, o bilhete todo perde
            if (allSelections.Any(s => s.Status == "Lost"))
            {
                if (ticket.Status != "Lost")
                {
                    ticket.Status = "Lost";
                    Console.WriteLine($"🎫 [BILHETE PERDIDO] ID: {ticket.Id}");
                }
                return;
            }

            // Se ganhou todas
            if (allSelections.All(s => s.Status == "Won") && ticket.Status != "Won")
            {
                Console.WriteLine($"🏆 [BILHETE VENCEU] ID: {ticket.Id} -> PAGANDO R$ {ticket.PotentialReturn:N2}...");

                var result = await walletService.CreditFundsAsync(ticket.UserCpf, ticket.PotentialReturn);

                if (result.Success)
                {
                    ticket.Status = "Won";
                    Console.WriteLine($"✅ [PAGAMENTO EFETUADO] Saldo creditado com sucesso.");
                }
                else
                {
                    Console.WriteLine($"❌ [FALHA PAGAMENTO] Core recusou: {result.Message}");
                }
            }
        }

        private async Task FetchAndSettleFromApi(List<string> matchIds, AppDbContext context, CoreWalletService walletService)
        {
            var idsString = string.Join(",", matchIds);
            var client = _httpClientFactory.CreateClient();
            var url = $"{BASE_URL}/v1/bet365/event?token={_betsApiToken}&FI={idsString}";

            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString };
                    var data = JsonSerializer.Deserialize<B365LiveResponse>(json, options);

                    if (data?.Results != null)
                    {
                        var allPackets = data.Results.SelectMany(l => l).ToList();
                        foreach (var ev in allPackets.Where(p => p.Type == "EV"))
                        {
                            if (ev.Status == "3" || ev.TimeStatus == "3")
                            {
                                var gameDb = await context.SportsEvents.FirstOrDefaultAsync(g => g.ExternalId == ev.Fi);
                                if (gameDb != null)
                                {
                                    gameDb.Status = "Ended";
                                    gameDb.Score = ev.Ss;

                                    // Processa apostas desse jogo recuperado
                                    var betsForGame = await context.BetSelections
                                        .Include(b => b.Bet)
                                        .Where(b => b.MatchId == ev.Fi && b.Status == "pending")
                                        .ToListAsync();

                                    foreach (var betSel in betsForGame)
                                    {
                                        ProcessSelectionResult(betSel, ev.Ss ?? "0-0");
                                        if (betSel.Bet != null) await CheckAndSettleTicketAsync(betSel.Bet, context, walletService);
                                    }
                                }
                            }
                        }
                        await context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [API RESGATE] Erro: {ex.Message}");
            }
        }
    }
}