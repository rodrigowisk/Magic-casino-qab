using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Magic_casino_sportbook.BackgroundServices
{
    public class LiveScoreWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;

        // 💎 URL PREMIUM
        private const string BASE_URL = "https://api.b365api.com";
        private readonly string _betsApiToken = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";

        public LiveScoreWorker(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("🤖 LiveScoreWorker (Settlement Premium) Iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await UpdateLiveScoresBetsApi(context);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro no LiveScoreWorker: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task UpdateLiveScoresBetsApi(AppDbContext context)
        {
            if (string.IsNullOrEmpty(_betsApiToken)) return;

            var pendingMatchIds = await context.BetSelections
                .Where(s => s.Status == "pending")
                .Select(s => s.MatchId)
                .Distinct()
                .ToListAsync();

            if (!pendingMatchIds.Any()) return;

            var client = _httpClientFactory.CreateClient();
            int batchSize = 10;

            for (int i = 0; i < pendingMatchIds.Count; i += batchSize)
            {
                var batchIds = pendingMatchIds.Skip(i).Take(batchSize).ToList();
                var idsString = string.Join(",", batchIds);

                var url = $"{BASE_URL}/v1/bet365/event?token={_betsApiToken}&FI={idsString}";

                try
                {
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);

                        if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var group in results.EnumerateArray())
                            {
                                if (group.ValueKind == JsonValueKind.Array)
                                {
                                    ProcessGameResult(group, context);
                                }
                            }
                        }
                    }
                }
                catch { }

                await Task.Delay(200);
            }
            await context.SaveChangesAsync();
        }

        private void ProcessGameResult(JsonElement listItems, AppDbContext context)
        {
            string gameId = "";
            string scoreStr = "";

            foreach (var item in listItems.EnumerateArray())
            {
                string type = "";
                if (item.TryGetProperty("type", out var t)) type = t.GetString() ?? "";

                if (type == "EV")
                {
                    gameId = item.TryGetProperty("FI", out var fi) ? fi.GetString() ?? "" : "";
                    scoreStr = item.TryGetProperty("ss", out var ss) ? ss.GetString() ?? "" : "";
                }
            }

            if (!string.IsNullOrEmpty(gameId) && !string.IsNullOrEmpty(scoreStr))
            {
                var selections = context.BetSelections
                    .Where(s => s.MatchId == gameId && s.Status == "pending")
                    .ToList();

                foreach (var sel in selections)
                {
                    sel.FinalScore = scoreStr;
                }
            }
        }
    }
}