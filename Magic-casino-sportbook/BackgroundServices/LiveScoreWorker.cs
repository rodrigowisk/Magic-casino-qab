using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
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
            Console.WriteLine("\n-------------------------------------------------------------");
            Console.WriteLine("🏁 [VERSION CHECK] LiveScoreWorker: VERSÃO 4.0 (PRODUTOR-CONSUMIDOR) 🏁");
            Console.WriteLine("-------------------------------------------------------------\n");

            Console.WriteLine("💰 [SETTLEMENT] Worker de Pagamentos Iniciado (Modo Econômico).");

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

                // Verifica a cada 45 segundos
                await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
            }
        }

        private async Task SettleBetsAsync(AppDbContext context)
        {
            // 1. Busca todas as apostas pendentes no banco
            var pendingBets = await context.BetSelections
                .Where(s => s.Status == "pending")
                .ToListAsync();

            if (!pendingBets.Any()) return;

            // 2. Coleta os IDs dos jogos dessas apostas
            var matchIds = pendingBets.Select(b => b.MatchId).Distinct().ToList();

            // 3. ESTRATÉGIA DE OURO: Busca os dados dos jogos no SEU BANCO LOCAL
            // Não gastamos API aqui. Confiamos que o LiveSportService já atualizou o banco.
            var dbGames = await context.SportsEvents
                .Where(g => matchIds.Contains(g.ExternalId))
                .ToListAsync();

            var gamesMap = dbGames.ToDictionary(g => g.ExternalId);
            var gamesToForceUpdate = new List<string>();
            bool dbChanged = false;

            // 4. Processamento Local (Custo Zero)
            foreach (var bet in pendingBets)
            {
                if (gamesMap.TryGetValue(bet.MatchId, out var game))
                {
                    // CENÁRIO A: O jogo já está marcado como encerrado no banco (Sucesso!)
                    if (game.Status == "Ended" && !string.IsNullOrEmpty(game.Score) && game.Score != "0-0")
                    {
                        Console.WriteLine($"💰 [PAGAMENTO] Processando aposta {bet.Id} localmente (Jogo {game.HomeTeam} Encerrado: {game.Score}).");
                        ProcessBetResult(bet, game.Score);
                        dbChanged = true;
                        continue;
                    }

                    // CENÁRIO B: Rede de Segurança (Fallback)
                    // Se o jogo começou há mais de 5 horas e ainda está "Live", o robô principal pode ter falhado.
                    // Adiciona na lista para chamar a API.
                    if (game.CommenceTime < DateTime.UtcNow.AddHours(-5))
                    {
                        if (!gamesToForceUpdate.Contains(game.ExternalId))
                            gamesToForceUpdate.Add(game.ExternalId);
                    }
                }
                else
                {
                    // Jogo não existe no banco (Muito raro): Adiciona para buscar na API
                    if (!gamesToForceUpdate.Contains(bet.MatchId))
                        gamesToForceUpdate.Add(bet.MatchId);
                }
            }

            if (dbChanged) await context.SaveChangesAsync();

            // 5. Chamada de Emergência (Só executa se tiver jogo "travado" ou sumido)
            if (gamesToForceUpdate.Any() && !string.IsNullOrEmpty(_betsApiToken))
            {
                Console.WriteLine($"⚠️ [RESGATE] Buscando {gamesToForceUpdate.Count} jogos antigos/travados na API...");
                // Processa em lotes de 10 para não estourar a API
                var batches = gamesToForceUpdate.Chunk(10);
                foreach (var batch in batches)
                {
                    await FetchAndSettleFromApi(batch.ToList(), context);
                }
            }
        }

        private void ProcessBetResult(BetSelection bet, string score)
        {
            bet.FinalScore = score;
            bool won = CheckWinner(bet.MarketName, bet.OutcomeName, score);

            if (won)
            {
                bet.Status = "Won";
                bet.IsWinner = true;
                Console.WriteLine($"✅ [GREEN] Aposta {bet.Id} Venceu! (Placar: {score})");
                // TODO: AQUI VOCÊ DEVE CHAMAR O SERVIÇO PARA CREDITAR O SALDO DO USUÁRIO
            }
            else
            {
                bet.Status = "Lost";
                bet.IsWinner = false;
                Console.WriteLine($"❌ [RED] Aposta {bet.Id} Perdeu. (Placar: {score})");
            }
        }

        private async Task FetchAndSettleFromApi(List<string> matchIds, AppDbContext context)
        {
            var idsString = string.Join(",", matchIds);
            var client = _httpClientFactory.CreateClient();
            var url = $"{BASE_URL}/v1/bet365/event?token={_betsApiToken}&FI={idsString}";

            try
            {
                var response = await client.GetAsync(url);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Console.WriteLine("⚠️ [API] 429 Too Many Requests. Pulando resgate.");
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

                    var data = JsonSerializer.Deserialize<B365LiveResponse>(json, options);

                    if (data?.Results != null)
                    {
                        var allPackets = data.Results.SelectMany(l => l).ToList();
                        var groupedGames = allPackets.Where(p => !string.IsNullOrEmpty(p.Fi)).GroupBy(p => p.Fi);

                        foreach (var gameGroup in groupedGames)
                        {
                            await ProcessGameSettlement(gameGroup.ToList(), context);
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

        private async Task ProcessGameSettlement(List<B365Packet> packets, AppDbContext context)
        {
            var ev = packets.FirstOrDefault(p => p.Type == "EV");
            if (ev == null || string.IsNullOrEmpty(ev.Ss)) return;

            // --- DETECÇÃO DE FIM DE JOGO ---
            bool gameFinished = false;
            if (ev.Status == "3") gameFinished = true;
            if (ev.Tm == "FT" || ev.Time == "FT" || ev.Time == "Ended") gameFinished = true;

            // --- FORÇA ENCERRAMENTO NO BANCO (Se a API diz que acabou) ---
            if (gameFinished)
            {
                var gameDb = await context.SportsEvents.FirstOrDefaultAsync(g => g.ExternalId == ev.Fi);
                if (gameDb != null && gameDb.Status != "Ended")
                {
                    Console.WriteLine($"🏁 [RESGATE] Forçando encerramento do jogo {gameDb.HomeTeam} (ID: {ev.Fi})");
                    gameDb.Status = "Ended";
                    gameDb.Score = ev.Ss; // Atualiza o placar final também
                }
            }

            // Só paga se acabou
            if (!gameFinished) return;

            var bets = await context.BetSelections
                .Where(b => b.MatchId == ev.Fi && b.Status == "pending")
                .ToListAsync();

            foreach (var bet in bets)
            {
                ProcessBetResult(bet, ev.Ss);
            }
        }

        private bool CheckWinner(string market, string outcome, string score)
        {
            try
            {
                var parts = score.Split('-');
                if (parts.Length < 2) return false;

                // Tenta parsear como inteiros (Futebol, Basquete, etc)
                if (!int.TryParse(parts[0], out int homeScore) || !int.TryParse(parts[1], out int awayScore))
                {
                    // Se falhar (ex: Tênis pode ter formato diferente), retorna falso por segurança
                    return false;
                }

                var marketName = market?.Trim();
                var selection = outcome?.Trim();

                if (marketName == "Resultado Final" || marketName == "Full Time Result" || marketName == "1X2" || marketName == "Vencedor")
                {
                    if (selection == "Casa" || selection == "1" || selection == "Home" || selection.Contains("1")) return homeScore > awayScore;
                    if (selection == "Fora" || selection == "2" || selection == "Away" || selection.Contains("2")) return awayScore > homeScore;
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