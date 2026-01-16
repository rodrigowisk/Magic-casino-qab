using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;

namespace Magic_casino_sportbook.Services
{
    public class PreMatchService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _context;
        private readonly string _token;
        private const int MAX_PAGES_PER_SPORT = 50;

        private readonly Dictionary<int, string> _sportMap = new() {
            { 1, "soccer" }, { 18, "basketball" }, { 13, "tennis" }, { 91, "volleyball" }
        };

        public PreMatchService(HttpClient httpClient, AppDbContext context)
        {
            _httpClient = httpClient;
            _context = context;
            _token = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";
        }

        public async Task SyncUpcomingGames()
        {
            if (string.IsNullOrEmpty(_token)) return;
            Console.WriteLine("📅 [PRE-MATCH] Iniciando sincronização de jogos futuros...");

            foreach (var sport in _sportMap)
            {
                int page = 1;
                while (page <= MAX_PAGES_PER_SPORT)
                {
                    var url = $"https://api.betsapi.com/v1/bet365/upcoming?sport_id={sport.Key}&token={_token}&page={page}";
                    var response = await _httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode) break;

                    using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) break;

                    foreach (var item in results.EnumerateArray())
                    {
                        string leagueName = item.GetProperty("league").GetProperty("name").GetString() ?? "";
                        if (IsFakeSport(leagueName)) continue;

                        string id = item.GetProperty("id").GetString() ?? "";
                        if (await _context.SportsEvents.AnyAsync(e => e.ExternalId == id)) continue;

                        _context.SportsEvents.Add(new SportsEvent
                        {
                            ExternalId = id,
                            HomeTeam = item.GetProperty("home").GetProperty("name").GetString() ?? "Casa",
                            AwayTeam = item.GetProperty("away").GetProperty("name").GetString() ?? "Fora",
                            CommenceTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(item.GetProperty("time").GetString() ?? "0")).UtcDateTime,
                            SportKey = sport.Value,
                            League = leagueName,
                            OddsSource = "BetsAPI_PreMatch"
                        });
                    }
                    await _context.SaveChangesAsync();
                    page++;
                }
            }
        }

        private bool IsFakeSport(string name)
        {
            string n = name.ToUpper();
            return n.Contains("ESOCCER") || n.Contains("VIRTUAL") || n.Contains("SIMULATED") || n.Contains("GT LEAGUES");
        }
    }
}