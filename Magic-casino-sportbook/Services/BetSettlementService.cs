using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Services.Gateways;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Magic_casino_sportbook.Services
{
    public class BetSettlementService
    {
        private readonly AppDbContext _context;
        private readonly BetsApiHttpService _betsApi;      // Nome correto: _betsApi
        private readonly IHttpClientFactory _httpFactory;  // Nome correto: _httpFactory

        private readonly string _oddsApiKey;
        private readonly string _provider;

        public BetSettlementService(
            AppDbContext context,
            BetsApiHttpService betsApi,
            IHttpClientFactory httpFactory)
        {
            _context = context;
            _betsApi = betsApi;          // Atribuição correta
            _httpFactory = httpFactory;  // Atribuição correta

            _oddsApiKey = Environment.GetEnvironmentVariable("ODDS_API_KEY") ?? "";
            _provider = Environment.GetEnvironmentVariable("ODDS_PROVIDER") ?? "BetsApi";
        }

        public async Task UpdatePendingBetsAsync()
        {
            var pendingSelections = await _context.BetSelections
                .Where(s => s.Status == "pending" && s.CommenceTime < DateTime.UtcNow)
                .ToListAsync();

            if (!pendingSelections.Any()) return;

            var matchIds = pendingSelections.Select(s => s.MatchId).Distinct().ToList();

            foreach (var matchId in matchIds)
            {
                try
                {
                    if (_provider == "BetsApi")
                    {
                        await SettleBetsApiMatch(matchId, pendingSelections);
                    }
                    else
                    {
                        await SettleTheOddsApiMatch(matchId, pendingSelections);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro ao liquidar match {matchId} via {_provider}: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();
            await UpdateParentBetsStatusAsync();
        }

        // ==============================================================================
        // ESTRATÉGIA 1: BETS API (Com Gateway Protegido)
        // ==============================================================================
        private async Task SettleBetsApiMatch(string matchId, List<BetSelection> allPending)
        {
            var url = $"/v1/bet365/result?event_id={matchId}";

            // CORREÇÃO: Usando _betsApi em vez de _api
            var response = await _betsApi.GetAsync(url, isLivePriority: true);

            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array) return;

            var eventData = results.EnumerateArray().FirstOrDefault();

            if (eventData.ValueKind != JsonValueKind.Undefined &&
                eventData.TryGetProperty("time_status", out var ts) &&
                ts.GetString() == "3")
            {
                string scoreStr = "";
                if (eventData.TryGetProperty("ss", out var ssProp)) scoreStr = ssProp.GetString();

                if (string.IsNullOrEmpty(scoreStr)) return;

                ProcessWinners(matchId, scoreStr, allPending);
            }
        }

        // ==============================================================================
        // ESTRATÉGIA 2: GENÉRICA / THE ODDS API
        // ==============================================================================
        private async Task SettleTheOddsApiMatch(string matchId, List<BetSelection> allPending)
        {
            // CORREÇÃO: Criar o client usando a factory, pois _httpClient não existe mais
            var client = _httpFactory.CreateClient();

            var url = $"https://api.the-odds-api.com/v4/sports/soccer/scores/?apiKey={_oddsApiKey}&eventIds={matchId}";
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

            var eventData = doc.RootElement.EnumerateArray().FirstOrDefault();

            if (eventData.ValueKind != JsonValueKind.Undefined &&
                eventData.TryGetProperty("completed", out var comp) &&
                comp.GetBoolean())
            {
                if (eventData.TryGetProperty("scores", out var scores) && scores.GetArrayLength() >= 2)
                {
                    var hScore = scores[0].GetProperty("score").GetInt32();
                    var aScore = scores[1].GetProperty("score").GetInt32();

                    string scoreStr = $"{hScore}-{aScore}";
                    ProcessWinners(matchId, scoreStr, allPending);
                }
            }
        }

        private void ProcessWinners(string matchId, string scoreStr, List<BetSelection> allPending)
        {
            var selections = allPending.Where(s => s.MatchId == matchId).ToList();
            foreach (var sel in selections)
            {
                sel.FinalScore = scoreStr;
                sel.IsWinner = CalculateWinnerFromScore(scoreStr, sel.SelectionName);
                sel.Status = sel.IsWinner == true ? "won" : "lost";
            }
        }

        private bool CalculateWinnerFromScore(string scoreStr, string selectionName)
        {
            var parts = scoreStr.Split('-');
            if (parts.Length < 2) return false;

            if (!int.TryParse(parts[0], out int hScore) || !int.TryParse(parts[1], out int aScore)) return false;

            selectionName = selectionName.ToLower();

            if (hScore > aScore)
                return selectionName.Contains("1") || selectionName.Contains("casa") || selectionName.Contains("home");

            if (aScore > hScore)
                return selectionName.Contains("2") || selectionName.Contains("fora") || selectionName.Contains("away");

            if (hScore == aScore)
                return selectionName.Contains("x") || selectionName.Contains("draw") || selectionName.Contains("empate");

            return false;
        }

        private async Task UpdateParentBetsStatusAsync()
        {
            var activeBets = await _context.Bets
                .Include(b => b.Selections)
                .Where(b => b.Status == "confirmed")
                .ToListAsync();

            foreach (var bet in activeBets)
            {
                if (bet.Selections.All(s => s.Status != "pending"))
                {
                    if (bet.Selections.Any(s => s.Status == "lost"))
                    {
                        bet.Status = "lost";
                    }
                    else if (bet.Selections.All(s => s.Status == "won"))
                    {
                        bet.Status = "won";
                    }
                }
            }
        }
    }
}