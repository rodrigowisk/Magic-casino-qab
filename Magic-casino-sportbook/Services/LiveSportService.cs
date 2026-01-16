using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.DTOs;
using Magic_casino_sportbook.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;

namespace Magic_casino_sportbook.Services
{
    public class LiveSportService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly string _token;

        // 💎 URL PREMIUM
        private const string BASE_URL = "https://api.b365api.com";

        public LiveSportService(HttpClient httpClient, IServiceScopeFactory scopeFactory, IHubContext<GameHub> hubContext)
        {
            _httpClient = httpClient;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _token = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";
        }

        public async Task SyncLiveFeed()
        {
            if (string.IsNullOrEmpty(_token)) return;

            // 1. Busca IDs ao vivo (InPlay Filter - Mais leve)
            var urlList = $"{BASE_URL}/v1/bet365/inplay_filter?sport_id=1&token={_token}";

            try
            {
                var response = await _httpClient.GetAsync(urlList);
                if (!response.IsSuccessStatusCode) return;

                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                var liveIds = new List<string>();

                if (doc.RootElement.TryGetProperty("results", out var resultsArray) && resultsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in resultsArray.EnumerateArray())
                    {
                        string id = item.TryGetProperty("id", out var fi) ? fi.GetString() : null;
                        if (id != null) liveIds.Add(id);
                    }
                }

                if (!liveIds.Any()) return;

                // 2. Busca Detalhes (Premium aceita múltiplos IDs)
                var targetIds = liveIds.Take(20).ToList();
                var idsString = string.Join(",", targetIds);
                var urlEvent = $"{BASE_URL}/v1/bet365/event?token={_token}&FI={idsString}";

                var respEvt = await _httpClient.GetAsync(urlEvent);

                // Cria escopo para banco de dados
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    if (respEvt.IsSuccessStatusCode)
                    {
                        using var docEvt = await JsonDocument.ParseAsync(await respEvt.Content.ReadAsStreamAsync());
                        if (docEvt.RootElement.TryGetProperty("results", out var eventList))
                        {
                            foreach (var group in eventList.EnumerateArray())
                            {
                                if (group.ValueKind == JsonValueKind.Array)
                                {
                                    await ProcessLiveGameData(group, context);
                                }
                            }
                        }
                    }
                    await context.SaveChangesAsync();

                    // 3. Notifica Frontend
                    await NotifyFrontend(targetIds, context);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro no Live Sync: {ex.Message}");
            }
        }

        private async Task ProcessLiveGameData(JsonElement listItems, AppDbContext context)
        {
            string id = "";
            string scoreStr = "0-0";
            string timeStr = "0'";
            string period = "Live";

            decimal oddHome = 0, oddDraw = 0, oddAway = 0;
            bool foundOdds = false;
            bool isProcessingMatchWinner = false;

            foreach (var item in listItems.EnumerateArray())
            {
                string type = GetStringSafe(item, "type");

                if (type == "EV")
                {
                    id = GetStringSafe(item, "FI");
                    scoreStr = GetStringSafe(item, "ss");
                    if (string.IsNullOrEmpty(scoreStr)) scoreStr = "0-0";

                    string tm = GetStringSafe(item, "TM");
                    if (!string.IsNullOrEmpty(tm)) timeStr = tm + "'";
                }

                if (type == "MG")
                {
                    string marketName = GetStringSafe(item, "NA");
                    if (marketName == "Fulltime Result" || marketName == "Match Winner" || marketName == "1X2")
                        isProcessingMatchWinner = true;
                    else
                        isProcessingMatchWinner = false;
                }

                if (type == "PA" && isProcessingMatchWinner)
                {
                    string oddStr = GetStringSafe(item, "OD");
                    string n2 = GetStringSafe(item, "N2");

                    decimal decimalOdd = ParseFractionalOrDecimal(oddStr);
                    if (decimalOdd > 1)
                    {
                        if (n2 == "1") oddHome = decimalOdd;
                        else if (n2 == "2") oddAway = decimalOdd;
                        else if (n2 == "X") oddDraw = decimalOdd;
                        foundOdds = true;
                    }
                }
            }

            if (string.IsNullOrEmpty(id)) return;

            // Salva Placar
            var stat = await context.LiveGameStat.FirstOrDefaultAsync(s => s.GameId == id);
            if (stat == null)
            {
                var gameExists = await context.SportsEvents.AnyAsync(e => e.ExternalId == id);
                if (gameExists)
                {
                    context.LiveGameStat.Add(new LiveGameStat
                    {
                        GameId = id,
                        HomeScore = ParseScore(scoreStr, 0),
                        AwayScore = ParseScore(scoreStr, 1),
                        CurrentMinute = timeStr,
                        Period = period,
                        LastUpdated = DateTime.UtcNow
                    });
                }
            }
            else
            {
                stat.HomeScore = ParseScore(scoreStr, 0);
                stat.AwayScore = ParseScore(scoreStr, 1);
                stat.CurrentMinute = timeStr;
                stat.Period = period;
                stat.LastUpdated = DateTime.UtcNow;
            }

            // Salva Odds
            if (foundOdds)
            {
                var sportEvent = await context.SportsEvents.FirstOrDefaultAsync(e => e.ExternalId == id);
                if (sportEvent != null)
                {
                    sportEvent.RawOddsHome = oddHome;
                    sportEvent.RawOddsDraw = oddDraw;
                    sportEvent.RawOddsAway = oddAway;
                    sportEvent.LastUpdate = DateTime.UtcNow;
                }
            }
        }

        private int ParseScore(string score, int index)
        {
            try
            {
                if (string.IsNullOrEmpty(score)) return 0;
                var parts = score.Split('-');
                if (parts.Length > index && int.TryParse(parts[index], out int s)) return s;
                return 0;
            }
            catch { return 0; }
        }

        private async Task NotifyFrontend(List<string> ids, AppDbContext context)
        {
            var updates = await context.LiveGameStat
                .Where(s => ids.Contains(s.GameId))
                .Join(context.SportsEvents,
                      stat => stat.GameId,
                      evt => evt.ExternalId,
                      (stat, evt) => new
                      {
                          gameId = stat.GameId,
                          homeScore = stat.HomeScore,
                          awayScore = stat.AwayScore,
                          currentMinute = stat.CurrentMinute,
                          period = stat.Period,
                          homeOdd = evt.RawOddsHome,
                          drawOdd = evt.RawOddsDraw,
                          awayOdd = evt.RawOddsAway
                      })
                .ToListAsync();

            if (updates.Any())
            {
                // ✅ NOME CORRETO PARA O FRONTEND (SINGULAR)
                await _hubContext.Clients.All.SendAsync("ReceiveLiveUpdate", updates);
            }
        }

        private string GetStringSafe(JsonElement el, string key)
        {
            if (el.ValueKind != JsonValueKind.Object) return "";
            if (el.TryGetProperty(key, out var prop)) return prop.GetString() ?? "";
            return "";
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
    }
}