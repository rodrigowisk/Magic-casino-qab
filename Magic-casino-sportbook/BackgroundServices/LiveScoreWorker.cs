using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Services; // Adicionado para acessar CoreWalletService
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            Console.WriteLine("🏁 [LIVE SCORE] WORKER 5.0: PAGAMENTO QAB AUTOMÁTICO 🏁");
            Console.WriteLine("-------------------------------------------------------------\n");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        // ✅ Injetamos o serviço de carteira para fazer o pagamento
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
            // 1. Busca apostas pendentes + Dados do Bilhete Pai
            var pendingSelections = await context.BetSelections
                .Include(s => s.Bet)
                .Where(s => s.Status == "pending")
                .ToListAsync();

            if (!pendingSelections.Any()) return;

            var matchIds = pendingSelections.Select(b => b.MatchId).Distinct().ToList();

            // 2. Busca jogos no banco local
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
                    // Verifica se o jogo acabou
                    if ((game.Status == "Ended" || game.Status == "3") && !string.IsNullOrEmpty(game.Score) && game.Score != "0-0")
                    {
                        Console.WriteLine($"🔍 [JOGO FINALIZADO] ID: {game.ExternalId} | Placar: {game.Score}");

                        // Atualiza status da SELEÇÃO
                        ProcessSelectionResult(selection, game.Score);

                        // Verifica status do BILHETE (Paga se ganhou)
                        if (selection.Bet != null)
                        {
                            await CheckAndSettleTicketAsync(selection.Bet, context, walletService);
                        }

                        dbChanged = true;
                    }
                    // Fallback para jogos velhos travados
                    else if (game.CommenceTime < DateTime.UtcNow.AddHours(-5))
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

            // 4. Resgate na API se necessário
            if (gamesToForceUpdate.Any() && !string.IsNullOrEmpty(_betsApiToken))
            {
                var batches = gamesToForceUpdate.Chunk(10);
                foreach (var batch in batches)
                {
                    await FetchAndSettleFromApi(batch.ToList(), context, walletService);
                }
            }
        }

        // --- LÓGICA DO BILHETE E PAGAMENTO ---
        private async Task CheckAndSettleTicketAsync(Bet ticket, AppDbContext context, CoreWalletService walletService)
        {
            // Recarrega todas as seleções desse bilhete para ter certeza
            var allSelections = await context.BetSelections
                .Where(s => s.BetId == ticket.Id)
                .ToListAsync();

            // Se ainda tem jogo rolando, não faz nada
            if (allSelections.Any(s => s.Status == "pending")) return;

            // Se alguma perdeu, o bilhete perdeu
            if (allSelections.Any(s => s.Status == "Lost"))
            {
                if (ticket.Status != "Lost")
                {
                    ticket.Status = "Lost";
                    Console.WriteLine($"🎫 [TICKET PERDIDO] ID: {ticket.Id} | CPF: {ticket.UserCpf}");
                }
                return;
            }

            // Se todas ganharam e o bilhete ainda não foi pago
            if (allSelections.All(s => s.Status == "Won") && ticket.Status != "Won")
            {
                Console.WriteLine($"🏆 [VITÓRIA] Ticket {ticket.Id} VENCEU! Pagando R$ {ticket.PotentialReturn}...");

                // 💰 PAGAMENTO REAL NA CARTEIRA QAB
                var result = await walletService.CreditFundsAsync(ticket.UserCpf, ticket.PotentialReturn);

                if (result.Success)
                {
                    ticket.Status = "Won";
                    Console.WriteLine($"✅ [PAGAMENTO SUCESSO] Saldo creditado para {ticket.UserCpf}");
                }
                else
                {
                    Console.WriteLine($"❌ [ERRO PAGAMENTO] Core recusou: {result.Message}");
                    // Não marcamos como 'Won' no banco para tentar pagar de novo no próximo ciclo
                }
            }
        }

        // --- MÉTODOS AUXILIARES ---
        private void ProcessSelectionResult(BetSelection selection, string score)
        {
            selection.FinalScore = score;
            bool won = CheckWinner(selection.MarketName, selection.OutcomeName, score);
            selection.Status = won ? "Won" : "Lost";
            selection.IsWinner = won;
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

        private bool CheckWinner(string market, string outcome, string score)
        {
            try
            {
                if (string.IsNullOrEmpty(score) || !score.Contains("-")) return false;
                var parts = score.Split('-');
                if (!int.TryParse(parts[0], out int home) || !int.TryParse(parts[1], out int away)) return false;

                var mkt = market?.ToLower().Trim();
                var sel = outcome?.ToLower().Trim();

                if (mkt.Contains("result") || mkt.Contains("vencedor") || mkt == "1x2")
                {
                    if (sel == "1" || sel.Contains("home") || sel.Contains("casa")) return home > away;
                    if (sel == "2" || sel.Contains("away") || sel.Contains("fora")) return away > home;
                    if (sel == "x" || sel.Contains("draw") || sel.Contains("empate")) return home == away;
                }

                // Lógica simples de gols (Over/Under seria mais complexo e requer o valor da linha)

                return false;
            }
            catch { return false; }
        }
    }
}