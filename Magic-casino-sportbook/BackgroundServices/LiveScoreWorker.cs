using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.DTOs.Live; // Garante que usa o DTO correto
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
            Console.WriteLine("💰 [SETTLEMENT] Worker de Pagamentos Iniciado.");

            if (string.IsNullOrEmpty(_betsApiToken))
            {
                Console.WriteLine("⚠️ [SETTLEMENT] AVISO: Token não encontrado!");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await SettleBetsAsync(context);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [SETTLEMENT] Erro Crítico: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task SettleBetsAsync(AppDbContext context)
        {
            if (string.IsNullOrEmpty(_betsApiToken)) return;

            var pendingSelections = await context.BetSelections
                .Where(s => s.Status == "pending")
                .ToListAsync();

            if (!pendingSelections.Any()) return;

            // Agrupa IDs
            var matchIds = pendingSelections.Select(s => s.MatchId).Distinct().Take(10).ToList();
            var idsString = string.Join(",", matchIds);

            // Log para confirmar quais jogos estamos verificando
            Console.WriteLine($"🔍 [SETTLEMENT] Verificando {matchIds.Count} jogos: {idsString}");

            var client = _httpClientFactory.CreateClient();
            var url = $"{BASE_URL}/v1/bet365/event?token={_betsApiToken}&FI={idsString}";

            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    // Log do tamanho do JSON para garantir que não veio vazio
                    // Console.WriteLine($"📦 [SETTLEMENT] JSON recebido ({json.Length} bytes)"); 

                    var data = JsonSerializer.Deserialize<B365LiveResponse>(json);

                    if (data?.Results != null)
                    {
                        foreach (var gamePackets in data.Results)
                        {
                            await ProcessGameSettlement(gamePackets, context);
                        }
                    }
                    await context.SaveChangesAsync();
                }
                else
                {
                    Console.WriteLine($"⚠️ [SETTLEMENT] Erro na API: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [SETTLEMENT] Erro HTTP: {ex.Message}");
            }
        }

        private async Task ProcessGameSettlement(List<B365Packet> packets, AppDbContext context)
        {
            var ev = packets.FirstOrDefault(p => p.Type == "EV");

            if (ev == null) return;

            // 🔥 LOG DETETIVE: Mostra exatamente o que o C# está lendo do JSON
            Console.WriteLine($"🕵️ [JOGO {ev.FixtureId}] Status='{ev.Status}' | Tempo='{ev.Time}' | Placar='{ev.ScoreString}'");

            if (string.IsNullOrEmpty(ev.ScoreString)) return;

            // --- LÓGICA DE FIM DE JOGO ---
            bool gameFinished = false;

            if (ev.Status == "3") gameFinished = true;
            if (ev.Time == "FT" || ev.Time == "Ended") gameFinished = true;

            // Lógica Ninja (Status 0 + Tempo 90)
            if (!gameFinished && ev.Time == "90" && ev.Status == "0" && ev.ScoreString != "0-0")
            {
                gameFinished = true;
                Console.WriteLine($"🥷 [JOGO {ev.FixtureId}] Ativando Lógica Ninja (Status 0 mas Tempo 90)");
            }

            if (!gameFinished)
            {
                // Console.WriteLine($"⏳ [JOGO {ev.FixtureId}] Ainda rolando. Ignorando.");
                return;
            }

            // --- FASE DE PAGAMENTO ---
            var bets = await context.BetSelections
                .Where(b => b.MatchId == ev.FixtureId && b.Status == "pending")
                .ToListAsync();

            Console.WriteLine($"🤑 [JOGO {ev.FixtureId}] Finalizado! Processando {bets.Count} apostas...");

            foreach (var bet in bets)
            {
                bet.FinalScore = ev.ScoreString;

                bool won = CheckWinner(bet.MarketName, bet.OutcomeName, ev.ScoreString);

                if (won)
                {
                    bet.Status = "Won";
                    bet.IsWinner = true;
                    Console.WriteLine($"✅ [GREEN] Aposta {bet.Id} (Apostou: {bet.OutcomeName}) Venceu! Placar: {ev.ScoreString}");
                }
                else
                {
                    bet.Status = "Lost";
                    bet.IsWinner = false;
                    Console.WriteLine($"❌ [RED] Aposta {bet.Id} (Apostou: {bet.OutcomeName}) Perdeu. Placar: {ev.ScoreString}");
                }
            }
        }

        private bool CheckWinner(string market, string outcome, string score)
        {
            try
            {
                var parts = score.Split('-');
                int homeScore = int.Parse(parts[0]);
                int awayScore = int.Parse(parts[1]);

                var marketName = market?.Trim();
                var selection = outcome?.Trim();

                if (marketName == "Resultado Final" || marketName == "Full Time Result")
                {
                    if (selection == "Casa" || selection == "1") return homeScore > awayScore;
                    if (selection == "Fora" || selection == "2") return awayScore > homeScore;
                    if (selection == "Empate" || selection == "X") return homeScore == awayScore;
                }

                if (marketName == "Ambos Marcam" || marketName == "Both Teams to Score")
                {
                    if (selection == "Sim" || selection == "Yes") return homeScore > 0 && awayScore > 0;
                    if (selection == "Não" || selection == "No") return homeScore == 0 || awayScore == 0;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}