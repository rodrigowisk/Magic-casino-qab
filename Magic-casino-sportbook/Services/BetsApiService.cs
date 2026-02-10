using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Data.Models;
using Magic_casino_sportbook.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Linq;

namespace Magic_casino_sportbook.Services
{
    public class BetsApiService : IOddsService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly BetsApiGatekeeper _gatekeeper;
        private readonly string _token;

        private const string BASE_URL = "https://api.b365api.com";
        private const int MAX_PAGES_PER_SPORT = 20;
        private const int REQUEST_CHUNK_SIZE = 10;

        private readonly Dictionary<int, string> _sportMap = new() {
            { 1, "soccer" }, { 13, "tennis" }, { 18, "basketball" }, { 91, "volleyball" },
            { 12, "american-football" }, { 16, "baseball" }, { 17, "ice-hockey" },
            { 78, "handball" }, { 8, "rugby-union" }, { 9, "boxing" }, { 3, "cricket" },
            { 92, "table-tennis" }, { 94, "badminton" }, { 95, "futsal" },
            { 19, "snooker" }, { 14, "darts" }, { 36, "australian-rules" }, { 151, "esports" }
        };

        public BetsApiService(
            HttpClient httpClient,
            IServiceScopeFactory scopeFactory,
            IHubContext<GameHub> hubContext,
            BetsApiGatekeeper gatekeeper)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _gatekeeper = gatekeeper;
            _token = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";
        }

        private bool IsValidImageId(string? id) => !string.IsNullOrEmpty(id) && id != "0";

        // ==============================================================================
        // 📅 ROBÔ DE GRADE + ODDS (Fluxo Rápido)
        // ==============================================================================
        public async Task SyncEventsSchedule()
        {
            if (string.IsNullOrEmpty(_token)) return;
            Console.WriteLine($"📅 [SYNC] Iniciando ciclo (Gatekeeper 200ms)...");

            var dbSemaphore = new SemaphoreSlim(1, 1);

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

                    // Busca Grade
                    for (int page = 1; page <= MAX_PAGES_PER_SPORT; page++)
                    {
                        try
                        {
                            var idsFound = await ProcessSportPageAsync(dbSemaphore, sport.Key, sport.Value, page, disabledLeagues, disabledTeams);
                            if (!idsFound.Any()) break;
                            idsToFetchOdds.AddRange(idsFound);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Erro ao processar página {page} de {sport.Value}: {ex.Message}");
                            // Continua para o próximo esporte em vez de quebrar tudo
                            break;
                        }
                    }

                    // Busca Odds (Lotes)
                    if (idsToFetchOdds.Any())
                    {
                        var uniqueIds = idsToFetchOdds.Distinct().ToList();
                        Console.WriteLine($"💰 [ODDS] {sport.Value}: Buscando odds para {uniqueIds.Count} jogos...");
                        await FetchOddsList(uniqueIds);
                    }
                }
            }

            await SyncMissingImages();
            Console.WriteLine("✅ [SYNC] Ciclo Finalizado!");
        }

        private async Task<List<string>> ProcessSportPageAsync(SemaphoreSlim dbLock, int sportId, string sportKey, int page, HashSet<string> disabledLeagues, HashSet<string> disabledTeams)
        {
            var foundIds = new List<string>();
            var url = $"{BASE_URL}/v1/bet365/upcoming?sport_id={sportId}&token={_token}&page={page}";

            var response = await _gatekeeper.ExecuteAsync(async () => await _httpClient.GetAsync(url));
            if (!response.IsSuccessStatusCode) return foundIds;

            try
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                JsonElement results = default;
                if (doc.RootElement.ValueKind == JsonValueKind.Array) results = doc.RootElement;
                else if (doc.RootElement.TryGetProperty("results", out var res)) results = res;

                if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return foundIds;

                await dbLock.WaitAsync();
                try
                {
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

                            foundIds.Add(id); // Guarda ID para buscar odd depois

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

                            if (IsFakeSport(leagueName, home, away, sportKey)) continue;
                            if (disabledLeagues.Contains(leagueName)) continue;
                            if ((rawHomeId != null && disabledTeams.Contains(rawHomeId)) || (rawAwayId != null && disabledTeams.Contains(rawAwayId))) continue;

                            long.TryParse(GetStringSafe(item, "time"), out long timeUnix);
                            var commenceTime = DateTimeOffset.FromUnixTimeSeconds(timeUnix).UtcDateTime;

                            if (existingGamesDict.TryGetValue(id, out var existingEvent))
                            {
                                if (existingEvent.CommenceTime != commenceTime)
                                {
                                    existingEvent.CommenceTime = commenceTime;
                                    existingEvent.LastUpdate = DateTime.UtcNow;
                                }
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
                                    CountryCode = !string.IsNullOrEmpty(rawCc) ? rawCc : null,
                                    HomeTeamLogo = IsValidImageId(rawHomeId) ? $"https://assets.b365api.com/images/team/m/{rawHomeId}.png" : null,
                                    AwayTeamLogo = IsValidImageId(rawAwayId) ? $"https://assets.b365api.com/images/team/m/{rawAwayId}.png" : null,
                                    Status = "Prematch",
                                    Odds = new List<EventMarket>()
                                });
                            }
                        }

                        if (newEvents.Any()) context.SportsEvents.AddRange(newEvents);
                        if (newEvents.Any() || context.ChangeTracker.HasChanges()) await context.SaveChangesAsync();
                    }
                }
                finally { dbLock.Release(); }
            }
            catch { }
            return foundIds;
        }

        // ==============================================================================
        // 🔧 FIX: Implementação Correta do Worker de Odds
        // ==============================================================================
        public async Task SyncPrematchOdds()
        {
            Console.WriteLine("♻️ [RECOVERY] Iniciando varredura de jogos sem odds ou desatualizados...");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Busca IDs de jogos que:
                // 1. Vão acontecer no futuro
                // 2. Estão com Odd da casa zerada OU não são atualizados há mais de 1 hora
                var gamesToUpdate = await context.SportsEvents
                    .AsNoTracking()
                    .Where(g => g.CommenceTime > DateTime.UtcNow &&
                               (g.RawOddsHome == 0 || g.LastUpdate < DateTime.UtcNow.AddHours(-1)))
                    .OrderBy(g => g.CommenceTime) // Prioriza os mais próximos
                    .Take(400) // Pega em lotes de 400 para não travar o banco
                    .Select(g => g.ExternalId)
                    .ToListAsync();

                if (gamesToUpdate.Any())
                {
                    Console.WriteLine($"♻️ [RECOVERY] Encontrados {gamesToUpdate.Count} jogos precisando de odds. Buscando...");

                    // Reutiliza a função de busca em lotes
                    await FetchOddsList(gamesToUpdate);
                }
                else
                {
                    Console.WriteLine("✅ [RECOVERY] Nenhum jogo pendente de atualização.");
                }
            }
        }

        // ==============================================================================
        // BUSCA DE ODDS: LOTE + RESGATE + FALLBACK INTELIGENTE
        // ==============================================================================
        private async Task FetchOddsList(List<string> allIds)
        {
            var chunks = allIds.Chunk(REQUEST_CHUNK_SIZE).ToList();

            foreach (var chunk in chunks)
            {
                var idsStr = string.Join(",", chunk);
                var url = $"{BASE_URL}/v1/bet365/prematch?token={_token}&FI={idsStr}";
                bool batchSuccess = false;

                try
                {
                    var response = await _gatekeeper.ExecuteAsync(async () => await _httpClient.GetAsync(url));
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();
                        if (!jsonString.Trim().StartsWith("error"))
                        {
                            using var doc = JsonDocument.Parse(jsonString);
                            if (doc.RootElement.TryGetProperty("results", out var resultsArray) && resultsArray.ValueKind == JsonValueKind.Array)
                            {
                                using var scope = _scopeFactory.CreateScope();
                                var localContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                                var dbGames = await localContext.SportsEvents.Include(e => e.Odds).Where(e => chunk.Contains(e.ExternalId)).ToListAsync();

                                foreach (var eventNode in resultsArray.EnumerateArray())
                                {
                                    string fi = GetStringSafe(eventNode, "FI");
                                    var targetGame = dbGames.FirstOrDefault(g => g.ExternalId == fi);
                                    if (targetGame != null)
                                    {
                                        ApplyOddsToGame(eventNode, targetGame, localContext);
                                        targetGame.LastUpdate = DateTime.UtcNow;
                                    }
                                }
                                await localContext.SaveChangesAsync();
                                batchSuccess = true;
                            }
                        }
                    }
                }
                catch { batchSuccess = false; }

                // MODO RESGATE: Salva 1 a 1 se o lote falhar
                if (!batchSuccess)
                {
                    Console.WriteLine($"⚠️ Falha Lote ({chunk.Length} jogos). Resgatando 1 a 1...");
                    foreach (var gameId in chunk) await ProcessOddsSingle(gameId);
                }
            }
        }

        private async Task ProcessOddsSingle(string gameId)
        {
            var url = $"{BASE_URL}/v1/bet365/prematch?token={_token}&FI={gameId}";
            try
            {
                var response = await _gatekeeper.ExecuteAsync(async () => await _httpClient.GetAsync(url));
                if (!response.IsSuccessStatusCode) return;

                var jsonString = await response.Content.ReadAsStringAsync();
                if (jsonString.Trim().StartsWith("error")) return;

                using var doc = JsonDocument.Parse(jsonString);
                JsonElement root = doc.RootElement;
                JsonElement results = root;
                if (root.TryGetProperty("results", out var resProp)) results = resProp;

                if (results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in results.EnumerateArray())
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var game = await context.SportsEvents.Include(e => e.Odds).FirstOrDefaultAsync(g => g.ExternalId == gameId);

                        if (game == null) return;

                        ApplyOddsToGame(item, game, context);
                        game.LastUpdate = DateTime.UtcNow;

                        // Retry de salvamento individual
                        int retries = 3;
                        while (retries > 0)
                        {
                            try { await context.SaveChangesAsync(); break; }
                            catch (DbUpdateConcurrencyException)
                            {
                                retries--;
                                foreach (var entry in context.ChangeTracker.Entries()) await entry.ReloadAsync();
                            }
                            catch { break; }
                        }
                    }
                }
            }
            catch { }
        }

        // ==============================================================================
        // 🧠 APLICAÇÃO DE ODDS (PADRÃO + FALLBACK)
        // ==============================================================================
        private void ApplyOddsToGame(JsonElement eventNode, SportsEvent game, AppDbContext context)
        {
            bool scheduleFound = false;

            // 1. TENTA SCHEDULE (PADRÃO 10 ANOS)
            if (eventNode.TryGetProperty("schedule", out var schedObj) &&
                schedObj.TryGetProperty("sp", out var schedSp) &&
                schedSp.TryGetProperty("main", out var mainOddsArray))
            {
                foreach (var odd in mainOddsArray.EnumerateArray())
                {
                    string name = GetStringSafe(odd, "name");
                    decimal price = ParseFractionalOrDecimal(GetStringSafe(odd, "odds"));

                    if (price > 1)
                    {
                        if (name == "1") game.RawOddsHome = price;
                        else if (name == "X") game.RawOddsDraw = price;
                        else if (name == "2") game.RawOddsAway = price;
                        AddHistoryOdd(game, "Match Winner", name, price, context);
                        scheduleFound = true;
                    }
                }
            }

            // 2. TENTA FULL TIME RESULT (FUTEBOL)
            if (!scheduleFound && eventNode.TryGetProperty("main", out var mainObj) &&
                mainObj.TryGetProperty("sp", out var mainSp) &&
                mainSp.TryGetProperty("full_time_result", out var ftrArray))
            {
                var ftrList = ftrArray.EnumerateArray().ToList();
                if (ftrList.Count == 3)
                {
                    decimal h = ParseFractionalOrDecimal(GetStringSafe(ftrList[0], "odds"));
                    decimal d = ParseFractionalOrDecimal(GetStringSafe(ftrList[1], "odds"));
                    decimal a = ParseFractionalOrDecimal(GetStringSafe(ftrList[2], "odds"));

                    if (h > 1) game.RawOddsHome = h;
                    if (d > 1) game.RawOddsDraw = d;
                    if (a > 1) game.RawOddsAway = a;
                }
            }

            // 3. PROCESSA TODOS OS DETALHES
            // Essa função retorna a lista de tudo o que achou
            var allMarkets = ProcessAllMarketsRecursively(eventNode, game, context);

            // 4. PLANO C: FALLBACK INTELIGENTE (Se ainda estiver zerado)
            if (game.RawOddsHome <= 1)
            {
                TryFindWinnerInDetails(game, allMarkets);
            }
        }

        private void TryFindWinnerInDetails(SportsEvent game, List<EventMarket> markets)
        {
            // Procura mercados tipo "Vencedor"
            var winnerMarket = markets.FirstOrDefault(m =>
                m.MarketName.ToLower().Contains("winner") ||
                m.MarketName.ToLower().Contains("money") ||
                m.MarketName == "1x2");

            if (winnerMarket == null) return;

            var odds = markets.Where(m => m.MarketName == winnerMarket.MarketName).ToList();
            string hTeam = (game.HomeTeam ?? "").ToLower().Trim();
            string aTeam = (game.AwayTeam ?? "").ToLower().Trim();

            foreach (var odd in odds)
            {
                string outcome = odd.OutcomeName.ToLower().Trim();
                if (outcome == "1" || outcome == hTeam || outcome.StartsWith(hTeam)) game.RawOddsHome = odd.Price;
                else if (outcome == "2" || outcome == aTeam || outcome.StartsWith(aTeam)) game.RawOddsAway = odd.Price;
                else if (outcome == "x" || outcome == "draw") game.RawOddsDraw = odd.Price;
            }
        }

        private List<EventMarket> ProcessAllMarketsRecursively(JsonElement element, SportsEvent game, AppDbContext context)
        {
            var detected = new List<EventMarket>();

            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("sp", out var spObj))
                {
                    foreach (var prop in spObj.EnumerateObject())
                    {
                        string marketName = prop.Name;
                        var oddsNode = prop.Value;
                        if (oddsNode.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var odd in oddsNode.EnumerateArray())
                            {
                                decimal price = ParseFractionalOrDecimal(GetStringSafe(odd, "odds"));
                                if (price <= 1) continue;
                                string name = GetStringSafe(odd, "name") ?? "";
                                string header = GetStringSafe(odd, "header") ?? "";
                                string handicap = GetStringSafe(odd, "handicap");
                                string outcomeName = name;
                                if (!string.IsNullOrEmpty(header) && header.Trim() != ".") outcomeName = string.IsNullOrEmpty(name) ? header : $"{header} {name}";
                                if (!string.IsNullOrEmpty(handicap)) outcomeName += $" {handicap}";

                                var mObj = AddHistoryOdd(game, marketName, outcomeName.Trim(), price, context, handicap);
                                detected.Add(mObj);
                            }
                        }
                    }
                }
                foreach (var prop in element.EnumerateObject()) detected.AddRange(ProcessAllMarketsRecursively(prop.Value, game, context));
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray()) detected.AddRange(ProcessAllMarketsRecursively(item, game, context));
            }
            return detected;
        }

        private EventMarket AddHistoryOdd(SportsEvent game, string market, string outcome, decimal price, AppDbContext context, string handicap = null)
        {
            var existingOdd = game.Odds.FirstOrDefault(o => o.MarketName == market && o.OutcomeName == outcome);
            if (existingOdd == null) existingOdd = context.EventMarkets.Local.FirstOrDefault(m => m.SportsEventId == game.Id && m.MarketName == market && m.OutcomeName == outcome);

            if (existingOdd != null)
            {
                if (existingOdd.Price != price) { existingOdd.Price = price; existingOdd.LastUpdate = DateTime.UtcNow; }
                return existingOdd;
            }
            else
            {
                decimal? hVal = null;
                if (!string.IsNullOrEmpty(handicap) && decimal.TryParse(handicap, NumberStyles.Any, CultureInfo.InvariantCulture, out var h)) hVal = h;

                var newOdd = new EventMarket { SportsEventId = game.Id, MarketName = market, OutcomeName = outcome, Price = price, Handicap = hVal, LastUpdate = DateTime.UtcNow };
                game.Odds.Add(newOdd);
                return newOdd;
            }
        }

        private string? GetStringSafe(JsonElement el, string key) => el.TryGetProperty(key, out var p) ? (p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString()) : null;

        private decimal ParseFractionalOrDecimal(string? val)
        {
            if (string.IsNullOrEmpty(val)) return 0;
            try
            {
                if (val.Contains("/"))
                {
                    var p = val.Split('/');
                    if (p.Length == 2 && decimal.TryParse(p[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var num)
                                      && decimal.TryParse(p[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var den)
                                      && den != 0)
                    {
                        return (num / den) + 1;
                    }
                    return 0;
                }
                return decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0;
            }
            catch { return 0; }
        }

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

                foreach (var game in jogosSemImagem)
                {
                    var url = $"{BASE_URL}/bet365/result?token={_token}&event_id={game.ExternalId}";
                    var response = await _gatekeeper.ExecuteAsync(async () => await _httpClient.GetAsync(url));

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                        if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in results.EnumerateArray())
                            {
                                if (item.TryGetProperty("home", out var h) && IsValidImageId(GetStringSafe(h, "image_id"))) { game.HomeTeamId = GetStringSafe(h, "image_id"); game.HomeTeamLogo = $"https://assets.b365api.com/images/team/m/{game.HomeTeamId}.png"; }
                                if (item.TryGetProperty("away", out var a) && IsValidImageId(GetStringSafe(a, "image_id"))) { game.AwayTeamId = GetStringSafe(a, "image_id"); game.AwayTeamLogo = $"https://assets.b365api.com/images/team/m/{game.AwayTeamId}.png"; }
                                if (item.TryGetProperty("league", out var l))
                                {
                                    if (IsValidImageId(GetStringSafe(l, "image_id"))) game.LeagueId = GetStringSafe(l, "image_id");
                                    if (!string.IsNullOrEmpty(GetStringSafe(l, "cc"))) game.CountryCode = GetStringSafe(l, "cc");
                                }
                            }
                        }
                    }
                }
                await context.SaveChangesAsync();
            }
            catch { }
        }
    }
}