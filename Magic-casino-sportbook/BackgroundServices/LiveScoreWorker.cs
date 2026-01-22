using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models; // Usa o novo B365PacketModels.cs
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;

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
            // 🔥 LOG ÚNICO PARA GARANTIR VERSÃO 🔥
            Console.WriteLine("\n\n-------------------------------------------------------------");
            Console.WriteLine("🏁 [VERSION CHECK] LiveScoreWorker: VERSÃO 3.0 (COMPILADO SEM DTO) 🏁");
            Console.WriteLine("-------------------------------------------------------------\n\n");

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
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
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

            // Agrupa IDs (lote pequeno de 5 para evitar 429 Too Many Requests)
            var matchIds = pendingSelections.Select(s => s.MatchId).Distinct().Take(5).ToList();
            var idsString = string.Join(",", matchIds);

            Console.WriteLine($"🔍 [SETTLEMENT] Verificando {matchIds.Count} jogos: {idsString}");

            var client = _httpClientFactory.CreateClient();
            var url = $"{BASE_URL}/v1/bet365/event?token={_betsApiToken}&FI={idsString}";

            try
            {
                var response = await client.GetAsync(url);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Console.WriteLine("⚠️ [SETTLEMENT] API Sobrecarregada (Erro 429). Pausando 10 segundos...");
                    await Task.Delay(10000);
                    return;
                }

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        NumberHandling = JsonNumberHandling.AllowReadingFromString,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };

                    // Agora usa a classe B365LiveResponse do arquivo corrigido
                    var data = JsonSerializer.Deserialize<B365LiveResponse>(json, options);

                    if (data?.Results != null)
                    {
                        // A API retorna uma lista de listas
                        var allPackets = data.Results.SelectMany(l => l).ToList();

                        // O modelo usa 'Fi' (que mapeia para FI no JSON)
                        var groupedGames = allPackets
                             .Where(p => !string.IsNullOrEmpty(p.Fi))
                             .GroupBy(p => p.Fi);

                        foreach (var gameGroup in groupedGames)
                        {
                            await ProcessGameSettlement(gameGroup.ToList(), context);
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
                Console.WriteLine($"❌ [SETTLEMENT] Erro HTTP ou Parse: {ex.Message}");
            }
        }

        private async Task ProcessGameSettlement(List<B365Packet> packets, AppDbContext context)
        {
            var ev = packets.FirstOrDefault(p => p.Type == "EV");

            if (ev == null) return;

            // Usa Ss (que mapeia para SS no JSON)
            if (string.IsNullOrEmpty(ev.Ss)) return;

            // --- LÓGICA DE FIM DE JOGO ---
            bool gameFinished = false;

            // Mapeamento correto: Tm (TM), Time (time)
            if (ev.Status == "3") gameFinished = true;
            if (ev.Tm == "FT" || ev.Time == "FT" || ev.Time == "Ended") gameFinished = true;

            if (!gameFinished) return;

            // --- FASE DE PAGAMENTO ---
            // Usa ev.Fi (FixtureId)
            var bets = await context.BetSelections
                .Where(b => b.MatchId == ev.Fi && b.Status == "pending")
                .ToListAsync();

            if (bets.Any())
            {
                Console.WriteLine($"🤑 [JOGO {ev.Fi}] Finalizado! Placar: {ev.Ss}. Processando {bets.Count} apostas...");
            }

            foreach (var bet in bets)
            {
                bet.FinalScore = ev.Ss;

                bool won = CheckWinner(bet.MarketName, bet.OutcomeName, ev.Ss);

                if (won)
                {
                    bet.Status = "Won";
                    bet.IsWinner = true;
                    Console.WriteLine($"✅ [GREEN] Aposta {bet.Id} (Apostou: {bet.OutcomeName}) Venceu!");
                }
                else
                {
                    bet.Status = "Lost";
                    bet.IsWinner = false;
                    Console.WriteLine($"❌ [RED] Aposta {bet.Id} (Apostou: {bet.OutcomeName}) Perdeu.");
                }
            }
        }

        private bool CheckWinner(string market, string outcome, string score)
        {
            try
            {
                var parts = score.Split('-');
                if (parts.Length < 2) return false;

                int homeScore = int.Parse(parts[0]);
                int awayScore = int.Parse(parts[1]);

                var marketName = market?.Trim();
                var selection = outcome?.Trim();

                if (marketName == "Resultado Final" || marketName == "Full Time Result" || marketName == "1X2")
                {
                    if (selection == "Casa" || selection == "1" || selection == "Home") return homeScore > awayScore;
                    if (selection == "Fora" || selection == "2" || selection == "Away") return awayScore > homeScore;
                    if (selection == "Empate" || selection == "X" || selection == "Draw") return homeScore == awayScore;
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