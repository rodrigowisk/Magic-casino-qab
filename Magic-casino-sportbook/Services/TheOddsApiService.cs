using Magic_casino_sportbook.DTOs;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace Magic_casino_sportbook.Services
{
    public class TheOddsApiService : IOddsService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly string _apiKey;
        private const string BOOKMAKER_KEY = "pinnacle";

        public TheOddsApiService(HttpClient httpClient, IServiceProvider serviceProvider)
        {
            _httpClient = httpClient;
            _serviceProvider = serviceProvider;
            _apiKey = Environment.GetEnvironmentVariable("ODDS_API_KEY")
                      ?? Environment.GetEnvironmentVariable("OddsApiKey")
                      ?? "";
        }

        public async Task SyncBaseOddsToDatabase()
        {
            if (string.IsNullOrWhiteSpace(_apiKey)) return;

            var sportsJson = await _httpClient.GetStringAsync($"https://api.the-odds-api.com/v4/sports/?apiKey={_apiKey}");
            var sports = JsonDocument.Parse(sportsJson).RootElement.EnumerateArray()
                .Select(s => s.GetProperty("key").GetString())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToList();

            Console.WriteLine($"\n🔥 THE ODDS API: Iniciando {sports.Count} ligas...");

            using var semaphore = new SemaphoreSlim(8);
            var tasks = sports.Select(async sportKeyRaw =>
            {
                await semaphore.WaitAsync();
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await ProcessSingleSport(sportKeyRaw, context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro na liga {sportKeyRaw}: {ex.Message}");
                }
                finally { semaphore.Release(); }
            });

            await Task.WhenAll(tasks);
            Console.WriteLine($"✅ THE ODDS API: Finalizado!");
        }

        private async Task ProcessSingleSport(string? sportKeyRaw, AppDbContext context)
        {
            if (string.IsNullOrEmpty(sportKeyRaw)) return;
            var url = $"https://api.the-odds-api.com/v4/sports/{sportKeyRaw}/odds/?apiKey={_apiKey}&bookmakers={BOOKMAKER_KEY}&markets=h2h,spreads,totals&regions=eu,uk,us";

            var resp = await _httpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return;

            var json = await resp.Content.ReadAsStringAsync();
            List<TheOddsApiResponse>? apiData;
            try { apiData = JsonSerializer.Deserialize<List<TheOddsApiResponse>>(json); } catch { return; }

            if (apiData == null || apiData.Count == 0) return;

            foreach (var item in apiData)
            {
                var bookmakerObj = item.bookmakers?.FirstOrDefault(b => (b.key ?? "").Equals(BOOKMAKER_KEY, StringComparison.OrdinalIgnoreCase));
                if (bookmakerObj?.markets == null) continue;

                var (cat, slug, leagueId) = ParseSportKeyRaw(item.sport_key);
                var existingEvent = await context.SportsEvents.Include(e => e.Odds).FirstOrDefaultAsync(e => e.ExternalId == item.id);

                if (existingEvent == null)
                {
                    existingEvent = new SportsEvent
                    {
                        ExternalId = item.id,
                        HomeTeam = item.home_team,
                        AwayTeam = item.away_team,
                        CommenceTime = item.commence_time.ToUniversalTime(),
                        SportKeyRaw = item.sport_key,
                        SportKey = cat,
                        League = !string.IsNullOrWhiteSpace(slug) ? slug.Replace("_", " ") : cat,
                        LastUpdate = DateTime.UtcNow,
                        OddsSource = "TheOddsApi"
                    };
                    context.SportsEvents.Add(existingEvent);
                }
                else
                {
                    existingEvent.CommenceTime = item.commence_time.ToUniversalTime();
                    existingEvent.LastUpdate = DateTime.UtcNow;
                }

                var h2h = bookmakerObj.markets.FirstOrDefault(m => m.key == "h2h");
                if (h2h?.outcomes != null)
                {
                    var home = h2h.outcomes.FirstOrDefault(o => o.name == item.home_team);
                    var away = h2h.outcomes.FirstOrDefault(o => o.name == item.away_team);
                    var draw = h2h.outcomes.FirstOrDefault(o => o.name == "Draw");

                    if (home != null) existingEvent.RawOddsHome = (decimal)home.price;
                    if (away != null) existingEvent.RawOddsAway = (decimal)away.price;
                    if (draw != null) existingEvent.RawOddsDraw = (decimal)draw.price;
                }
            }
            await context.SaveChangesAsync();
        }

        // -----------------------------------------------------------
        // Helpers e Interface (Métodos vazios para cumprir contrato)
        // -----------------------------------------------------------

        public Task BackfillSportKeyRaw() => Task.CompletedTask;
        public Task<bool> SyncFullMarketsForEvent(string eventId) => Task.FromResult(true);

        public Task SyncLiveFeed() => Task.CompletedTask;
        public Task SyncMissingImages() => Task.CompletedTask;

        // ✅ ADICIONADOS AGORA PARA CORRIGIR O ERRO DE COMPILAÇÃO
        public Task SyncEventsSchedule() => Task.CompletedTask;
        public Task SyncPrematchOdds() => Task.CompletedTask;

        private static bool IsNumeric(string? s) => !string.IsNullOrWhiteSpace(s) && long.TryParse(s, out _);
        private static (string category, string leagueSlug, string leagueId) ParseSportKeyRaw(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return ("", "", "");
            var parts = raw.Split('_', StringSplitOptions.RemoveEmptyEntries);
            return (parts[0], raw, "");
        }
    }
}