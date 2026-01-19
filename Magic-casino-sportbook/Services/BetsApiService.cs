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
        private const int MAX_PAGES_PER_SPORT = 20;

        private readonly Dictionary<int, string> _sportMap = new() {
            { 1, "soccer" }, { 13, "tennis" }, { 18, "basketball" }, { 91, "volleyball" },
            { 12, "american-football" }, { 16, "baseball" }, { 17, "ice-hockey" },
            { 78, "handball" }, { 8, "rugby-union" }, { 9, "boxing" }, { 3, "cricket" },
            { 92, "table-tennis" }, { 94, "badminton" }, { 95, "futsal" },
            { 19, "snooker" }, { 14, "darts" }, { 36, "australian-rules" }, { 151, "esports" }
        };

        public BetsApiService(HttpClient httpClient, IServiceScopeFactory scopeFactory, IHubContext<GameHub> hubContext)
        {
            _httpClient = httpClient;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _token = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";
        }

        private bool IsValidImageId(string? id) => !string.IsNullOrEmpty(id) && id != "0";

        // ==============================================================================
        // 📅 ROBÔ 1: CALENDÁRIO
        // ==============================================================================
        public async Task SyncEventsSchedule()
        {
            if (string.IsNullOrEmpty(_token)) return;
            Console.WriteLine($"📅 [CALENDÁRIO] Iniciando varredura...");
            using var semaphore = new SemaphoreSlim(8);
            var tasks = new List<Task>();

            using (var configScope = _scopeFactory.CreateScope())
            {
                var configContext = configScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var disabledConfigs = await configContext.SportConfigurations.AsNoTracking().Where(c => c.IsEnabled == false).ToListAsync();
                var disabledSports = new HashSet<string>(disabledConfigs.Where(c => c.Type == "SPORT").Select(c => c.Identifier));
                var disabledLeagues = new HashSet<string>(disabledConfigs.Where(c => c.Type == "LEAGUE").Select(c => c.Identifier));
                var disabledTeams = new HashSet<string>(disabledConfigs.Where(c => c.Type == "TEAM").Select(c => c.Identifier));

                foreach (var sport in _sportMap)
                {
                    if (disabledSports.Contains(sport.Value)) continue;
                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            dbContext.Database.SetCommandTimeout(300);
                            await FetchUpcomingEventsOnly(sport.Key, sport.Value, dbContext, disabledLeagues, disabledTeams);
                        }
                        catch (Exception ex) { Console.WriteLine($"❌ Erro Calendário {sport.Value}: {ex.Message}"); }
                        finally { semaphore.Release(); }
                    }));
                }
            }
            await Task.WhenAll(tasks);
            await SyncMissingImages();
            Console.WriteLine("✅ [CALENDÁRIO] Finalizado!");
        }

        private async Task FetchUpcomingEventsOnly(int sportId, string sportKey, AppDbContext context, HashSet<string> disabledLeagues, HashSet<string> disabledTeams)
        {
            for (int page = 1; page <= MAX_PAGES_PER_SPORT; page++)
            {
                var url = $"{BASE_URL}/v1/bet365/upcoming?sport_id={sportId}&token={_token}&page={page}";
                try
                {
                    var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    if (!response.IsSuccessStatusCode) break;

                    using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    JsonElement results = default;
                    if (doc.RootElement.ValueKind == JsonValueKind.Array) results = doc.RootElement;
                    else if (doc.RootElement.TryGetProperty("results", out var res)) results = res;

                    if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) break;

                    var pageItems = results.EnumerateArray().ToList();
                    var pageIds = pageItems.Select(item => GetStringSafe(item, "id") ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList();
                    var existingGamesDict = await context.SportsEvents.Where(e => pageIds.Contains(e.ExternalId)).ToDictionaryAsync(e => e.ExternalId);

                    foreach (var item in pageItems)
                    {
                        string id = GetStringSafe(item, "id") ?? "";
                        if (string.IsNullOrEmpty(id)) continue;

                        string home = "", away = "", leagueName = "Outros";
                        string? rawHomeId = null, rawAwayId = null, rawLeagueId = null, rawCc = null;

                        if (item.TryGetProperty("home", out var hObj)) { home = GetStringSafe(hObj, "name") ?? ""; rawHomeId = GetStringSafe(hObj, "image_id"); }
                        if (item.TryGetProperty("away", out var aObj)) { away = GetStringSafe(aObj, "name") ?? ""; rawAwayId = GetStringSafe(aObj, "image_id"); }
                        if (item.TryGetProperty("league", out var lObj))
                        {
                            leagueName = GetStringSafe(lObj, "name") ?? "Outros";
                            rawLeagueId = GetStringSafe(lObj, "image_id");
                            rawCc = GetStringSafe(lObj, "cc");
                        }

                        string? homeId = IsValidImageId(rawHomeId) ? rawHomeId : null;
                        string? awayId = IsValidImageId(rawAwayId) ? rawAwayId : null;
                        string? leagueId = IsValidImageId(rawLeagueId) ? rawLeagueId : null;
                        string? countryCode = !string.IsNullOrEmpty(rawCc) ? rawCc : null;

                        if (IsFakeSport(leagueName, home, away, sportKey)) continue;
                        if (disabledLeagues.Contains(leagueName)) continue;
                        if ((homeId != null && disabledTeams.Contains(homeId)) || (awayId != null && disabledTeams.Contains(awayId))) continue;

                        long.TryParse(GetStringSafe(item, "time"), out long timeUnix);

                        if (existingGamesDict.TryGetValue(id, out var existingEvent))
                        {
                            bool changed = false;
                            if (existingEvent.League != leagueName) { existingEvent.League = leagueName; changed = true; }
                            if (existingEvent.HomeTeamId == null && homeId != null) { existingEvent.HomeTeamId = homeId; existingEvent.HomeTeamLogo = $"https://assets.b365api.com/images/team/m/{homeId}.png"; changed = true; }
                            if (existingEvent.AwayTeamId == null && awayId != null) { existingEvent.AwayTeamId = awayId; existingEvent.AwayTeamLogo = $"https://assets.b365api.com/images/team/m/{awayId}.png"; changed = true; }
                            if (existingEvent.LeagueId == null && leagueId != null) { existingEvent.LeagueId = leagueId; changed = true; }
                            if (existingEvent.CountryCode == null && countryCode != null) { existingEvent.CountryCode = countryCode; changed = true; }

                            if (changed) existingEvent.LastUpdate = DateTime.UtcNow;
                        }
                        else
                        {
                            var newEvent = new SportsEvent
                            {
                                ExternalId = id,
                                HomeTeam = home,
                                AwayTeam = away,
                                League = leagueName,
                                CommenceTime = DateTimeOffset.FromUnixTimeSeconds(timeUnix).UtcDateTime,
                                SportKey = sportKey,
                                SportCategory = sportKey,
                                OddsSource = "BetsAPI_Premium",
                                LastUpdate = DateTime.UtcNow,
                                HomeTeamId = homeId,
                                AwayTeamId = awayId,
                                LeagueId = leagueId,
                                CountryCode = countryCode,
                                HomeTeamLogo = homeId != null ? $"https://assets.b365api.com/images/team/m/{homeId}.png" : null,
                                AwayTeamLogo = awayId != null ? $"https://assets.b365api.com/images/team/m/{awayId}.png" : null,
                                Status = "Prematch",
                                Odds = new List<EventMarket>() // ✅ CORREÇÃO: Usando EventMarket
                            };
                            context.SportsEvents.Add(newEvent);
                        }
                    }
                    await context.SaveChangesAsync();
                }
                catch (Exception) { }
                await Task.Delay(100);
            }
        }

        public async Task SyncPrematchOdds()
        {
            if (string.IsNullOrEmpty(_token)) return;
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var upcomingGames = await context.SportsEvents
                .Where(e => e.CommenceTime > DateTime.UtcNow && e.CommenceTime < DateTime.UtcNow.AddDays(4)
                         && (e.RawOddsHome <= 1 || e.LastUpdate < DateTime.UtcNow.AddMinutes(-30)))
                .OrderBy(e => e.CommenceTime).Take(300).ToListAsync();

            if (upcomingGames.Any())
            {
                Console.WriteLine($"💰 [ODDS PREMATCH] Atualizando {upcomingGames.Count} jogos...");
                await FetchBet365OddsIndividual(upcomingGames);
            }
        }

        private async Task FetchBet365OddsIndividual(List<SportsEvent> games)
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 25 };
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
                                if (ProcessStructuredOdds(resultItem, dbGame, localContext))
                                {
                                    dbGame.LastUpdate = DateTime.UtcNow;
                                    localContext.Entry(dbGame).State = EntityState.Modified;
                                }
                            }
                            else if (resultItem.ValueKind == JsonValueKind.Array)
                            {
                                if (await ProcessFlatListMatch(resultItem, dbGame, localContext))
                                {
                                    dbGame.LastUpdate = DateTime.UtcNow;
                                    localContext.Entry(dbGame).State = EntityState.Modified;
                                }
                            }
                        }
                    }
                    dbGame.LastUpdate = DateTime.UtcNow;
                    await localContext.SaveChangesAsync(ct);
                }
                catch (Exception ex) { Console.WriteLine($"❌ Erro Odds {game.ExternalId}: {ex.Message}"); }
            });
        }

        private bool IsMatchWinnerMarket(string key, string name)
        {
            key = key.ToLower().Trim();
            name = name.ToLower().Trim();
            if (key.Contains("1st") || name.Contains("1st") || key.Contains("2nd") || name.Contains("2nd") ||
                key.Contains("quarter") || name.Contains("quarter") || key.Contains("half") || name.Contains("half") ||
                key.Contains("inning") || name.Contains("inning") || key.Contains("set ") || name.Contains("set ") ||
                key.Contains("handicap") || name.Contains("handicap") || key.Contains("total") || name.Contains("total") ||
                key.Contains("over") || name.Contains("over") || key.Contains("under") || name.Contains("under"))
                return false;

            if (key == "full_time_result" || key == "match_winner" || key == "1x2" ||
                key == "game_lines" || key == "match_lines" || key == "money_line" ||
                key == "moneyline" || key == "match_odds" || key == "to_win_match" ||
                key == "to win fight" || key == "fight winner" || key == "to win the match" ||
                key == "match betting" || key == "game winner" || key == "2_way_winner" || key == "winner" || key == "head_to_head")
                return true;

            if (name.Contains("winner") || name == "money line" || name.Contains("1x2")) return true;
            return false;
        }

        private bool ProcessStructuredOdds(JsonElement data, SportsEvent game, AppDbContext context)
        {
            bool updated = false;
            if (data.TryGetProperty("main", out var mainObj) && mainObj.TryGetProperty("sp", out var spObj))
            {
                foreach (var property in spObj.EnumerateObject())
                {
                    string marketKey = property.Name.ToLower();
                    JsonElement marketNode = property.Value;
                    if (marketNode.ValueKind != JsonValueKind.Object || !marketNode.TryGetProperty("odds", out var oddsArray)) continue;

                    string marketName = GetStringSafe(marketNode, "name") ?? marketKey;

                    foreach (var odd in oddsArray.EnumerateArray())
                    {
                        decimal price = ParseFractionalOrDecimal(GetStringSafe(odd, "odds"));
                        if (price > 1.0m)
                        {
                            string name = GetStringSafe(odd, "name") ?? "";
                            string header = GetStringSafe(odd, "header") ?? "";
                            string outcomeName = (!string.IsNullOrEmpty(header) && !name.Contains(header)) ? $"{name} {header}".Trim() : (string.IsNullOrEmpty(name) ? header : name);
                            AddHistoryOdd(game, marketName, outcomeName, price, context);
                        }
                    }

                    if (IsMatchWinnerMarket(marketKey, marketName))
                    {
                        if (Process1x2Market(marketNode, game, context)) updated = true;
                    }
                }
            }
            return updated;
        }

        private void AddHistoryOdd(SportsEvent game, string market, string outcome, decimal price, AppDbContext context)
        {
            // ✅ CORREÇÃO: Usando EventMarkets (nova tabela)
            var existingOdd = game.Odds.FirstOrDefault(o => o.MarketName == market && o.OutcomeName == outcome);

            // Busca na tabela local se não estiver carregado na memória do objeto
            if (existingOdd == null)
                existingOdd = context.EventMarkets.Local.FirstOrDefault(m => m.SportsEventId == game.Id && m.MarketName == market && m.OutcomeName == outcome);

            if (existingOdd != null)
            {
                existingOdd.Price = price;
                existingOdd.LastUpdate = DateTime.UtcNow;
            }
            else
            {
                game.Odds.Add(new EventMarket
                {
                    SportsEventId = game.Id, // Usa o GUID interno 
                    MarketName = market,
                    OutcomeName = outcome,
                    Price = price,
                    LastUpdate = DateTime.UtcNow
                });
            }
        }

        private bool Process1x2Market(JsonElement marketNode, SportsEvent game, AppDbContext context)
        {
            bool changed = false;
            if (marketNode.TryGetProperty("odds", out var oddsArray))
            {
                string hTeam = game.HomeTeam?.ToLower().Trim() ?? "xxx";
                string aTeam = game.AwayTeam?.ToLower().Trim() ?? "xxx";

                foreach (var odd in oddsArray.EnumerateArray())
                {
                    decimal price = ParseFractionalOrDecimal(GetStringSafe(odd, "odds"));
                    if (price <= 1.0m) continue;

                    string name = (GetStringSafe(odd, "name") ?? "").ToLower().Trim();
                    string header = (GetStringSafe(odd, "header") ?? "").ToLower().Trim();

                    if (!string.IsNullOrEmpty(GetStringSafe(odd, "handicap"))) continue;

                    string target = "";
                    if (header == "1" || name == "1") target = "1";
                    else if (header == "2" || name == "2") target = "2";
                    else if (header == "x" || name == "x" || name == "draw") target = "X";
                    else if (name == hTeam || hTeam.Contains(name) || name.Contains(hTeam)) target = "1";
                    else if (name == aTeam || aTeam.Contains(name) || name.Contains(aTeam)) target = "2";

                    if (target == "1" && game.RawOddsHome != price) { game.RawOddsHome = price; changed = true; }
                    else if (target == "2" && game.RawOddsAway != price) { game.RawOddsAway = price; changed = true; }
                    else if (target == "X" && game.RawOddsDraw != price) { game.RawOddsDraw = price; changed = true; }
                }
            }
            return changed;
        }

        private async Task<bool> ProcessFlatListMatch(JsonElement listItems, SportsEvent currentGame, AppDbContext context)
        {
            bool isProcessingMatchWinner = false;
            bool changed = false;

            foreach (var item in listItems.EnumerateArray())
            {
                string type = GetStringSafe(item, "type") ?? "";
                if (type == "MG")
                {
                    string mkName = GetStringSafe(item, "NA") ?? "";
                    isProcessingMatchWinner = IsMatchWinnerMarket(mkName, mkName);
                }

                if (type == "PA" && isProcessingMatchWinner)
                {
                    decimal decOdd = ParseFractionalOrDecimal(GetStringSafe(item, "OD"));
                    if (decOdd > 1)
                    {
                        string n2 = (GetStringSafe(item, "N2") ?? "").ToLower();
                        string na = (GetStringSafe(item, "NA") ?? "").ToLower();

                        if (!string.IsNullOrEmpty(GetStringSafe(item, "HA"))) continue;

                        string hTeam = currentGame.HomeTeam?.ToLower().Trim() ?? "xxx";
                        string aTeam = currentGame.AwayTeam?.ToLower().Trim() ?? "xxx";
                        bool localChange = false;

                        if (n2 == "1" || na == "1" || na == hTeam || na.Contains(hTeam)) { currentGame.RawOddsHome = decOdd; localChange = true; }
                        else if (n2 == "2" || na == "2" || na == aTeam || na.Contains(aTeam)) { currentGame.RawOddsAway = decOdd; localChange = true; }
                        else if (n2 == "x" || na == "x" || na.Contains("draw")) { currentGame.RawOddsDraw = decOdd; localChange = true; }

                        if (localChange)
                        {
                            AddHistoryOdd(currentGame, "Match Winner", GetStringSafe(item, "NA") ?? "Unknown", decOdd, context);
                            changed = true;
                        }
                    }
                }
            }
            return changed;
        }

        private string? GetStringSafe(JsonElement el, string key) { if (el.ValueKind != JsonValueKind.Object) return null; return el.TryGetProperty(key, out var prop) ? (prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString()) : null; }
        private decimal ParseFractionalOrDecimal(string? val) { if (string.IsNullOrEmpty(val)) return 0; if (val.Contains("/")) { var p = val.Split('/'); return (decimal.Parse(p[0]) / decimal.Parse(p[1])) + 1; } return decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0; }
        private bool IsFakeSport(string l, string h, string a, string k) => !string.IsNullOrEmpty(l) && (l.ToUpper().Contains("ESOCCER") || l.ToUpper().Contains("E-SOCCER"));

        public async Task SyncBaseOddsToDatabase() => await SyncEventsSchedule();

        public async Task SyncMissingImages()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var jogosSemImagem = await context.SportsEvents.Where(g => g.CommenceTime > DateTime.UtcNow && (g.HomeTeamId == null || g.LeagueId == null)).OrderBy(g => g.CommenceTime).Take(20).ToListAsync();
                if (!jogosSemImagem.Any()) return;

                var options = new ParallelOptions { MaxDegreeOfParallelism = 5 };
                await Parallel.ForEachAsync(jogosSemImagem, options, async (game, ct) =>
                {
                    var url = $"https://api.betsapi.com/bet365/result?token={_token}&event_id={game.ExternalId}";
                    try
                    {
                        var response = await _httpClient.GetAsync(url, ct);
                        if (response.IsSuccessStatusCode)
                        {
                            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                            if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in results.EnumerateArray())
                                {
                                    string id = GetStringSafe(item, "id") ?? "";
                                    if (id != game.ExternalId && GetStringSafe(item, "bet365_id") != game.ExternalId) continue;
                                    bool updated = false;
                                    if (item.TryGetProperty("home", out var h) && IsValidImageId(GetStringSafe(h, "image_id"))) { game.HomeTeamId = GetStringSafe(h, "image_id"); game.HomeTeamLogo = $"https://assets.b365api.com/images/team/m/{game.HomeTeamId}.png"; updated = true; }
                                    if (item.TryGetProperty("away", out var a) && IsValidImageId(GetStringSafe(a, "image_id"))) { game.AwayTeamId = GetStringSafe(a, "image_id"); game.AwayTeamLogo = $"https://assets.b365api.com/images/team/m/{game.AwayTeamId}.png"; updated = true; }
                                    if (item.TryGetProperty("league", out var l))
                                    {
                                        if (IsValidImageId(GetStringSafe(l, "image_id"))) { game.LeagueId = GetStringSafe(l, "image_id"); updated = true; }
                                        // ✅ ATUALIZA CÓDIGO DO PAÍS AQUI TAMBÉM
                                        if (!string.IsNullOrEmpty(GetStringSafe(l, "cc"))) { game.CountryCode = GetStringSafe(l, "cc"); updated = true; }
                                    }
                                    if (updated) game.LastUpdate = DateTime.UtcNow;
                                }
                            }
                        }
                    }
                    catch { }
                });
                await context.SaveChangesAsync();
            }
            catch { }
        }
    }
}