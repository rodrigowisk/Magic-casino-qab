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
            Console.WriteLine("🏁 [LIVE SCORE] WORKER GERAL: ATUALIZAÇÃO EM MASSA (10 MIN) 🏁");
            Console.WriteLine("-------------------------------------------------------------\n");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var walletService = scope.ServiceProvider.GetRequiredService<CoreWalletService>();

                        await UpdateAllActiveGamesAsync(context, walletService);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [WORKER ERRO]: {ex.Message}");
                }

                Console.WriteLine("💤 [WORKER] Aguardando 10 minutos para o próximo ciclo...");
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }

        private async Task UpdateAllActiveGamesAsync(AppDbContext context, CoreWalletService walletService)
        {
            // 1. Busca IDs de TODOS os jogos ativos (Live, WaitingLive, ou Prematch atrasados)
            var activeGamesIds = await context.SportsEvents
                .Where(g => g.Status == "Live" ||
                            g.Status == "WaitingLive" ||
                            (g.Status == "Prematch" && g.CommenceTime < DateTime.UtcNow))
                .Select(g => g.ExternalId)
                .ToListAsync();

            // 2. Busca IDs de jogos com apostas PENDENTES (Para pagar apostas antigas também)
            var pendingBetGameIds = await context.BetSelections
                .Where(s => s.Status == "pending")
                .Select(s => s.MatchId)
                .Distinct()
                .ToListAsync();

            // 3. Une as listas
            var allTargetIds = activeGamesIds
                .Union(pendingBetGameIds)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (!allTargetIds.Any()) return;

            Console.WriteLine($"🔍 [WORKER MASSIVO] Verificando status de {allTargetIds.Count} jogos na API...");

            // 4. Processa na API em lotes de 10
            if (!string.IsNullOrEmpty(_betsApiToken))
            {
                var batches = allTargetIds.Chunk(10);
                foreach (var batch in batches)
                {
                    await FetchAndSettleFromApi(batch.ToList(), context, walletService);
                    await Task.Delay(1500); // Rate limit gentil
                }
            }
        }

        private void ProcessSelectionResult(BetSelection selection, string score)
        {
            if (selection.Status != "pending") return;

            selection.FinalScore = score;

            // ✅ Lógica de verificação de vitória
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
                if (!int.TryParse(parts[0], out int homeScore) || !int.TryParse(parts[1], out int awayScore)) return false;

                var mkt = market?.ToLower().Trim() ?? "";
                var sel = outcome?.ToLower().Trim() ?? "";

                // 1X2 (Resultado Final)
                if (mkt.Contains("result") || mkt.Contains("vencedor") || mkt == "1x2" || mkt.Contains("match winner"))
                {
                    if (sel == "1") return homeScore > awayScore;
                    if (sel == "2") return awayScore > homeScore;
                    if (sel == "x" || sel == "draw" || sel == "empate") return homeScore == awayScore;
                }

                // Dupla Hipótese
                if (mkt.Contains("double") || mkt.Contains("dupla"))
                {
                    if (sel == "1x") return homeScore >= awayScore;
                    if (sel == "x2" || sel == "2x") return awayScore >= homeScore;
                    if (sel == "12") return homeScore != awayScore;
                }

                // Gols (Over/Under)
                if (mkt.Contains("goal") || mkt.Contains("gols") || mkt.Contains("over") || mkt.Contains("under"))
                {
                    int totalGols = homeScore + awayScore;
                    var match = Regex.Match(sel, @"(\d+(\.\d+)?)");
                    if (match.Success && double.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double line))
                    {
                        if (sel.Contains("over") || sel.Contains("mais")) return totalGols > line;
                        if (sel.Contains("under") || sel.Contains("menos")) return totalGols < line;
                    }
                }

                // BTTS (Ambos Marcam)
                if (mkt.Contains("both teams") || mkt.Contains("ambos"))
                {
                    bool bttsYes = homeScore > 0 && awayScore > 0;
                    if (sel == "sim" || sel == "yes") return bttsYes;
                    if (sel == "não" || sel == "no") return !bttsYes;
                }

                return false;
            }
            catch { return false; }
        }

        private async Task CheckAndSettleTicketAsync(Bet ticket, AppDbContext context, CoreWalletService walletService)
        {
            // Verifica todas as seleções desse bilhete
            var allSelections = await context.BetSelections.Where(s => s.BetId == ticket.Id).ToListAsync();

            if (allSelections.Any(s => s.Status == "pending")) return; // Ainda tem jogo rolando

            // Se qualquer uma perdeu, o bilhete perdeu
            if (allSelections.Any(s => s.Status == "Lost"))
            {
                if (ticket.Status != "Lost")
                {
                    ticket.Status = "Lost";
                    context.Entry(ticket).State = EntityState.Modified;
                    Console.WriteLine($"🎫 [BILHETE PERDIDO] ID: {ticket.Id}");
                }
                return;
            }

            // Se todas venceram, paga!
            if (allSelections.All(s => s.Status == "Won") && ticket.Status != "Won")
            {
                Console.WriteLine($"🏆 [BILHETE VENCEU] ID: {ticket.Id} -> PAGANDO R$ {ticket.PotentialReturn:N2}...");

                // 1. Credita na Carteira
                var result = await walletService.CreditFundsAsync(ticket.UserCpf, ticket.PotentialReturn);

                if (result.Success)
                {
                    ticket.Status = "Won";
                    context.Entry(ticket).State = EntityState.Modified;

                    // 2. 🔥 CORREÇÃO BLINDADA: INSERÇÃO DIRETA VIA SQL
                    // Isso evita o erro de compilação por falta do Model/DbSet no C#
                    // Mas garante que o registro apareça na tabela "transactions" do banco.
                    try
                    {
                        // Atenção: PostgreSQL usa aspas duplas para nomes de tabelas/colunas case-sensitive
                        var sql = "INSERT INTO \"transactions\" (\"user_cpf\", \"amount\", \"external_reference\", \"status\", \"created_at\", \"paid_at\") VALUES ({0}, {1}, {2}, {3}, {4}, {5})";

                        await context.Database.ExecuteSqlRawAsync(sql,
                            ticket.UserCpf,
                            ticket.PotentialReturn,
                            ticket.Id,
                            "paid",
                            DateTime.UtcNow,
                            DateTime.UtcNow
                        );

                        Console.WriteLine($"📝 [HISTÓRICO] Transação registrada via SQL direto para CPF {ticket.UserCpf}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ [ERRO HISTÓRICO] Não foi possível salvar transação: {ex.Message}");
                    }

                    Console.WriteLine($"✅ [PAGAMENTO EFETUADO] Saldo creditado e finalizado.");
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
                            // Status 3 = Ended
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

                                    // Busca apostas pendentes para este jogo
                                    var betsForGame = await context.BetSelections
                                        .Include(b => b.Bet)
                                        .Where(b => b.MatchId == res.Bet365Id && b.Status == "pending")
                                        .ToListAsync();

                                    foreach (var betSel in betsForGame)
                                    {
                                        ProcessSelectionResult(betSel, res.Score);
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
                Console.WriteLine($"❌ [API ERRO] Falha ao atualizar resultados: {ex.Message}");
            }
        }
    }
}