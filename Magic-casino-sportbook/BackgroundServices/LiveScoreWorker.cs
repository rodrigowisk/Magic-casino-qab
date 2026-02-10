using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Events; // ✅ Necessário para GameEndedEvent e BetWonEvent
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
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly BetsApiGatekeeper _gatekeeper; // ✅ NOVO: Gatekeeper adicionado

        public LiveScoreWorker(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory, BetsApiGatekeeper gatekeeper)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
            _gatekeeper = gatekeeper; // ✅ NOVO: Injeção do Gatekeeper
            _betsApiToken = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";

            // ✅ JSON Permissivo para evitar erros de tipos (string/int)
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("\n-------------------------------------------------------------");
            Console.WriteLine("🏁 [LIVE SCORE] WORKER GERAL: ATUALIZAÇÃO EM MASSA (10 SEG) 🏁");
            Console.WriteLine("-------------------------------------------------------------\n");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var walletService = scope.ServiceProvider.GetRequiredService<CoreWalletService>();

                        // O RabbitMQ está aqui
                        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                        await UpdateAllActiveGamesAsync(context, walletService, publishEndpoint);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [WORKER ERRO]: {ex.Message}");
                }

                Console.WriteLine("💤 [WORKER] Aguardando 10 segundos para o próximo ciclo...");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

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
                    await FetchAndSettleFromApi(batch.ToList(), context, walletService, publishEndpoint);
                    await Task.Delay(1500);
                }
            }
        }

        private async Task FetchAndSettleFromApi(List<string> matchIds, AppDbContext context, CoreWalletService walletService, IPublishEndpoint publishEndpoint)
        {
            var idsString = string.Join(",", matchIds);
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20); // ✅ NOVO: Timeout ajustado para 20s
            var url = $"{BASE_URL}/v1/bet365/result?token={_betsApiToken}&event_id={idsString}";

            try
            {
                // ✅ NOVO: Espera na Fila VIP (Prioridade para o Ao Vivo)
                await _gatekeeper.WaitAsync(isLivePriority: true);

                var response = await client.GetAsync(url);

                // Proteção Anti-429
                if ((int)response.StatusCode == 429)
                {
                    Console.WriteLine("⛔ [RESULT LIMIT] 429 Detectado. Pausando 5s...");
                    await Task.Delay(5000);
                    return;
                }

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<B365ResultResponse>(json, _jsonOptions);

                    if (data?.Results != null)
                    {
                        foreach (var res in data.Results)
                        {
                            // ✅ 1. EXTRAÇÃO DE ESTATÍSTICAS (RODA PARA TODOS OS JOGOS DO BATCH)
                            if (res.Stats.HasValue && res.Stats.Value.ValueKind == JsonValueKind.Object)
                            {
                                await ProcessLiveStats(context, res.ActualId, res.Stats.Value, res.Score, res.TimeStatus);
                            }

                            // ✅ 2. LÓGICA ORIGINAL DE ENCERRAMENTO
                            if (res.TimeStatus == "3" && !string.IsNullOrEmpty(res.Score))
                            {
                                var gameDb = await context.SportsEvents.FirstOrDefaultAsync(g => g.ExternalId == res.ActualId);

                                if (gameDb != null)
                                {
                                    // DETECTOU QUE O JOGO ACABOU AGORA
                                    if (gameDb.Status != "Ended")
                                    {
                                        Console.WriteLine($"🏁 [FIM DETECTADO] {gameDb.HomeTeam} vs {gameDb.AwayTeam} -> Ended ({res.Score})");

                                        // 1. Atualiza no Banco Local
                                        gameDb.Status = "Ended";
                                        gameDb.Score = res.Score;
                                        gameDb.GameTime = "FT";
                                        context.Entry(gameDb).State = EntityState.Modified;

                                        // 🚀 2. PUBLICA O EVENTO PARA O MÓDULO DE TORNEIOS (RABBITMQ)
                                        // Isso avisa o TournamentWorker que o jogo acabou, sem acoplamento.
                                        await publishEndpoint.Publish(new GameEndedEvent
                                        {
                                            GameId = gameDb.ExternalId,
                                            HomeTeam = gameDb.HomeTeam,
                                            AwayTeam = gameDb.AwayTeam,
                                            Score = res.Score,
                                            SportKey = gameDb.SportKey,
                                            EndedAt = DateTime.UtcNow
                                        });
                                        Console.WriteLine($"🐰 [RABBITMQ] Evento de Fim de Jogo Publicado: {gameDb.ExternalId}");
                                    }

                                    // Processa apostas simples (Sportbook)
                                    var betsForGame = await context.BetSelections
                                        .Include(b => b.Bet)
                                        .Where(b => b.MatchId == res.ActualId && b.Status == "pending")
                                        .ToListAsync();

                                    foreach (var betSel in betsForGame)
                                    {
                                        ProcessSelectionResult(betSel, res.Score);
                                        if (betSel.Bet != null)
                                            await CheckAndSettleTicketAsync(betSel.Bet, context, walletService, publishEndpoint);
                                    }
                                }
                            }
                        }
                    }
                }
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [API ERRO] Falha ao conectar na BetsAPI: {ex.Message}");
            }
        }

        // ==============================================================================
        // 📊 PARSER DE ESTATÍSTICAS (NOVO CÓDIGO)
        // ==============================================================================
        private async Task ProcessLiveStats(AppDbContext context, string gameId, JsonElement statsNode, string score, string timeStatus)
        {
            var statDb = await context.Set<LiveGameStat>().FirstOrDefaultAsync(s => s.GameId == gameId);
            bool isNew = false;

            if (statDb == null)
            {
                statDb = new LiveGameStat { GameId = gameId };
                isNew = true;
            }

            if (!string.IsNullOrEmpty(score) && score.Contains("-"))
            {
                var p = score.Split('-');
                if (int.TryParse(p[0], out int h)) statDb.HomeScore = h;
                if (int.TryParse(p[1], out int a)) statDb.AwayScore = a;
            }

            statDb.IsFinished = (timeStatus == "3" || timeStatus == "4");

            // Extração cega: Se não existir no JSON, retorna 0 com segurança.
            statDb.HomeCorners = GetStatValueSafe(statsNode, "corners", 0);
            statDb.AwayCorners = GetStatValueSafe(statsNode, "corners", 1);

            statDb.HomeYellowCards = GetStatValueSafe(statsNode, "yellowcards", 0);
            statDb.AwayYellowCards = GetStatValueSafe(statsNode, "yellowcards", 1);

            statDb.HomeRedCards = GetStatValueSafe(statsNode, "redcards", 0);
            statDb.AwayRedCards = GetStatValueSafe(statsNode, "redcards", 1);

            statDb.HomeDangerousAttacks = GetStatValueSafe(statsNode, "dangerous_attacks", 0);
            statDb.AwayDangerousAttacks = GetStatValueSafe(statsNode, "dangerous_attacks", 1);

            statDb.HomeAttacks = GetStatValueSafe(statsNode, "attacks", 0);
            statDb.AwayAttacks = GetStatValueSafe(statsNode, "attacks", 1);

            statDb.HomePossession = GetStatValueSafe(statsNode, "possession_rt", 0);
            statDb.AwayPossession = GetStatValueSafe(statsNode, "possession_rt", 1);

            statDb.HomeOnTarget = GetStatValueSafe(statsNode, "on_target", 0);
            statDb.AwayOnTarget = GetStatValueSafe(statsNode, "on_target", 1);

            statDb.HomeOffTarget = GetStatValueSafe(statsNode, "off_target", 0);
            statDb.AwayOffTarget = GetStatValueSafe(statsNode, "off_target", 1);

            statDb.LastUpdated = DateTime.UtcNow;

            if (isNew) context.Set<LiveGameStat>().Add(statDb);
            else context.Entry(statDb).State = EntityState.Modified;
        }

        private int GetStatValueSafe(JsonElement node, string key, int index)
        {
            try
            {
                if (node.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    if (arr.GetArrayLength() > index)
                    {
                        var valStr = arr[index].GetString();
                        if (int.TryParse(valStr, out int val)) return val;
                    }
                }
            }
            catch { }
            return 0;
        }

        // ==============================================================================
        // LÓGICA DE RESOLUÇÃO DE APOSTAS (MANTIDA ORIGINAL)
        // ==============================================================================
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

                await publishEndpoint.Publish(new BetWonEvent
                {
                    BetId = ticket.Id,
                    UserCpf = ticket.UserCpf,
                    PayoutAmount = ticket.PotentialReturn,
                    WonAt = DateTime.UtcNow
                });
            }
        }
    }

    public class B365ResultResponse
    {
        [JsonPropertyName("success")]
        public int Success { get; set; }

        [JsonPropertyName("results")]
        public List<B365ResultItem> Results { get; set; }
    }

    public class B365ResultItem
    {
        [JsonPropertyName("bet365_id")]
        public string Bet365Id { get; set; }

        [JsonPropertyName("id")]
        public string FallbackId { get; set; }
        public string ActualId => !string.IsNullOrEmpty(Bet365Id) ? Bet365Id : FallbackId;

        [JsonPropertyName("ss")]
        public string Score { get; set; }

        [JsonPropertyName("time_status")]
        public string TimeStatus { get; set; }

        [JsonPropertyName("stats")]
        public JsonElement? Stats { get; set; }
    }
}