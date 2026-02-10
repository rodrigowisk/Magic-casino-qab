using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Data.Models;
using Magic_casino_sportbook.DTOs;
using Magic_casino_sportbook.Hubs;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Services.Parsers; // ✅ Importando os novos Parsers
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Magic_casino_sportbook.Services
{
    public class BetsApiService : IOddsService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly string _token;
        private readonly List<IMarketParser> _parsers; // ✅ Lista de Parsers fatorados

        // Semáforo local para controle de concorrência e Rate Limit
        private static readonly SemaphoreSlim _localGate = new SemaphoreSlim(1, 1);

        private const string BASE_URL = "https://api.b365api.com";
        private const int MAX_PAGES_PER_SPORT = 20;
        private const int REQUEST_CHUNK_SIZE = 50;

        private readonly Dictionary<int, string> _sportMap = new() {
            { 1, "soccer" }, { 13, "tennis" }, { 18, "basketball" }, { 91, "volleyball" },
            { 12, "american-football" }, { 16, "baseball" }, { 17, "ice-hockey" },
            { 78, "handball" }, { 8, "rugby-union" }, { 9, "boxing" }, { 3, "cricket" },
            { 92, "table-tennis" }, { 94, "badminton" }, { 95, "futsal" },
            { 19, "snooker" }, { 14, "darts" }, { 36, "australian-rules" }, { 151, "esports" }
        };

        private readonly string[] _excludedTerms = new[]
        {
            "ESOCCER", "E-SOCCER", "E SOCCER", "EBASKETBALL", "E-BASKETBALL",
            "E BASKETBALL", "EVOLLEYBALL", "E-VOLLEYBALL", "E-HOCKEY", "EHOCKEY",
            "E-CRICKET", "ECRICKET", "VIRTUAL", "CYBER", "SIMULATED",
            "GT LEAGUE", "BATTLE", "2X2", "4X4", "PENALTY", "E-SPORT", "ESPORT", "FIFA", "NBA 2K"
        };

        public BetsApiService(
            HttpClient httpClient,
            IServiceScopeFactory scopeFactory,
            IHubContext<GameHub> hubContext)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _token = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";

            // ✅ Inicializa os parsers fatorados
            _parsers = new List<IMarketParser>
            {
                new MainParser(), new AsianParser(), new GoalsParser(), new PropsParser()
            };
        }

        private bool IsValidImageId(string? id) => !string.IsNullOrEmpty(id) && id != "0";

        // ==============================================================================
        // 📅 ROBÔ DE GRADE (Fluxo Principal + Resgate)
        // ==============================================================================
        public async Task SyncEventsSchedule()
        {
            if (string.IsNullOrEmpty(_token)) return;
            Console.WriteLine($"📅 [SYNC] Iniciando ciclo de grade...");

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

                    var idsToFetchOdds = new List<string>();

                    // 1. Busca Jogos Futuros (Upcoming)
                    for (int page = 1; page <= MAX_PAGES_PER_SPORT; page++)
                    {
                        try
                        {
                            await _localGate.WaitAsync();
                            List<string> idsFound;
                            try
                            {
                                idsFound = await ProcessSportPageAsync(sport.Key, sport.Value, page, disabledLeagues, disabledTeams);
                            }
                            finally
                            {
                                _localGate.Release();
                            }

                            if (!idsFound.Any()) break;
                            idsToFetchOdds.AddRange(idsFound);
                            await Task.Delay(1000);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Erro pág {page} {sport.Value}: {ex.Message}");
                            break;
                        }
                    }

                    // 2. Busca Odds Iniciais
                    if (idsToFetchOdds.Any())
                    {
                        var uniqueIds = idsToFetchOdds.Distinct().ToList();
                        Console.WriteLine($"💰 [ODDS] {sport.Value}: Buscando odds para {uniqueIds.Count} jogos...");
                        await FetchOddsList(uniqueIds, sport.Value);
                    }

                    // 3. 🚑 RESGATE DE JOGOS AO VIVO
                    await RescueLiveGames(sport.Key, sport.Value);
                }
            }

            await SyncMissingImages();
            Console.WriteLine("✅ [SYNC] Ciclo Finalizado!");
        }

        // ==============================================================================
        // 🚑 RESGATE ECONÔMICO (INTEGRADO NO CICLO)
        // ==============================================================================
        private async Task RescueLiveGames(int sportId, string sportKey)
        {
            var url = $"{BASE_URL}/v1/bet365/inplay_filter?sport_id={sportId}&token={_token}";

            try
            {
                await _localGate.WaitAsync();
                var response = await _httpClient.GetAsync(url);
                _localGate.Release();

                if (!response.IsSuccessStatusCode) return;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var liveIdsInApi = new List<string>();
                    foreach (var item in results.EnumerateArray())
                    {
                        var id = GetStringSafe(item, "id");
                        if (!string.IsNullOrEmpty(id)) liveIdsInApi.Add(id);
                    }

                    if (!liveIdsInApi.Any()) return;

                    var gamesInLimbo = await context.SportsEvents
                        .Where(g => liveIdsInApi.Contains(g.ExternalId) && g.Status != "Live" && g.Status != "Ended")
                        .ToListAsync();

                    if (gamesInLimbo.Any())
                    {
                        Console.WriteLine($"🚑 [RESGATE] {sportKey}: Atualizando {gamesInLimbo.Count} jogos para LIVE.");

                        foreach (var game in gamesInLimbo)
                        {
                            game.Status = "Live";
                            game.LastUpdate = DateTime.UtcNow;
                            if (string.IsNullOrEmpty(game.GameTime) || game.GameTime == "0'") game.GameTime = "1'";
                        }

                        await context.SaveChangesAsync();

                        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
                        await hub.Clients.All.SendAsync("GameWentLive", gamesInLimbo);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_localGate.CurrentCount == 0) _localGate.Release();
                Console.WriteLine($"⚠️ Erro Resgate {sportKey}: {ex.Message}");
            }
        }

        private async Task<List<string>> ProcessSportPageAsync(int sportId, string sportKey, int page, HashSet<string> disabledLeagues, HashSet<string> disabledTeams)
        {
            var foundIds = new List<string>();
            var url = $"{BASE_URL}/v1/bet365/upcoming?sport_id={sportId}&token={_token}&page={page}";

            var response = await _httpClient.GetAsync(url);

            if ((int)response.StatusCode == 429)
            {
                Console.WriteLine($"⛔ [SCHEDULE] 429 na pág {page}. Pausando 5s...");
                await Task.Delay(5000);
                return foundIds;
            }

            if (!response.IsSuccessStatusCode) return foundIds;

            try
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                JsonElement results = default;
                if (doc.RootElement.ValueKind == JsonValueKind.Array) results = doc.RootElement;
                else if (doc.RootElement.TryGetProperty("results", out var res)) results = res;

                if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return foundIds;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var pageItems = results.EnumerateArray().ToList();
                    var pageIds = pageItems.Select(item => GetStringSafe(item, "id") ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList();

                    var existingGamesDict = await context.SportsEvents
                        .Where(e => pageIds.Contains(e.ExternalId))
                        .ToDictionaryAsync(e => e.ExternalId);

                    var newEvents = new List<SportsEvent>();

                    foreach (var item in pageItems)
                    {
                        string id = GetStringSafe(item, "id") ?? "";
                        if (string.IsNullOrEmpty(id)) continue;

                        foundIds.Add(id);

                        string home = "", away = "", leagueName = "Outros";
                        string? rawHomeId = null, rawAwayId = null, rawLeagueId = null, rawCc = null;

                        if (item.TryGetProperty("league", out var lObj))
                        {
                            leagueName = GetStringSafe(lObj, "name") ?? "Outros";
                            rawLeagueId = GetStringSafe(lObj, "id");
                            rawCc = GetStringSafe(lObj, "cc");
                        }

                        if (item.TryGetProperty("home", out var hObj))
                        {
                            home = GetStringSafe(hObj, "name") ?? "";
                            rawHomeId = GetStringSafe(hObj, "id");
                        }
                        if (item.TryGetProperty("away", out var aObj))
                        {
                            away = GetStringSafe(aObj, "name") ?? "";
                            rawAwayId = GetStringSafe(aObj, "id");
                        }

                        if (IsFakeSport(leagueName, home, away)) continue;
                        if (disabledLeagues.Contains(leagueName)) continue;
                        if ((rawHomeId != null && disabledTeams.Contains(rawHomeId)) || (rawAwayId != null && disabledTeams.Contains(rawAwayId))) continue;

                        long.TryParse(GetStringSafe(item, "time"), out long timeUnix);
                        var commenceTime = DateTimeOffset.FromUnixTimeSeconds(timeUnix).UtcDateTime;

                        string apiStatus = GetStringSafe(item, "time_status");
                        string dbStatus = (apiStatus == "1") ? "Live" : "Prematch";

                        if (existingGamesDict.TryGetValue(id, out var existingEvent))
                        {
                            if (existingEvent.CommenceTime != commenceTime)
                            {
                                existingEvent.CommenceTime = commenceTime;
                                existingEvent.LastUpdate = DateTime.UtcNow;
                            }

                            if (string.IsNullOrEmpty(existingEvent.LeagueId) && IsValidImageId(rawLeagueId))
                            {
                                existingEvent.LeagueId = rawLeagueId;
                                existingEvent.LeagueExternalId = rawLeagueId;
                            }
                            if (string.IsNullOrEmpty(existingEvent.HomeTeamId) && IsValidImageId(rawHomeId)) existingEvent.HomeTeamId = rawHomeId;
                            if (string.IsNullOrEmpty(existingEvent.AwayTeamId) && IsValidImageId(rawAwayId)) existingEvent.AwayTeamId = rawAwayId;

                            if (dbStatus == "Live" && existingEvent.Status != "Live") existingEvent.Status = "Live";
                        }
                        else
                        {
                            newEvents.Add(new SportsEvent
                            {
                                ExternalId = id,
                                HomeTeam = home,
                                AwayTeam = away,
                                League = leagueName,
                                CommenceTime = commenceTime,
                                SportKey = sportKey,
                                SportCategory = sportKey,
                                OddsSource = "BetsAPI_Premium",
                                LastUpdate = DateTime.UtcNow,
                                HomeTeamId = IsValidImageId(rawHomeId) ? rawHomeId : null,
                                AwayTeamId = IsValidImageId(rawAwayId) ? rawAwayId : null,
                                LeagueId = IsValidImageId(rawLeagueId) ? rawLeagueId : null,
                                LeagueExternalId = IsValidImageId(rawLeagueId) ? rawLeagueId : null,
                                CountryCode = !string.IsNullOrEmpty(rawCc) ? rawCc : null,
                                HomeTeamLogo = IsValidImageId(rawHomeId) ? $"https://assets.b365api.com/images/team/m/{rawHomeId}.png" : null,
                                AwayTeamLogo = IsValidImageId(rawAwayId) ? $"https://assets.b365api.com/images/team/m/{rawAwayId}.png" : null,
                                Status = dbStatus,
                                Odds = new List<EventMarket>()
                            });
                        }
                    }

                    if (newEvents.Any()) context.SportsEvents.AddRange(newEvents);
                    if (newEvents.Any() || context.ChangeTracker.HasChanges()) await context.SaveChangesAsync();
                }
            }
            catch { }
            return foundIds;
        }

        // ==============================================================================
        // 🔧 INTERFACE & WORKER DE ODDS
        // ==============================================================================
        public async Task SyncBaseOddsToDatabase() => await SyncEventsSchedule();

        public async Task SyncPrematchOdds()
        {
            Console.WriteLine("♻️ [RECOVERY] Iniciando varredura de odds...");
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var gamesToUpdate = await context.SportsEvents
                    .AsNoTracking()
                    .Where(g => g.CommenceTime > DateTime.UtcNow &&
                               (g.RawOddsHome == 0 || g.LastUpdate < DateTime.UtcNow.AddHours(-1)))
                    .OrderBy(g => g.CommenceTime)
                    .Take(400)
                    .Select(g => new { g.ExternalId, g.SportKey })
                    .ToListAsync();

                if (gamesToUpdate.Any())
                {
                    Console.WriteLine($"♻️ [RECOVERY] {gamesToUpdate.Count} jogos pendentes.");
                    var grouped = gamesToUpdate.GroupBy(g => g.SportKey ?? "soccer");
                    foreach (var grp in grouped)
                    {
                        var ids = grp.Select(x => x.ExternalId).ToList();
                        await FetchOddsList(ids, grp.Key);
                    }
                }
            }
        }

        private async Task FetchOddsList(List<string> allIds, string sportKey)
        {
            var chunks = allIds.Chunk(REQUEST_CHUNK_SIZE).ToList();
            foreach (var chunk in chunks)
            {
                await ProcessBatchWithRetries(chunk.ToList(), sportKey);
            }
        }

        private async Task ProcessBatchWithRetries(List<string> batchIds, string sportKey, int retryLevel = 0)
        {
            if (batchIds.Count == 0) return;

            var idsStr = string.Join(",", batchIds);
            var url = $"{BASE_URL}/v1/bet365/prematch?token={_token}&FI={idsStr}";

            await Task.Delay(500 + (retryLevel * 1000));
            await _localGate.WaitAsync();

            try
            {
                var response = await _httpClient.GetAsync(url);

                if ((int)response.StatusCode == 429)
                {
                    Console.WriteLine($"⛔ [ODDS 429] Lote bloqueado. Dividindo...");
                    _localGate.Release();

                    if (batchIds.Count > 1) await SplitAndRetry(batchIds, sportKey, retryLevel + 1);
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _localGate.Release();
                    if (batchIds.Count > 1) await SplitAndRetry(batchIds, sportKey, retryLevel + 1);
                    return;
                }

                var jsonString = await response.Content.ReadAsStringAsync();

                if (jsonString.Contains("failure") || jsonString.Contains("PARAM_INVALID"))
                {
                    Console.WriteLine($"⚠️ [API] Lote contaminado. Dividindo...");
                    _localGate.Release();
                    if (batchIds.Count > 1) await SplitAndRetry(batchIds, sportKey, retryLevel + 1);
                    return;
                }

                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    try
                    {
                        using var doc = JsonDocument.Parse(jsonString);
                        if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                        {
                            await ProcessOddsResults(context, results, sportKey);
                        }
                    }
                    catch (JsonException)
                    {
                        _localGate.Release();
                        if (batchIds.Count > 1) await SplitAndRetry(batchIds, sportKey, retryLevel + 1);
                        return;
                    }
                }
                _localGate.Release();
            }
            catch
            {
                if (_localGate.CurrentCount == 0) _localGate.Release();
                if (batchIds.Count > 1) await SplitAndRetry(batchIds, sportKey, retryLevel + 1);
            }
        }

        private async Task SplitAndRetry(List<string> ids, string sportKey, int nextLevel)
        {
            int mid = ids.Count / 2;
            await ProcessBatchWithRetries(ids.Take(mid).ToList(), sportKey, nextLevel);
            await ProcessBatchWithRetries(ids.Skip(mid).ToList(), sportKey, nextLevel);
        }

        private async Task ProcessOddsResults(AppDbContext context, JsonElement results, string sportKey)
        {
            var ids = new List<string>();
            foreach (var item in results.EnumerateArray())
            {
                string fi = GetStringSafe(item, "FI");
                if (!string.IsNullOrEmpty(fi)) ids.Add(fi);
            }

            var dbEvents = await context.SportsEvents.Include(e => e.Odds).Where(e => ids.Contains(e.ExternalId)).ToListAsync();

            foreach (var item in results.EnumerateArray())
            {
                string fi = GetStringSafe(item, "FI");
                var game = dbEvents.FirstOrDefault(e => e.ExternalId == fi);
                if (game == null) continue;

                ApplyOddsToGame(item, game, context);
                game.LastUpdate = DateTime.UtcNow;
            }
            await context.SaveChangesAsync();
        }

        // ==============================================================================
        // SYNC IMAGENS
        // ==============================================================================
        public async Task SyncMissingImages()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var jogosSemImagem = await context.SportsEvents
                    .Where(g => g.CommenceTime > DateTime.UtcNow && (g.HomeTeamId == null || g.LeagueId == null))
                    .OrderBy(g => g.CommenceTime).Take(20).ToListAsync();

                if (!jogosSemImagem.Any()) return;

                foreach (var game in jogosSemImagem)
                {
                    await Task.Delay(500);
                    var url = $"{BASE_URL}/v1/bet365/result?token={_token}&event_id={game.ExternalId}";
                    var response = await _httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                        if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in results.EnumerateArray())
                            {
                                if (item.TryGetProperty("home", out var h))
                                {
                                    var hid = GetStringSafe(h, "id");
                                    if (IsValidImageId(hid)) { game.HomeTeamId = hid; game.HomeTeamLogo = $"https://assets.b365api.com/images/team/m/{hid}.png"; }
                                }
                                if (item.TryGetProperty("away", out var a))
                                {
                                    var aid = GetStringSafe(a, "id");
                                    if (IsValidImageId(aid)) { game.AwayTeamId = aid; game.AwayTeamLogo = $"https://assets.b365api.com/images/team/m/{aid}.png"; }
                                }
                                if (item.TryGetProperty("league", out var l))
                                {
                                    var lid = GetStringSafe(l, "id");
                                    if (IsValidImageId(lid)) game.LeagueId = lid;
                                    var cc = GetStringSafe(l, "cc");
                                    if (!string.IsNullOrEmpty(cc)) game.CountryCode = cc;
                                }
                            }
                        }
                    }
                }
                await context.SaveChangesAsync();
            }
            catch { }
        }

        // ==============================================================================
        // ✅ NOVA LÓGICA DE PARSE FATORADA (USANDO A PASTA PARSERS)
        // ==============================================================================
        private void ApplyOddsToGame(JsonElement eventNode, SportsEvent game, AppDbContext context)
        {
            var moneyLineMarkets = new List<MarketDto>();

            // 1. Extrai todos os mercados usando os Parsers especialistas
            foreach (var parser in _parsers)
            {
                var parsedMarkets = parser.Parse(eventNode, game.SportKey ?? "soccer");

                foreach (var m in parsedMarkets)
                {
                    // Atualiza ou Insere no banco de dados (EventMarket)
                    var existing = game.Odds.FirstOrDefault(o => o.MarketName == m.MarketName && o.OutcomeName == m.OutcomeName);

                    if (existing == null)
                    {
                        existing = context.EventMarkets.Local.FirstOrDefault(x => x.SportsEventId == game.Id && x.MarketName == m.MarketName && x.OutcomeName == m.OutcomeName);
                    }

                    if (existing != null)
                    {
                        existing.Price = m.Price;
                        existing.LastUpdate = DateTime.UtcNow;
                    }
                    else
                    {
                        var newOdd = new EventMarket
                        {
                            SportsEventId = game.Id,
                            ExternalId = m.ExternalId,
                            MarketName = m.MarketName,
                            OutcomeName = m.OutcomeName,
                            Price = m.Price,
                            Handicap = m.Handicap,
                            LastUpdate = DateTime.UtcNow
                        };
                        game.Odds.Add(newOdd);
                    }

                    // Filtra mercados para atualizar as colunas rápidas RawOdds (1x2 / Money Line)
                    string mName = m.MarketName?.Trim().ToUpper() ?? "";
                    if (mName.Contains("MONEY LINE") || mName.Contains("WINNER") || mName.Contains("VENCEDOR") ||
                        mName.Contains("RESULT") || mName.Contains("FULL") || mName.Contains("1X2") || mName == "12")
                    {
                        moneyLineMarkets.Add(m);
                    }
                }
            }

            // 2. Atualiza as colunas de acesso rápido (RawOddsHome/Draw/Away)
            bool foundByLabel = false;
            foreach (var m in moneyLineMarkets)
            {
                if (IsHome(m.OutcomeName, game.HomeTeam)) { game.RawOddsHome = m.Price; foundByLabel = true; }
                else if (IsDraw(m.OutcomeName)) { game.RawOddsDraw = m.Price; foundByLabel = true; }
                else if (IsAway(m.OutcomeName, game.AwayTeam)) { game.RawOddsAway = m.Price; foundByLabel = true; }
            }

            // Fallback (Ordem de aparição) caso o nome do time não case perfeitamente com a OutcomeName
            if (!foundByLabel && moneyLineMarkets.Count >= 2)
            {
                game.RawOddsHome = moneyLineMarkets[0].Price;
                game.RawOddsAway = moneyLineMarkets[1].Price;
                if (moneyLineMarkets.Count == 3)
                {
                    game.RawOddsDraw = moneyLineMarkets[1].Price;
                    game.RawOddsAway = moneyLineMarkets[2].Price;
                }
            }
        }

        // ==============================================================================
        // AUXILIARES DE PARSE (Identificação de Times e Empate)
        // ==============================================================================
        private bool IsHome(string outcome, string homeTeam)
        {
            if (string.IsNullOrEmpty(outcome)) return false;
            var o = outcome.ToLower().Trim();
            var h = homeTeam?.ToLower().Trim() ?? "xxx";
            return o == "1" || o == "home" || o == "casa" || o == h || o.Contains(h) || h.Contains(o);
        }

        private bool IsAway(string outcome, string awayTeam)
        {
            if (string.IsNullOrEmpty(outcome)) return false;
            var o = outcome.ToLower().Trim();
            var a = awayTeam?.ToLower().Trim() ?? "xxx";
            return o == "2" || o == "away" || o == "fora" || o == a || o.Contains(a) || a.Contains(o);
        }

        private bool IsDraw(string outcome)
        {
            if (string.IsNullOrEmpty(outcome)) return false;
            var o = outcome.ToLower().Trim();
            return o == "x" || o == "draw" || o == "empate";
        }

        private string? GetStringSafe(JsonElement el, string key) => el.TryGetProperty(key, out var p) ? (p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString()) : null;

        private bool IsFakeSport(string l, string h, string a)
        {
            var text = $"{l} {h} {a}".ToUpper();
            return _excludedTerms.Any(term => text.Contains(term));
        }
    }
}