using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Events; // ✅ Necessário para BetWonEvent
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MassTransit; // ✅ Necessário para IPublishEndpoint

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
            Console.WriteLine("🏁 [LIVE SCORE] WORKER GERAL: ATUALIZAÇÃO EM MASSA (30 SEG) 🏁");
            Console.WriteLine("-------------------------------------------------------------\n");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // ✅ 1. Cria o Escopo
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var walletService = scope.ServiceProvider.GetRequiredService<CoreWalletService>();

                        // ✅ 2. Pega o RabbitMQ (PublishEndpoint) DENTRO do escopo
                        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                        // ✅ 3. Passa ele como parâmetro para o método de atualização
                        await UpdateAllActiveGamesAsync(context, walletService, publishEndpoint);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [WORKER ERRO]: {ex.Message}");
                }

                Console.WriteLine("💤 [WORKER] Aguardando 30 segundos para o próximo ciclo...");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        // 👇 Recebe publishEndpoint como parâmetro
        private async Task UpdateAllActiveGamesAsync(AppDbContext context, CoreWalletService walletService, IPublishEndpoint publishEndpoint)
        {
            var activeGamesIds = await context.SportsEvents
                .Where(g => g.Status == "Live" || g.Status == "WaitingLive" || (g.Status == "Prematch" && g.CommenceTime < DateTime.UtcNow))
                .Select(g => g.ExternalId).ToListAsync();

            var pendingBetGameIds = await context.BetSelections
                .Where(s => s.Status == "pending")
                .Select(s => s.MatchId).Distinct().ToListAsync();

            var allTargetIds = activeGamesIds.Union(pendingBetGameIds)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

            if (!allTargetIds.Any()) return;

            Console.WriteLine($"🔍 [WORKER MASSIVO] Verificando status de {allTargetIds.Count} jogos na API...");

            if (!string.IsNullOrEmpty(_betsApiToken))
            {
                var batches = allTargetIds.Chunk(10);
                foreach (var batch in batches)
                {
                    // 👇 Repassa o publishEndpoint
                    await FetchAndSettleFromApi(batch.ToList(), context, walletService, publishEndpoint);
                    await Task.Delay(1500);
                }
            }
        }

        private void ProcessSelectionResult(BetSelection selection, string score)
        {
            if (selection.Status != "pending") return;
            selection.FinalScore = score;
            bool won = CheckWinner(selection.MarketName, selection.OutcomeName, score);
            selection.Status = won ? "Won" : "Lost";
            selection.IsWinner = won;

            if (won) Console.WriteLine($"   ✅ Aposta VENCEDORA: {selection.MarketName} -> {selection.OutcomeName} (Placar: {score})");
            else Console.WriteLine($"   ❌ Aposta PERDIDA: {selection.MarketName} -> {selection.OutcomeName} (Placar: {score})");
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

        // 👇 Recebe publishEndpoint como parâmetro
        private async Task CheckAndSettleTicketAsync(Bet ticket, AppDbContext context, CoreWalletService walletService, IPublishEndpoint publishEndpoint)
        {
            var allSelections = await context.BetSelections.Where(s => s.BetId == ticket.Id).ToListAsync();

            if (allSelections.Any(s => s.Status == "pending")) return;

            if (allSelections.Any(s => s.Status == "Lost"))
            {
                if (ticket.Status != "Lost")
                {
                    ticket.Status = "Lost";
                    context.Entry(ticket).State = EntityState.Modified;
                }
                return;
            }

            if (allSelections.All(s => s.Status == "Won") && ticket.Status != "Won")
            {
                Console.WriteLine($"🏆 [BILHETE VENCEU] ID: {ticket.Id} - Enviando para pagamento...");

                ticket.Status = "Won";
                context.Entry(ticket).State = EntityState.Modified;

                // ✅ AGORA SIM: Usa a variável `publishEndpoint` que veio do parâmetro (sem underline)
                await publishEndpoint.Publish(new BetWonEvent
                {
                    BetId = ticket.Id,
                    UserCpf = ticket.UserCpf,
                    PayoutAmount = ticket.PotentialReturn,
                    WonAt = DateTime.UtcNow
                });
            }
        }

        // 👇 Recebe publishEndpoint como parâmetro
        private async Task FetchAndSettleFromApi(List<string> matchIds, AppDbContext context, CoreWalletService walletService, IPublishEndpoint publishEndpoint)
        {
            var idsString = string.Join(",", matchIds);
            var client = _httpClientFactory.CreateClient();
            var url = $"{BASE_URL}/v1/bet365/result?token={_betsApiToken}&event_id={idsString}";

            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString };
                    var data = JsonSerializer.Deserialize<B365ResultResponse>(json, options);

                    if (data?.Results != null)
                    {
                        foreach (var res in data.Results)
                        {
                            if (res.TimeStatus == "3" && !string.IsNullOrEmpty(res.Score))
                            {
                                var gameDb = await context.SportsEvents.FirstOrDefaultAsync(g => g.ExternalId == res.Bet365Id);

                                if (gameDb != null)
                                {
                                    if (gameDb.Status != "Ended")
                                    {
                                        Console.WriteLine($"🏁 [FIM DETECTADO] {gameDb.HomeTeam} vs {gameDb.AwayTeam} -> Ended ({res.Score})");
                                        gameDb.Status = "Ended";
                                        gameDb.Score = res.Score;
                                        gameDb.GameTime = "FT";
                                        context.Entry(gameDb).State = EntityState.Modified;
                                    }

                                    var betsForGame = await context.BetSelections
                                        .Include(b => b.Bet)
                                        .Where(b => b.MatchId == res.Bet365Id && b.Status == "pending")
                                        .ToListAsync();

                                    foreach (var betSel in betsForGame)
                                    {
                                        ProcessSelectionResult(betSel, res.Score);
                                        if (betSel.Bet != null)
                                            // 👇 Repassa o publishEndpoint
                                            await CheckAndSettleTicketAsync(betSel.Bet, context, walletService, publishEndpoint);
                                    }
                                }
                            }
                        }
                    }
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [API ERRO] Falha ao conectar na BetsAPI: {ex.Message}");
            }
        }
    }
}