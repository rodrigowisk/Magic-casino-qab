using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Magic_casino_sportbook.Services
{
    public class BetSettlementService
    {
        private readonly AppDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string _oddsApiKey;
        private readonly string _betsApiToken;
        private readonly string _provider;

        public BetSettlementService(AppDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
            // Carrega as configurações do ambiente
            _oddsApiKey = Environment.GetEnvironmentVariable("ODDS_API_KEY") ?? "";
            _betsApiToken = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";
            _provider = Environment.GetEnvironmentVariable("ODDS_PROVIDER") ?? "BetsApi";
        }

        public async Task UpdatePendingBetsAsync()
        {
            // 1. Busca seleções pendentes onde o jogo já deve ter começado
            var pendingSelections = await _context.BetSelections
                .Where(s => s.Status == "pending" && s.CommenceTime < DateTime.UtcNow)
                .ToListAsync();

            if (!pendingSelections.Any()) return;

            // 2. Agrupa por MatchId para evitar chamadas repetidas
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
                    Console.WriteLine($"❌ Erro ao liquidar match {matchId}: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();

            // 4. Atualiza o status do bilhete principal (Won/Lost)
            await UpdateParentBetsStatusAsync();
        }

        private async Task SettleBetsApiMatch(string matchId, List<BetSelection> allPending)
        {
            // Endpoint de RESULTADOS da BetsAPI
            var url = $"https://api.betsapi.com/v1/bet365/result?token={_betsApiToken}&event_id={matchId}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array) return;

            var eventData = results.EnumerateArray().FirstOrDefault();

            // time_status "3" na BetsAPI significa jogo encerrado
            if (eventData.ValueKind != JsonValueKind.Undefined && eventData.GetProperty("time_status").GetString() == "3")
            {
                string scoreStr = eventData.GetProperty("ss").GetString() ?? ""; // Ex: "2-1"

                if (string.IsNullOrEmpty(scoreStr)) return;

                var selections = allPending.Where(s => s.MatchId == matchId).ToList();
                foreach (var sel in selections)
                {
                    sel.FinalScore = scoreStr;
                    sel.IsWinner = CalculateWinnerFromScore(scoreStr, sel.SelectionName);
                    sel.Status = sel.IsWinner == true ? "won" : "lost";
                }
            }
        }

        private async Task SettleTheOddsApiMatch(string matchId, List<BetSelection> allPending)
        {
            var url = $"https://api.the-odds-api.com/v4/sports/soccer/scores/?apiKey={_oddsApiKey}&eventIds={matchId}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var eventData = doc.RootElement.EnumerateArray().FirstOrDefault();

            if (eventData.ValueKind != JsonValueKind.Undefined && eventData.GetProperty("completed").GetBoolean())
            {
                var scores = eventData.GetProperty("scores");
                var hScore = scores[0].GetProperty("score").GetInt32();
                var aScore = scores[1].GetProperty("score").GetInt32();
                string hName = scores[0].GetProperty("name").GetString() ?? "";
                string aName = scores[1].GetProperty("name").GetString() ?? "";

                string scoreStr = $"{hScore}-{aScore}";

                var selections = allPending.Where(s => s.MatchId == matchId).ToList();
                foreach (var sel in selections)
                {
                    sel.FinalScore = scoreStr;
                    // Lógica para comparar nomes dos times da The Odds API
                    bool homeWin = hScore > aScore && sel.SelectionName.Contains(hName);
                    bool awayWin = aScore > hScore && sel.SelectionName.Contains(aName);
                    bool draw = hScore == aScore && (sel.SelectionName.ToLower().Contains("draw") || sel.SelectionName.ToLower().Contains("empate"));

                    sel.IsWinner = homeWin || awayWin || draw;
                    sel.Status = sel.IsWinner == true ? "won" : "lost";
                }
            }
        }

        private bool CalculateWinnerFromScore(string scoreStr, string selectionName)
        {
            var parts = scoreStr.Split('-');
            if (parts.Length < 2) return false;

            int.TryParse(parts[0], out int hScore);
            int.TryParse(parts[1], out int aScore);

            selectionName = selectionName.ToLower();

            // 1 = Casa, 2 = Fora, X = Empate (Lógica padrão de mercado 1X2)
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
                        // TODO: Integrar com CoreWalletService para pagar o prêmio aqui!
                    }
                }
            }
            await _context.SaveChangesAsync();
        }
    }
}