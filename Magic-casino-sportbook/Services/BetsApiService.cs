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
        // Mantemos 10 páginas para não estourar o limite da API, já que agora são muitos esportes
        private const int MAX_PAGES_PER_SPORT = 10;

        // ✅ LISTA COMPLETA DE ESPORTES PARA O ROBÔ
        // O robô vai tentar buscar todos estes, a menos que você bloqueie no Admin.
        private readonly Dictionary<int, string> _sportMap = new() {
            { 1, "soccer" },                // Futebol
            { 13, "tennis" },               // Tênis
            { 18, "basketball" },           // Basquete
            { 91, "volleyball" },           // Vôlei
            { 12, "american-football" },    // Futebol Americano
            { 16, "baseball" },             // Beisebol
            { 17, "ice-hockey" },           // Hóquei no Gelo
            { 78, "handball" },             // Handebol
            { 8, "rugby-union" },           // Rugby
            { 9, "boxing" },                // Boxe/UFC
            { 3, "cricket" },               // Críquete
            { 92, "table-tennis" },         // Tênis de Mesa
            { 94, "badminton" },            // Badminton
            { 95, "futsal" },               // Futsal
            { 19, "snooker" },              // Sinuca
            { 14, "darts" },                // Dardos
            { 36, "australian-rules" },     // Futebol Australiano
            { 151, "esports" }              // E-Sports (Geral)
        };

        public BetsApiService(HttpClient httpClient, IServiceScopeFactory scopeFactory, IHubContext<GameHub> hubContext)
        {
            _httpClient = httpClient;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _token = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";
        }

        // ==============================================================================
        // 📅 ROBÔ 1: CALENDÁRIO (SÓ EVENTOS, SEM ODDS) - Roda a cada 6h
        // ==============================================================================
        public async Task SyncEventsSchedule()
        {
            if (string.IsNullOrEmpty(_token)) return;

            Console.WriteLine($"📅 [CALENDÁRIO] Iniciando varredura de novos eventos...");

            // Aumentei o semáforo para 8 threads para processar a lista maior de esportes mais rápido
            using var semaphore = new SemaphoreSlim(8);
            var tasks = new List<Task>();

            using (var configScope = _scopeFactory.CreateScope())
            {
                var configContext = configScope.ServiceProvider.GetRequiredService<AppDbContext>();

                // 1. Carrega configurações de bloqueio do ADMIN
                var disabledConfigs = await configContext.SportConfigurations
                    .AsNoTracking()
                    .Where(c => c.IsEnabled == false)
                    .ToListAsync();

                var disabledSports = new HashSet<string>(disabledConfigs.Where(c => c.Type == "SPORT").Select(c => c.Identifier));
                var disabledLeagues = new HashSet<string>(disabledConfigs.Where(c => c.Type == "LEAGUE").Select(c => c.Identifier));
                var disabledTeams = new HashSet<string>(disabledConfigs.Where(c => c.Type == "TEAM").Select(c => c.Identifier));

                foreach (var sport in _sportMap)
                {
                    // 2. SE O ESPORTE ESTIVER BLOQUEADO NO ADMIN, O ROBÔ NEM TENTA BUSCAR
                    if (disabledSports.Contains(sport.Value))
                    {
                        Console.WriteLine($"⛔ [CALENDÁRIO] Ignorando {sport.Value} (Bloqueado no Admin).");
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

                            // Busca apenas a estrutura dos eventos
                            await FetchUpcomingEventsOnly(sport.Key, sport.Value, dbContext, disabledLeagues, disabledTeams);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Erro Calendário {sport.Value}: {ex.Message}");
                        }
                        finally { semaphore.Release(); }
                    }));
                }
            }

            await Task.WhenAll(tasks);

            // Aproveita o ciclo do calendário para baixar imagens faltantes
            await SyncMissingImages();

            Console.WriteLine("✅ [CALENDÁRIO] Sincronização finalizada!");
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
                    var existingGamesDict = await context.SportsEvents
                        .Where(e => pageIds.Contains(e.ExternalId))
                        .ToDictionaryAsync(e => e.ExternalId);

                    foreach (var item in pageItems)
                    {
                        string id = GetStringSafe(item, "id") ?? "";
                        if (string.IsNullOrEmpty(id)) continue;

                        string home = "", away = "", leagueName = "Outros";
                        string? homeId = null, awayId = null, leagueId = null;

                        if (item.TryGetProperty("home", out var hObj)) { home = GetStringSafe(hObj, "name") ?? ""; homeId = GetStringSafe(hObj, "image_id"); }
                        if (item.TryGetProperty("away", out var aObj)) { away = GetStringSafe(aObj, "name") ?? ""; awayId = GetStringSafe(aObj, "image_id"); }
                        if (item.TryGetProperty("league", out var lObj)) { leagueName = GetStringSafe(lObj, "name") ?? "Outros"; leagueId = GetStringSafe(lObj, "image_id"); }

                        if (IsFakeSport(leagueName, home, away, sportKey)) continue;

                        // Filtros de Ligas e Times (Admin)
                        if (disabledLeagues.Contains(leagueName)) continue;
                        if ((homeId != null && disabledTeams.Contains(homeId)) || (awayId != null && disabledTeams.Contains(awayId))) continue;

                        long.TryParse(GetStringSafe(item, "time"), out long timeUnix);

                        if (existingGamesDict.TryGetValue(id, out var existingEvent))
                        {
                            bool changed = false;
                            if (existingEvent.League != leagueName) { existingEvent.League = leagueName; changed = true; }

                            if (existingEvent.HomeTeamId == null && homeId != null)
                            {
                                existingEvent.HomeTeamId = homeId;
                                existingEvent.HomeTeamLogo = $"https://assets.b365api.com/images/team/m/{homeId}.png";
                                changed = true;
                            }
                            if (existingEvent.AwayTeamId == null && awayId != null)
                            {
                                existingEvent.AwayTeamId = awayId;
                                existingEvent.AwayTeamLogo = $"https://assets.b365api.com/images/team/m/{awayId}.png";
                                changed = true;
                            }

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
                                HomeTeamLogo = homeId != null ? $"https://assets.b365api.com/images/team/m/{homeId}.png" : null,
                                AwayTeamLogo = awayId != null ? $"https://assets.b365api.com/images/team/m/{awayId}.png" : null,
                                Status = "Prematch",
                                Odds = new List<MarketOdd>()
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

        // ==============================================================================
        // 💰 ROBÔ 2: ODDS PRÉ-JOGO (SÓ ODDS, A CADA 20 MIN)
        // ==============================================================================
        public async Task SyncPrematchOdds()
        {
            if (string.IsNullOrEmpty(_token)) return;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var upcomingGames = await context.SportsEvents
                .Where(e => e.CommenceTime > DateTime.UtcNow
                         && e.CommenceTime < DateTime.UtcNow.AddHours(24)
                         && (e.Status == "Prematch" || e.Status == null))
                .OrderBy(e => e.CommenceTime)
                .Take(50)
                .ToListAsync();

            if (!upcomingGames.Any()) return;

            Console.WriteLine($"💰 [ODDS PREMATCH] Atualizando {upcomingGames.Count} jogos prioritários...");
            await FetchBet365OddsIndividual(upcomingGames);
        }

        // ==============================================================================
        // 🔴 ROBÔ 3: AO VIVO (PLACAR + ODDS, A CADA 10S)
        // ==============================================================================
        public async Task SyncLiveFeed()
        {
            if (string.IsNullOrEmpty(_token)) return;

            // Busca AO VIVO de TODOS os esportes (não filtra sport_id=1 na URL para pegar tudo)
            // Se quiser filtrar, o código abaixo processa o resultado e checa o banco.
            var url = $"{BASE_URL}/v1/bet365/inplay_filter?token={_token}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;

                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (!doc.RootElement.TryGetProperty("results", out var results)) return;

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var updates = new List<object>();
                bool saved = false;

                foreach (var item in results.EnumerateArray())
                {
                    string id = GetStringSafe(item, "id") ?? "";
                    if (string.IsNullOrEmpty(id)) continue;

                    // Só atualiza se o jogo JÁ EXISTIR no banco (ou seja, passou pelo filtro do Calendário)
                    var game = await context.SportsEvents.FirstOrDefaultAsync(g => g.ExternalId == id);
                    if (game != null)
                    {
                        string score = GetStringSafe(item, "ss") ?? "0-0";
                        string time = GetStringSafe(item, "tm") ?? "0";

                        if (game.Status != "Live" || game.Score != score || game.GameTime != time)
                        {
                            game.Status = "Live";
                            game.Score = score;
                            game.GameTime = time;
                            game.LastUpdate = DateTime.UtcNow;

                            updates.Add(new
                            {
                                gameId = game.ExternalId,
                                homeScore = ParseScore(score, 0),
                                awayScore = ParseScore(score, 1),
                                currentMinute = time + "'",
                                period = "Live"
                            });
                            saved = true;
                        }
                    }
                }

                if (saved) await context.SaveChangesAsync();

                if (updates.Any())
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveLiveUpdate", updates);
                }
            }
            catch { }
        }

        // ==============================================================================
        // 🛠️ MÉTODOS AUXILIARES
        // ==============================================================================

        public async Task SyncMissingImages()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var jogosSemImagem = await context.SportsEvents
                    .Where(g => g.CommenceTime > DateTime.UtcNow && (g.HomeTeamId == null || g.HomeTeamLogo == null))
                    .OrderBy(g => g.CommenceTime)
                    .Take(50)
                    .ToListAsync();

                if (!jogosSemImagem.Any()) return;

                Console.WriteLine($"🖼️ [IMAGENS] Corrigindo {jogosSemImagem.Count} jogos...");

                var batches = jogosSemImagem.Select((x, i) => new { Index = i, Value = x })
                    .GroupBy(x => x.Index / 10).Select(x => x.Select(v => v.Value).ToList()).ToList();

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
                                    if (item.TryGetProperty("home", out var h))
                                    {
                                        game.HomeTeamId = GetStringSafe(h, "image_id");
                                        if (game.HomeTeamId != null) game.HomeTeamLogo = $"https://assets.b365api.com/images/team/m/{game.HomeTeamId}.png";
                                    }
                                    if (item.TryGetProperty("away", out var a))
                                    {
                                        game.AwayTeamId = GetStringSafe(a, "image_id");
                                        if (game.AwayTeamId != null) game.AwayTeamLogo = $"https://assets.b365api.com/images/team/m/{game.AwayTeamId}.png";
                                    }
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
                catch (Exception ex) { Console.WriteLine($"❌ Erro Odds {game.ExternalId}: {ex.Message}"); }
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
                    if (marketNode.ValueKind != JsonValueKind.Object || !marketNode.TryGetProperty("odds", out var oddsArray)) continue;

                    string marketName = GetStringSafe(marketNode, "name") ?? marketKey;
                    foreach (var odd in oddsArray.EnumerateArray())
                    {
                        decimal price = ParseFractionalOrDecimal(GetStringSafe(odd, "odds"));
                        if (price <= 1.0m) continue;

                        string name = GetStringSafe(odd, "name") ?? "";
                        string header = GetStringSafe(odd, "header") ?? "";
                        string outcomeName = (!string.IsNullOrEmpty(header) && !name.Contains(header)) ? $"{name} {header}".Trim() : (string.IsNullOrEmpty(name) ? header : name);

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
            if (existingOdd == null) existingOdd = context.MarketOdds.Local.FirstOrDefault(m => m.SportsEventId == game.ExternalId && m.MarketName == market && m.OutcomeName == outcome);

            if (existingOdd != null) existingOdd.Price = price;
            else game.Odds.Add(new MarketOdd { SportsEventId = game.ExternalId, Bookmaker = "Bet365", MarketName = market, OutcomeName = outcome, Price = price });
        }

        private bool Process1x2Market(JsonElement marketNode, SportsEvent game, AppDbContext context)
        {
            bool changed = false;
            if (marketNode.TryGetProperty("odds", out var oddsArray))
            {
                foreach (var odd in oddsArray.EnumerateArray())
                {
                    decimal price = ParseFractionalOrDecimal(GetStringSafe(odd, "odds"));
                    if (price <= 0) continue;
                    string target = GetStringSafe(odd, "name") ?? GetStringSafe(odd, "header") ?? "";
                    if (target == "1") { if (game.RawOddsHome != price) { game.RawOddsHome = price; changed = true; } }
                    else if (target == "2") { if (game.RawOddsAway != price) { game.RawOddsAway = price; changed = true; } }
                    else if (target.ToLower().Contains("draw") || target == "X") { if (game.RawOddsDraw != price) { game.RawOddsDraw = price; changed = true; } }
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
                    string mkName = GetStringSafe(item, "NA") ?? "";
                    isProcessingMatchWinner = (mkName == "Fulltime Result" || mkName == "Match Winner" || mkName == "1X2");
                }
                if (type == "PA" && isProcessingMatchWinner)
                {
                    decimal decOdd = ParseFractionalOrDecimal(GetStringSafe(item, "OD"));
                    if (decOdd > 1)
                    {
                        string n2 = GetStringSafe(item, "N2") ?? "";
                        bool chg = false;
                        if (n2 == "1") { currentGame.RawOddsHome = decOdd; chg = true; }
                        else if (n2 == "2") { currentGame.RawOddsAway = decOdd; chg = true; }
                        else if (n2 == "X") { currentGame.RawOddsDraw = decOdd; chg = true; }
                        if (chg)
                        {
                            AddHistoryOdd(currentGame, "Match Winner", GetStringSafe(item, "NA") ?? "Unknown", decOdd, context);
                            currentGame.LastUpdate = DateTime.UtcNow;
                            context.Entry(currentGame).State = EntityState.Modified;
                        }
                    }
                }
            }
            await Task.CompletedTask;
        }

        private string? GetStringSafe(JsonElement el, string key)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            return el.TryGetProperty(key, out var prop) ? (prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString()) : null;
        }

        private decimal ParseFractionalOrDecimal(string? val)
        {
            if (string.IsNullOrEmpty(val)) return 0;
            if (val.Contains("/")) { var p = val.Split('/'); return (decimal.Parse(p[0]) / decimal.Parse(p[1])) + 1; }
            return decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0;
        }

        private int ParseScore(string score, int index)
        {
            try { return int.Parse(score.Split('-')[index]); } catch { return 0; }
        }

        private bool IsFakeSport(string l, string h, string a, string k) => !string.IsNullOrEmpty(l) && (l.ToUpper().Contains("ESOCCER") || l.ToUpper().Contains("E-SOCCER"));

        // Compatibilidade (Depreciado) - Chama o Calendário
        public async Task SyncBaseOddsToDatabase() => await SyncEventsSchedule();
    }
}