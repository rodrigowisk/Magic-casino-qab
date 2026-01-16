using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Data.Models;
using Magic_casino_sportbook.DTOs;
using Magic_casino_sportbook.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace Magic_casino_sportbook.Services
{
    public class BetsApiService : IOddsService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly string _token;

        private const string BASE_URL = "https://api.b365api.com";
        private const int MAX_PAGES_PER_SPORT = 50;

        private readonly Dictionary<int, string> _sportMap = new() {
            { 1, "soccer" }
        };

        public BetsApiService(HttpClient httpClient, IServiceScopeFactory scopeFactory, IHubContext<GameHub> hubContext)
        {
            _httpClient = httpClient;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _token = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";
        }

        public async Task SyncBaseOddsToDatabase()
        {
            if (string.IsNullOrEmpty(_token)) return;

            using var semaphore = new SemaphoreSlim(10);
            var tasks = new List<Task>();

            Console.WriteLine($"💎 [SYNC PREMIUM V8] Conectando em {BASE_URL}...");

            using (var configScope = _scopeFactory.CreateScope())
            {
                var configContext = configScope.ServiceProvider.GetRequiredService<AppDbContext>();

                var disabledConfigs = await configContext.SportConfigurations
                    .AsNoTracking()
                    .Where(c => c.IsEnabled == false)
                    .ToListAsync();

                var disabledSports = new HashSet<string>(disabledConfigs.Where(c => c.Type == "SPORT").Select(c => c.Identifier));
                var disabledLeagues = new HashSet<string>(disabledConfigs.Where(c => c.Type == "LEAGUE").Select(c => c.Identifier));
                var disabledTeams = new HashSet<string>(disabledConfigs.Where(c => c.Type == "TEAM").Select(c => c.Identifier));

                foreach (var sport in _sportMap)
                {
                    if (disabledSports.Contains(sport.Value))
                    {
                        Console.WriteLine($"⛔ [ROBÔ] Ignorando esporte {sport.Value} (Desativado no Admin).");
                        continue;
                    }

                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            dbContext.Database.SetCommandTimeout(300);

                            await ProcessSingleSport(sport.Key, sport.Value, dbContext, disabledLeagues, disabledTeams);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Erro no esporte {sport.Value}: {ex.Message}");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);
            await SyncMissingImages();
            Console.WriteLine("✅ [SYNC PREMIUM] Finalizado!");
        }

        private async Task ProcessSingleSport(int sportId, string sportKey, AppDbContext context, HashSet<string> disabledLeagues, HashSet<string> disabledTeams)
        {
            var apiGames = await FetchBet365EventsPaged(sportId, sportKey, context, disabledLeagues, disabledTeams);

            if (apiGames.Count == 0) return;

            var gamesToUpdate = apiGames.ToList();

            if (gamesToUpdate.Any())
            {
                Console.WriteLine($"🔍 {sportKey}: Atualizando ODDS de {gamesToUpdate.Count} jogos...");
                await FetchBet365OddsIndividual(gamesToUpdate);
            }
        }

        public async Task SyncMissingImages()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Pega jogos que estão sem imagem, para tentar achar o image_id correto
                var jogosSemImagem = await context.SportsEvents
                    .Where(g => g.CommenceTime > DateTime.UtcNow && (g.HomeTeamId == null || g.AwayTeamId == null))
                    .OrderBy(g => g.CommenceTime)
                    .Take(50)
                    .ToListAsync();

                if (!jogosSemImagem.Any()) return;

                Console.WriteLine($"🖼️ [IMAGENS] Tentando resgatar logos de {jogosSemImagem.Count} jogos...");

                var batches = jogosSemImagem
                    .Select((x, i) => new { Index = i, Value = x })
                    .GroupBy(x => x.Index / 10)
                    .Select(x => x.Select(v => v.Value).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    var idsString = string.Join(",", batch.Select(g => g.ExternalId));
                    var url = $"{BASE_URL}/v1/bet365/event?token={_token}&FI={idsString}";

                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                        if (doc.RootElement.TryGetProperty("results", out var results))
                        {
                            foreach (var item in results.EnumerateArray())
                            {
                                string id = GetStringSafe(item, "id") ?? "";
                                var game = batch.FirstOrDefault(g => g.ExternalId == id);
                                if (game != null)
                                {
                                    // 🔴 CORREÇÃO AQUI: Removemos o fallback para "id".
                                    // Se não tiver image_id, fica NULL mesmo.
                                    if (item.TryGetProperty("home", out var h))
                                        game.HomeTeamId = GetStringSafe(h, "image_id");

                                    if (item.TryGetProperty("away", out var a))
                                        game.AwayTeamId = GetStringSafe(a, "image_id");

                                    if (item.TryGetProperty("league", out var l))
                                        game.LeagueId = GetStringSafe(l, "image_id");

                                    context.Entry(game).State = EntityState.Modified;
                                }
                            }
                        }
                    }
                    await Task.Delay(200);
                }
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro no SyncMissingImages: {ex.Message}");
            }
        }

        private async Task<List<SportsEvent>> FetchBet365EventsPaged(int sportId, string sportKey, AppDbContext context, HashSet<string> disabledLeagues, HashSet<string> disabledTeams)
        {
            var allTrackedEvents = new List<SportsEvent>();
            int page = 1;

            while (page <= MAX_PAGES_PER_SPORT)
            {
                var url = $"{BASE_URL}/v1/bet365/upcoming?sport_id={sportId}&token={_token}&page={page}";
                try
                {
                    var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    if (!response.IsSuccessStatusCode) break;

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var doc = await JsonDocument.ParseAsync(stream);
                    JsonElement results = default;

                    if (doc.RootElement.ValueKind == JsonValueKind.Array) results = doc.RootElement;
                    else if (doc.RootElement.TryGetProperty("results", out var res)) results = res;

                    if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) break;

                    var pageItems = results.EnumerateArray().ToList();
                    var pageIds = pageItems.Select(item => GetStringSafe(item, "id") ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList();

                    var existingGamesDict = await context.SportsEvents
                        .Where(e => pageIds.Contains(e.ExternalId))
                        .ToDictionaryAsync(e => e.ExternalId);

                    foreach (var item in pageItems)
                    {
                        string betsapiId = GetStringSafe(item, "id") ?? "";
                        if (string.IsNullOrEmpty(betsapiId)) continue;

                        string home = "", away = "", leagueName = "Outros";
                        string? homeId = null, awayId = null, leagueId = null;

                        // 🔴 CORREÇÃO AQUI TAMBÉM: Nada de usar ID como imagem
                        if (item.TryGetProperty("home", out var hObj))
                        {
                            home = GetStringSafe(hObj, "name") ?? "";
                            homeId = GetStringSafe(hObj, "image_id"); // Somente image_id
                        }

                        if (item.TryGetProperty("away", out var aObj))
                        {
                            away = GetStringSafe(aObj, "name") ?? "";
                            awayId = GetStringSafe(aObj, "image_id"); // Somente image_id
                        }

                        if (item.TryGetProperty("league", out var lObj))
                        {
                            leagueName = GetStringSafe(lObj, "name") ?? "Outros";
                            leagueId = GetStringSafe(lObj, "image_id"); // Somente image_id
                        }

                        // --- FILTROS ---
                        if (IsFakeSport(leagueName, home, away, sportKey)) continue;
                        if (disabledLeagues.Contains(leagueName)) continue;
                        if ((homeId != null && disabledTeams.Contains(homeId)) || (awayId != null && disabledTeams.Contains(awayId))) continue;

                        string timeStr = GetStringSafe(item, "time") ?? "";
                        if (!long.TryParse(timeStr, out long timeUnix)) continue;

                        if (existingGamesDict.TryGetValue(betsapiId, out var existingEvent))
                        {
                            bool changed = false;
                            if (existingEvent.League != leagueName) { existingEvent.League = leagueName; changed = true; }

                            // Só atualiza se achou uma imagem válida nova
                            if (existingEvent.HomeTeamId == null && homeId != null) { existingEvent.HomeTeamId = homeId; changed = true; }
                            if (existingEvent.AwayTeamId == null && awayId != null) { existingEvent.AwayTeamId = awayId; changed = true; }
                            if (existingEvent.LeagueId == null && leagueId != null) { existingEvent.LeagueId = leagueId; changed = true; }

                            if (changed) existingEvent.LastUpdate = DateTime.UtcNow;
                            allTrackedEvents.Add(existingEvent);
                        }
                        else
                        {
                            var newEvent = new SportsEvent
                            {
                                ExternalId = betsapiId,
                                HomeTeam = home,
                                AwayTeam = away,
                                CommenceTime = DateTimeOffset.FromUnixTimeSeconds(timeUnix).UtcDateTime,
                                SportKey = sportKey,
                                SportCategory = sportKey,
                                League = leagueName,
                                OddsSource = "BetsAPI_Premium",
                                LastUpdate = DateTime.UtcNow,
                                HomeTeamId = homeId,
                                AwayTeamId = awayId,
                                LeagueId = leagueId,
                                Odds = new List<MarketOdd>()
                            };
                            context.SportsEvents.Add(newEvent);
                            allTrackedEvents.Add(newEvent);
                        }
                    }
                    await context.SaveChangesAsync();
                }
                catch (Exception) { }
                page++;
                await Task.Delay(100);
            }
            return allTrackedEvents;
        }

        private async Task FetchBet365OddsIndividual(List<SportsEvent> games)
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 10 };

            await Parallel.ForEachAsync(games, parallelOptions, async (game, ct) =>
            {
                var cleanId = game.ExternalId.Trim();
                var url = $"{BASE_URL}/v4/bet365/prematch?token={_token}&FI={cleanId}";

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var localContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var dbGame = await localContext.SportsEvents.Include(e => e.Odds).FirstOrDefaultAsync(e => e.ExternalId == game.ExternalId, ct);
                    if (dbGame == null) return;

                    var response = await _httpClient.GetAsync(url, ct);
                    if (!response.IsSuccessStatusCode) return;

                    var jsonString = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(jsonString);
                    JsonElement root = doc.RootElement;

                    if (root.TryGetProperty("results", out var resultsArray) && resultsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var resultItem in resultsArray.EnumerateArray())
                        {
                            if (resultItem.TryGetProperty("main", out _))
                            {
                                bool updated = ProcessStructuredOdds(resultItem, dbGame, localContext);
                                if (updated)
                                {
                                    dbGame.LastUpdate = DateTime.UtcNow;
                                    localContext.Entry(dbGame).State = EntityState.Modified;
                                    Console.WriteLine($"💰 [v4] {dbGame.HomeTeam}: {dbGame.RawOddsHome} | {dbGame.RawOddsDraw} | {dbGame.RawOddsAway}");
                                }
                            }
                            else if (resultItem.ValueKind == JsonValueKind.Array)
                            {
                                await ProcessFlatListMatch(resultItem, dbGame, localContext);
                            }
                        }
                    }
                    await localContext.SaveChangesAsync(ct);
                }
                catch (Exception ex) { Console.WriteLine($"❌ Erro Jogo {game.ExternalId}: {ex.Message}"); }
            });
        }

        private bool ProcessStructuredOdds(JsonElement data, SportsEvent game, AppDbContext context)
        {
            bool updated = false;

            if (data.TryGetProperty("main", out var mainObj) && mainObj.TryGetProperty("sp", out var spObj))
            {
                foreach (var property in spObj.EnumerateObject())
                {
                    string marketKey = property.Name;
                    JsonElement marketNode = property.Value;

                    if (marketNode.ValueKind != JsonValueKind.Object) continue;
                    if (!marketNode.TryGetProperty("odds", out var oddsArray)) continue;

                    string marketName = GetStringSafe(marketNode, "name") ?? marketKey;

                    foreach (var odd in oddsArray.EnumerateArray())
                    {
                        string oddValStr = GetStringSafe(odd, "odds") ?? "0";
                        decimal price = ParseFractionalOrDecimal(oddValStr);
                        if (price <= 1.0m) continue;

                        string name = GetStringSafe(odd, "name") ?? "";
                        string header = GetStringSafe(odd, "header") ?? "";

                        string outcomeName = name;
                        if (!string.IsNullOrEmpty(header) && !name.Contains(header))
                        {
                            outcomeName = $"{name} {header}".Trim();
                        }
                        if (string.IsNullOrEmpty(outcomeName)) outcomeName = header;

                        AddHistoryOdd(game, marketName, outcomeName, price, context);
                    }

                    if (marketKey == "full_time_result" || marketKey == "match_winner")
                    {
                        Process1x2Market(marketNode, game, context);
                        updated = true;
                    }
                }
            }
            return updated;
        }

        private void AddHistoryOdd(SportsEvent game, string market, string outcome, decimal price, AppDbContext context)
        {
            var existingOdd = game.Odds.FirstOrDefault(o => o.MarketName == market && o.OutcomeName == outcome);

            if (existingOdd == null)
            {
                existingOdd = context.MarketOdds
                    .Local
                    .FirstOrDefault(m => m.SportsEventId == game.ExternalId && m.MarketName == market && m.OutcomeName == outcome);
            }

            if (existingOdd != null)
            {
                if (existingOdd.Price != price)
                {
                    existingOdd.Price = price;
                }
            }
            else
            {
                var newOdd = new MarketOdd
                {
                    SportsEventId = game.ExternalId,
                    Bookmaker = "Bet365",
                    MarketName = market,
                    OutcomeName = outcome,
                    Price = price
                };
                game.Odds.Add(newOdd);
            }
        }

        private bool Process1x2Market(JsonElement marketNode, SportsEvent game, AppDbContext context)
        {
            bool changed = false;
            string marketName = GetStringSafe(marketNode, "name") ?? "Match Winner";

            if (marketNode.TryGetProperty("odds", out var oddsArray) && oddsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var odd in oddsArray.EnumerateArray())
                {
                    string oddValStr = GetStringSafe(odd, "odds") ?? "0";
                    string name = GetStringSafe(odd, "name") ?? "";
                    string header = GetStringSafe(odd, "header") ?? "";
                    decimal price = ParseFractionalOrDecimal(oddValStr);

                    if (price <= 0) continue;

                    string target = !string.IsNullOrEmpty(name) ? name : header;

                    if (target == "1") { if (game.RawOddsHome != price) { game.RawOddsHome = price; changed = true; } }
                    else if (target == "2") { if (game.RawOddsAway != price) { game.RawOddsAway = price; changed = true; } }
                    else if (target.ToLower() == "draw" || target == "X") { if (game.RawOddsDraw != price) { game.RawOddsDraw = price; changed = true; } }
                }
            }
            return changed;
        }

        private async Task ProcessFlatListMatch(JsonElement listItems, SportsEvent currentGame, AppDbContext context)
        {
            bool isProcessingMatchWinner = false;

            foreach (var item in listItems.EnumerateArray())
            {
                string type = GetStringSafe(item, "type") ?? "";

                if (type == "MG")
                {
                    string marketName = GetStringSafe(item, "NA") ?? "";
                    if (marketName == "Fulltime Result" || marketName == "Match Winner" || marketName == "1X2")
                        isProcessingMatchWinner = true;
                    else
                        isProcessingMatchWinner = false;
                    continue;
                }

                if (type == "PA" && isProcessingMatchWinner)
                {
                    string oddStr = GetStringSafe(item, "OD") ?? "0";
                    string n2 = GetStringSafe(item, "N2") ?? "";
                    string name = GetStringSafe(item, "NA") ?? "";

                    decimal decimalOdd = ParseFractionalOrDecimal(oddStr);

                    if (decimalOdd > 1)
                    {
                        bool changed = false;
                        if (n2 == "1") { currentGame.RawOddsHome = decimalOdd; changed = true; }
                        else if (n2 == "2") { currentGame.RawOddsAway = decimalOdd; changed = true; }
                        else if (n2 == "X") { currentGame.RawOddsDraw = decimalOdd; changed = true; }

                        if (changed)
                        {
                            AddHistoryOdd(currentGame, "Match Winner", name, decimalOdd, context);
                            currentGame.LastUpdate = DateTime.UtcNow;
                            context.Entry(currentGame).State = EntityState.Modified;
                            Console.WriteLine($"💰 [Flat] {currentGame.HomeTeam}: {n2} -> {decimalOdd}");
                        }
                    }
                }
            }
            await Task.CompletedTask;
        }

        private string? GetStringSafe(JsonElement el, string key)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            if (el.TryGetProperty(key, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String) return prop.GetString();
                if (prop.ValueKind == JsonValueKind.Number) return prop.ToString();
            }
            return null;
        }

        private decimal ParseFractionalOrDecimal(string val)
        {
            if (string.IsNullOrEmpty(val)) return 0;
            if (val.Contains("/"))
            {
                var p = val.Split('/');
                if (p.Length == 2 && decimal.TryParse(p[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var n) && decimal.TryParse(p[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var d) && d != 0)
                    return (n / d) + 1;
            }
            if (decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var res)) return res;
            return 0;
        }

        private bool IsFakeSport(string league, string home, string away, string sportKey)
        {
            if (!string.IsNullOrWhiteSpace(league))
            {
                string l = league.ToUpper();
                if (l.Contains("ESOCCER") || l.Contains("E-SOCCER") || l.Contains("EBASKETBALL") || l.Contains("ESPORTS")) return true;
            }
            return false;
        }

        public Task BackfillSportKeyRaw() => Task.CompletedTask;
        public Task<bool> SyncFullMarketsForEvent(string eventId) => Task.FromResult(true);
        public async Task SyncLiveFeed() => await Task.CompletedTask;
    }
}