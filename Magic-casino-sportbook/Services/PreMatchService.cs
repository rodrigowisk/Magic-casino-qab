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

        // Semáforo para impedir conflito com o Live e respeitar a API
        private static readonly SemaphoreSlim _apiGate = new SemaphoreSlim(1, 1);

        private const int MAX_PAGES = 50;
        private const int MAX_RETRIES = 3;

        private readonly string[] _excludedTerms = new[]
        {
            "ESOCCER", "E-SOCCER", "E SOCCER", "EBASKETBALL", "E-BASKETBALL",
            "E BASKETBALL", "EVOLLEYBALL", "E-VOLLEYBALL", "E-HOCKEY", "EHOCKEY",
            "E-CRICKET", "ECRICKET", "VIRTUAL", "CYBER", "SIMULATED",
            "GT LEAGUE", "BATTLE", "2X2", "4X4", "PENALTY", "E-SPORT", "ESPORT", "FIFA", "NBA 2K"
        };

        public PreMatchService(HttpClient httpClient, IServiceScopeFactory scopeFactory)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _scopeFactory = scopeFactory;
            _token = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";

            _parsers = new List<IMarketParser>
            {
                new MainParser(), new AsianParser(), new GoalsParser(), new PropsParser()
            };
        }

        public async Task SyncUpcomingGames()
        {
            if (string.IsNullOrEmpty(_token)) return;

            Console.WriteLine("🚀 [PRE-MATCH] Iniciando Ingestão Otimizada (Lotes de 50)...");

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

            foreach (var config in activeConfigs)
            {
                string apiSportId = ConvertToApiId(config.Identifier);
                await ProcessSport(apiSportId, config.Identifier, limitDate);
            }

            Console.WriteLine("🏁 [PRE-MATCH] Ciclo finalizado.");
        }

        private async Task ProcessSport(string apiSportId, string internalSportKey, DateTime limitDate)
        {
            int page = 1;
            bool keepFetching = true;
            var eventIdsForOdds = new List<string>();
            int consecutiveErrors = 0;

            while (keepFetching && page <= MAX_PAGES)
            {
                var url = $"https://api.betsapi.com/v1/bet365/upcoming?sport_id={apiSportId}&token={_token}&page={page}";

                await _apiGate.WaitAsync();
                try
                {
                    var response = await _httpClient.GetAsync(url);

                    if ((int)response.StatusCode == 429)
                    {
                        Console.WriteLine($"⛔ [PRE-MATCH LIMIT] 429 na pág {page}. Pausando 5s...");
                        await Task.Delay(5000);
                        _apiGate.Release();
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        consecutiveErrors++;
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

                            string leagueName = "Desconhecido";
                            if (item.TryGetProperty("league", out var lObj)) leagueName = GetStr(lObj, "name") ?? "Desconhecido";

                            string homeName = "", awayName = "";
                            if (item.TryGetProperty("home", out var h)) homeName = GetStr(h, "name");
                            if (item.TryGetProperty("away", out var a)) awayName = GetStr(a, "name");

                            if (IsVirtualOrEsport(leagueName, homeName, awayName)) continue;

                            eventIdsForOdds.Add(externalId);

                            var existingEvent = await context.SportsEvents.FirstOrDefaultAsync(e => e.ExternalId == externalId);

                            string leagueId = null, cc = null;
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
                }
                finally
                {
                    if (_apiGate.CurrentCount == 0) _apiGate.Release();
                }

                await Task.Delay(1200);
            }

            if (eventIdsForOdds.Any())
            {
                var distinctIds = eventIdsForOdds.Distinct().ToList();
                Console.WriteLine($"💰 [ODDS] Buscando cotações para {distinctIds.Count} jogos de {internalSportKey}...");
                // ✅ VOLTAMOS PARA SEQUENCIAL (Mais seguro para API e com Retry)
                await FetchOddsSequential(distinctIds, internalSportKey);
            }
        }

        private async Task FetchOddsSequential(List<string> allIds, string sportKey)
        {
            // ✅ LOTE DE 50 (Economiza chamadas à API)
            var chunks = allIds.Chunk(50).ToList();

            foreach (var chunk in chunks)
            {
                var idsStr = string.Join(",", chunk);
                var url = $"https://api.betsapi.com/v1/bet365/prematch?token={_token}&FI={idsStr}";

                await _apiGate.WaitAsync();
                try
                {
                    var response = await _httpClient.GetAsync(url);

                    if ((int)response.StatusCode == 429)
                    {
                        Console.WriteLine($"⛔ [ODDS LIMIT] 429 Detectado. Esperando 5s para tentar novamente...");
                        await Task.Delay(5000);
                        _apiGate.Release();
                        continue; // Tenta o MESMO lote de novo
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();

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
                                        var moneyLineMarkets = new List<MarketDto>();

                                        foreach (var m in parsedMarkets)
                                        {
                                            // Atualiza ou Insere na tabela de Odds
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

                                            // Filtra candidatos a Odd Principal
                                            string mName = m.MarketName?.Trim().ToUpper() ?? "";

                                            // ✅ CORREÇÃO: ADICIONADO "RESULT", "FULL" E "1X2" (Isso conserta o bug de odds zeradas)
                                            if (mName.Contains("MONEY LINE") || mName.Contains("WINNER") || mName.Contains("VENCEDOR") ||
                                                mName.Contains("RESULT") || mName.Contains("FULL") || mName.Contains("1X2") || mName == "12")
                                            {
                                                moneyLineMarkets.Add(m);
                                            }
                                        }

                                        // Atualiza as colunas principais (RawOdds)
                                        bool foundByLabel = false;
                                        foreach (var m in moneyLineMarkets)
                                        {
                                            if (IsHome(m.OutcomeName, dbEvent.HomeTeam)) { dbEvent.RawOddsHome = m.Price; foundByLabel = true; }
                                            else if (IsDraw(m.OutcomeName)) { dbEvent.RawOddsDraw = m.Price; foundByLabel = true; }
                                            else if (IsAway(m.OutcomeName, dbEvent.AwayTeam)) { dbEvent.RawOddsAway = m.Price; foundByLabel = true; }
                                        }

                                        // 🚨 O PULO DO GATO (Fallback por Ordem)
                                        if (!foundByLabel && moneyLineMarkets.Count >= 2)
                                        {
                                            dbEvent.RawOddsHome = moneyLineMarkets[0].Price;
                                            dbEvent.RawOddsAway = moneyLineMarkets[1].Price;

                                            if (moneyLineMarkets.Count == 3)
                                            {
                                                dbEvent.RawOddsDraw = moneyLineMarkets[1].Price;
                                                dbEvent.RawOddsAway = moneyLineMarkets[2].Price;
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
                finally
                {
                    if (_apiGate.CurrentCount == 0) _apiGate.Release();
                }

                await Task.Delay(1000);
            }
        }

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

        private bool IsVirtualOrEsport(string league, string home, string away)
        {
            var text = $"{league} {home} {away}".ToUpper();
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