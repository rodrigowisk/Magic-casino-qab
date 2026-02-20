using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Events;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Services.Gateways;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class LiveScoreWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly BetsApiHttpService _api;
        private readonly string _betsApiToken;
        private const string BASE_URL = "https://api.b365api.com";
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly BetsApiGatekeeper _gatekeeper;
        private readonly IConnectionMultiplexer _redis;

        public LiveScoreWorker(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory, BetsApiHttpService api, BetsApiGatekeeper gatekeeper, IConnectionMultiplexer redis)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
            _gatekeeper = gatekeeper;
            _redis = redis;
            _api = api;
            _betsApiToken = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";

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
            Console.WriteLine("🏁 [LIVE SCORE] WORKER GERAL: ATUALIZAÇÃO VIA RABBITMQ (10 SEG) 🏁");
            Console.WriteLine("-------------------------------------------------------------\n");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var walletService = scope.ServiceProvider.GetRequiredService<CoreWalletService>();
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

            Console.WriteLine($"🔍 [WORKER] Atualizando {allTargetIds.Count} jogos...");

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
            var url = $"/v1/bet365/result?event_id={idsString}";

            try
            {
                await _gatekeeper.WaitAsync(isLivePriority: true);
                var response = await _api.GetAsync(url, isLivePriority: true);

                if (response == null) return;

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
                            var gameDb = await context.SportsEvents
                                .AsNoTracking()
                                .FirstOrDefaultAsync(g => g.ExternalId == res.ActualId);

                            bool hasStats = res.Stats.HasValue && res.Stats.Value.ValueKind == JsonValueKind.Object;

                            var currentStats = await ProcessAndReturnLiveStats(context, res.ActualId, res.Stats, res.Score, res.TimeStatus);

                            if (currentStats != null)
                            {
                                await publishEndpoint.Publish(new LiveGameUpdatedEvent
                                {
                                    GameId = res.ActualId,
                                    Score = res.Score,

                                    Status = "Live",
                                    LastUpdate = DateTime.UtcNow,
                                    GameTime = null,
                                    RawOddsHome = null,
                                    RawOddsDraw = null,
                                    RawOddsAway = null,
                                    HomeScore = currentStats.HomeScore,
                                    AwayScore = currentStats.AwayScore,
                                    HomeCorners = currentStats.HomeCorners,
                                    AwayCorners = currentStats.AwayCorners,
                                    HomeYellowCards = hasStats ? currentStats.HomeYellowCards : null,
                                    AwayYellowCards = hasStats ? currentStats.AwayYellowCards : null,
                                    HomeRedCards = hasStats ? currentStats.HomeRedCards : null,
                                    AwayRedCards = hasStats ? currentStats.AwayRedCards : null,
                                    HomeAttacks = currentStats.HomeAttacks,
                                    AwayAttacks = currentStats.AwayAttacks,
                                    HomeDangerousAttacks = currentStats.HomeDangerousAttacks,
                                    AwayDangerousAttacks = currentStats.AwayDangerousAttacks,
                                    HomeOnTarget = currentStats.HomeOnTarget,
                                    AwayOnTarget = currentStats.AwayOnTarget,
                                    HomeOffTarget = currentStats.HomeOffTarget,
                                    AwayOffTarget = currentStats.AwayOffTarget
                                });
                            }

                            if (res.TimeStatus == "3" && !string.IsNullOrEmpty(res.Score))
                            {
                                var gameToEdit = await context.SportsEvents.FirstOrDefaultAsync(g => g.ExternalId == res.ActualId);

                                if (gameToEdit != null)
                                {
                                    if (gameToEdit.Status != "Ended")
                                    {
                                        Console.WriteLine($"🏁 [FIM DETECTADO] {gameToEdit.HomeTeam} vs {gameToEdit.AwayTeam} -> Ended ({res.Score})");
                                        gameToEdit.Status = "Ended";
                                        gameToEdit.Score = res.Score;
                                        gameToEdit.GameTime = "FT";
                                        context.Entry(gameToEdit).State = EntityState.Modified;

                                        try
                                        {
                                            var dbRedis = _redis.GetDatabase();
                                            await dbRedis.KeyDeleteAsync($"live_game:{gameToEdit.ExternalId}");
                                            await dbRedis.SetRemoveAsync("dirty_live_games", gameToEdit.ExternalId);
                                            Console.WriteLine($"🧹 [REDIS] Jogo {gameToEdit.ExternalId} removido do cache.");
                                        }
                                        catch (Exception exRedis) { Console.WriteLine($"⚠️ Erro Redis: {exRedis.Message}"); }

                                        await publishEndpoint.Publish(new GameEndedEvent
                                        {
                                            GameId = gameToEdit.ExternalId,
                                            HomeTeam = gameToEdit.HomeTeam,
                                            AwayTeam = gameToEdit.AwayTeam,
                                            Score = res.Score,
                                            SportKey = gameToEdit.SportKey,
                                            EndedAt = DateTime.UtcNow
                                        });
                                    }

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

        private async Task<LiveGameStat> ProcessAndReturnLiveStats(AppDbContext context, string gameId, JsonElement? statsNode, string score, string timeStatus)
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

            // ✅ FIX: Verifica se é nulo antes de usar
            if (statsNode.HasValue && statsNode.Value.ValueKind == JsonValueKind.Object)
            {
                // Converte para JsonElement não-nulo
                JsonElement node = statsNode.Value;

                // Passa o objeto seguro para o método auxiliar
                statDb.HomeCorners = GetStatValueSafe(node, "corners", 0);
                statDb.AwayCorners = GetStatValueSafe(node, "corners", 1);
                statDb.HomeYellowCards = GetStatValueSafe(node, "yellowcards", 0);
                statDb.AwayYellowCards = GetStatValueSafe(node, "yellowcards", 1);
                statDb.HomeRedCards = GetStatValueSafe(node, "redcards", 0);
                statDb.AwayRedCards = GetStatValueSafe(node, "redcards", 1);
                statDb.HomeDangerousAttacks = GetStatValueSafe(node, "dangerous_attacks", 0);
                statDb.AwayDangerousAttacks = GetStatValueSafe(node, "dangerous_attacks", 1);
                statDb.HomeAttacks = GetStatValueSafe(node, "attacks", 0);
                statDb.AwayAttacks = GetStatValueSafe(node, "attacks", 1);
                statDb.HomePossession = GetStatValueSafe(node, "possession_rt", 0);
                statDb.AwayPossession = GetStatValueSafe(node, "possession_rt", 1);
                statDb.HomeOnTarget = GetStatValueSafe(node, "on_target", 0);
                statDb.AwayOnTarget = GetStatValueSafe(node, "on_target", 1);
                statDb.HomeOffTarget = GetStatValueSafe(node, "off_target", 0);
                statDb.AwayOffTarget = GetStatValueSafe(node, "off_target", 1);
            }

            statDb.LastUpdated = DateTime.UtcNow;

            if (isNew) context.Set<LiveGameStat>().Add(statDb);
            else context.Entry(statDb).State = EntityState.Modified;

            return statDb;
        }

        private string GetGameStatusName(string timeStatus) => timeStatus switch
        {
            "1" => "Live",
            "2" => "HT",
            "3" => "FIM",
            "4" => "Adiado",
            "5" => "Cancelado",
            "7" => "Interrompido",
            _ => "Live"
        };

        // ✅ Método auxiliar espera JsonElement (obrigatório)
        private int GetStatValueSafe(JsonElement node, string key, int index)
        {
            try
            {
                if (node.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > index)
                {
                    var valStr = arr[index].GetString();
                    if (int.TryParse(valStr, out int val)) return val;
                }
            }
            catch { }
            return 0;
        }

        private void ProcessSelectionResult(BetSelection selection, string score)
        {
            if (selection.Status != "pending") return;
            selection.FinalScore = score;
            bool won = CheckWinner(selection.MarketName, selection.OutcomeName, score);
            selection.Status = won ? "Won" : "Lost";
            selection.IsWinner = won;
            if (won) Console.WriteLine($"   ✅ Aposta VENCEDORA: {selection.MarketName} -> {selection.OutcomeName}");
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
                Console.WriteLine($"🏆 [BILHETE VENCEU] ID: {ticket.Id}");
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

    public class NumberToStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number) return reader.TryGetInt64(out long l) ? l.ToString() : reader.GetDouble().ToString();
            if (reader.TokenType == JsonTokenType.String) return reader.GetString();
            using (JsonDocument document = JsonDocument.ParseValue(ref reader)) return document.RootElement.ToString();
        }
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }

    public class B365ResultItem
    {
        [JsonPropertyName("bet365_id")]
        public string Bet365Id { get; set; }
        [JsonPropertyName("id")]
        public string FallbackId { get; set; }
        public string ActualId => !string.IsNullOrEmpty(Bet365Id) ? Bet365Id : FallbackId;
        [JsonPropertyName("ss")]
        [JsonConverter(typeof(NumberToStringConverter))]
        public string Score { get; set; }
        [JsonPropertyName("time_status")]
        [JsonConverter(typeof(NumberToStringConverter))]
        public string TimeStatus { get; set; }
        [JsonPropertyName("stats")]
        public JsonElement? Stats { get; set; }
    }
}