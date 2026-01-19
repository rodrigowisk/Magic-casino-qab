using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Data.Models;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Services.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Magic_casino_sportbook.Services
{
    public class PreMatchService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _token;
        private readonly List<IMarketParser> _parsers;

        // Configurações de Resiliência
        private const int MAX_PAGES = 50;
        private const int MAX_RETRIES = 3;

        // 🛑 LISTA NEGRA DE ESPORTES VIRTUAIS (Fácil de transformar em dinâmico depois)
        // No futuro, você pode carregar isso de uma tabela do banco: _context.BlockedTerms.ToList()
        private readonly string[] _excludedTerms = new[]
        {
            "ESOCCER", "E-SOCCER", "E SOCCER",
            "EBASKETBALL", "E-BASKETBALL", "E BASKETBALL",
            "EVOLLEYBALL", "E-VOLLEYBALL",
            "E-HOCKEY", "EHOCKEY",
            "E-CRICKET", "ECRICKET",
            "VIRTUAL", "CYBER", "SIMULATED",
            "GT LEAGUE", "BATTLE", "2X2", "4X4", "PENALTY",
            "E-SPORT", "ESPORT", "FIFA", "NBA 2K"
        };

        public PreMatchService(HttpClient httpClient, IServiceScopeFactory scopeFactory)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _scopeFactory = scopeFactory;
            _token = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";

            _parsers = new List<IMarketParser>
            {
                new MainParser(),
                new AsianParser(),
                new GoalsParser(),
                new PropsParser()
            };
        }

        public async Task SyncUpcomingGames()
        {
            if (string.IsNullOrEmpty(_token)) return;

            Console.WriteLine("🚀 [PRE-MATCH] Iniciando Ingestão Filtrada (Sem Virtuais)...");

            List<SportConfiguration> activeConfigs;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                activeConfigs = await context.SportConfigurations
                    .AsNoTracking()
                    .Where(c => c.Type == "SPORT" && c.IsEnabled == true)
                    .ToListAsync();
            }

            if (!activeConfigs.Any()) activeConfigs.Add(new SportConfiguration { Identifier = "soccer" });

            var limitDate = DateTime.UtcNow.AddDays(4);
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 3 };

            await Parallel.ForEachAsync(activeConfigs, parallelOptions, async (config, ct) =>
            {
                string apiSportId = ConvertToApiId(config.Identifier);
                await ProcessSport(apiSportId, config.Identifier, limitDate);
            });

            Console.WriteLine("🏁 [PRE-MATCH] Ciclo finalizado.");
        }

        private async Task ProcessSport(string apiSportId, string internalSportKey, DateTime limitDate)
        {
            int page = 1;
            bool keepFetching = true;
            var eventIdsForOdds = new ConcurrentBag<string>();
            int consecutiveErrors = 0;

            while (keepFetching && page <= MAX_PAGES)
            {
                var url = $"https://api.betsapi.com/v1/bet365/upcoming?sport_id={apiSportId}&token={_token}&page={page}";

                try
                {
                    var response = await _httpClient.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        consecutiveErrors++;
                        Console.WriteLine($"⚠️ Erro API ({response.StatusCode}) em {internalSportKey} pág {page}. Tentativa {consecutiveErrors}/{MAX_RETRIES}");
                        if (consecutiveErrors >= MAX_RETRIES) break;
                        await Task.Delay(2000);
                        continue;
                    }

                    consecutiveErrors = 0;

                    using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                    {
                        keepFetching = false;
                        break;
                    }

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        foreach (var item in results.EnumerateArray())
                        {
                            string timeStr = GetStr(item, "time") ?? "0";
                            if (!long.TryParse(timeStr, out long timeTs)) continue;
                            var gameDate = DateTimeOffset.FromUnixTimeSeconds(timeTs).UtcDateTime;

                            if (gameDate < DateTime.UtcNow.AddHours(-24)) continue;
                            if (gameDate > limitDate) { keepFetching = false; }

                            string externalId = GetStr(item, "id");
                            if (string.IsNullOrEmpty(externalId)) continue;

                            // -----------------------------------------------------------
                            // 🛑 BLOQUEIO DE ESPORTES VIRTUAIS (CHECKPOINT)
                            // -----------------------------------------------------------
                            string leagueName = "Desconhecido";
                            if (item.TryGetProperty("league", out var lObj))
                            {
                                leagueName = GetStr(lObj, "name") ?? "Desconhecido";
                            }

                            string homeName = "";
                            if (item.TryGetProperty("home", out var h)) homeName = GetStr(h, "name");

                            string awayName = "";
                            if (item.TryGetProperty("away", out var a)) awayName = GetStr(a, "name");

                            // Se for virtual, PULA para o próximo e não salva
                            if (IsVirtualOrEsport(leagueName, homeName, awayName)) continue;
                            // -----------------------------------------------------------

                            eventIdsForOdds.Add(externalId);

                            var existingEvent = await context.SportsEvents.FirstOrDefaultAsync(e => e.ExternalId == externalId);

                            string leagueId = null;
                            string cc = null;
                            if (item.TryGetProperty("league", out var lObj2))
                            {
                                leagueId = GetStr(lObj2, "image_id");
                                cc = GetStr(lObj2, "cc");
                            }

                            if (existingEvent == null)
                            {
                                var homeObj = item.TryGetProperty("home", out var h2) ? h2 : default;
                                var awayObj = item.TryGetProperty("away", out var a2) ? a2 : default;

                                var newEvent = new SportsEvent
                                {
                                    ExternalId = externalId,
                                    SportKey = internalSportKey,
                                    League = leagueName,
                                    LeagueId = leagueId,
                                    CountryCode = cc,
                                    HomeTeam = GetStr(homeObj, "name") ?? "Home",
                                    AwayTeam = GetStr(awayObj, "name") ?? "Away",
                                    HomeTeamId = GetStr(homeObj, "image_id"),
                                    AwayTeamId = GetStr(awayObj, "image_id"),
                                    CommenceTime = gameDate,
                                    Status = "Prematch",
                                    OddsSource = "BetsAPI",
                                    LastUpdate = DateTime.UtcNow
                                };
                                context.SportsEvents.Add(newEvent);
                            }
                            else
                            {
                                existingEvent.CommenceTime = gameDate;
                                existingEvent.LastUpdate = DateTime.UtcNow;
                                if (cc != null) existingEvent.CountryCode = cc;
                            }
                        }
                        await context.SaveChangesAsync();
                    }
                    Console.WriteLine($"✅ {internalSportKey} Pág {page} OK");
                    page++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro Grade {internalSportKey}: {ex.Message}");
                    consecutiveErrors++;
                    if (consecutiveErrors >= MAX_RETRIES) keepFetching = false;
                }
            }

            if (!eventIdsForOdds.IsEmpty)
            {
                var distinctIds = eventIdsForOdds.Distinct().ToList();
                Console.WriteLine($"💰 [ODDS] Buscando cotações para {distinctIds.Count} jogos de {internalSportKey}...");
                await FetchOddsParallel(distinctIds, internalSportKey);
            }
        }

        private async Task FetchOddsParallel(List<string> allIds, string sportKey)
        {
            var chunks = allIds.Chunk(10).ToList();
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 5 };

            await Parallel.ForEachAsync(chunks, parallelOptions, async (chunk, ct) =>
            {
                var idsStr = string.Join(",", chunk);
                var url = $"https://api.betsapi.com/v1/bet365/prematch?token={_token}&FI={idsStr}";

                try
                {
                    var response = await _httpClient.GetAsync(url, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(ct);

                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            using var doc = JsonDocument.Parse(json);

                            if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                            {
                                foreach (var item in results.EnumerateArray())
                                {
                                    string fi = GetStr(item, "FI");
                                    if (string.IsNullOrEmpty(fi)) continue;

                                    var dbEvent = await context.SportsEvents
                                        .Include(e => e.Odds)
                                        .FirstOrDefaultAsync(e => e.ExternalId == fi);

                                    if (dbEvent == null) continue;

                                    foreach (var parser in _parsers)
                                    {
                                        var parsedMarkets = parser.Parse(item, sportKey);

                                        foreach (var m in parsedMarkets)
                                        {
                                            var existing = dbEvent.Odds.FirstOrDefault(o => o.MarketName == m.MarketName && o.OutcomeName == m.OutcomeName);

                                            if (existing != null)
                                            {
                                                existing.Price = m.Price;
                                                existing.LastUpdate = DateTime.UtcNow;
                                            }
                                            else
                                            {
                                                dbEvent.Odds.Add(new EventMarket
                                                {
                                                    SportsEventId = dbEvent.Id,
                                                    ExternalId = m.ExternalId,
                                                    MarketName = m.MarketName,
                                                    OutcomeName = m.OutcomeName,
                                                    Price = m.Price,
                                                    Handicap = m.Handicap,
                                                    LastUpdate = DateTime.UtcNow
                                                });
                                            }
                                        }
                                    }
                                    dbEvent.LastUpdate = DateTime.UtcNow;
                                }
                                await context.SaveChangesAsync();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Erro Chunk Odds: {ex.Message}");
                }
            });
        }

        // 💎 O FILTRO INTELIGENTE
        private bool IsVirtualOrEsport(string league, string home, string away)
        {
            var text = $"{league} {home} {away}".ToUpper();

            // Verifica se contém qualquer termo da lista negra
            foreach (var term in _excludedTerms)
            {
                if (text.Contains(term)) return true;
            }
            return false;
        }

        private string GetStr(JsonElement el, string key)
            => el.TryGetProperty(key, out var p) ? p.GetString() : null;

        private string ConvertToApiId(string identifier)
        {
            if (int.TryParse(identifier, out _)) return identifier;
            return identifier.ToLower().Trim() switch
            {
                "soccer" => "1",
                "futebol" => "1",
                "basketball" => "18",
                "basquete" => "18",
                "tennis" => "13",
                "tenis" => "13",
                "volleyball" => "91",
                "volei" => "91",
                "esports" => "151",
                "esoccer" => "151",
                "futsal" => "83",
                "handball" => "78",
                "ice_hockey" => "17",
                "hockey" => "17",
                "mma" => "162",
                "boxing" => "9",
                "baseball" => "16",
                "american_football" => "12",
                "rugby_union" => "8",
                "rugby_league" => "19",
                _ => "1"
            };
        }
    }
}