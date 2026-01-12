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
        private readonly string _apiKey = "SUA_API_KEY_AQUI"; // Use a mesma do SportsService

        public BetSettlementService(AppDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        public async Task UpdatePendingBetsAsync()
        {
            // 1. Busca seleções pendentes onde o jogo já deve ter começado
            var pendingSelections = await _context.BetSelections
                .Where(s => s.Status == "pending" && s.CommenceTime < DateTime.UtcNow)
                .ToListAsync();

            if (!pendingSelections.Any()) return;

            // 2. Agrupa por MatchId para não fazer requisições repetidas para o mesmo jogo
            var matchIds = pendingSelections.Select(s => s.MatchId).Distinct();

            foreach (var matchId in matchIds)
            {
                try
                {
                    // Consulta o endpoint de scores da The Odds API
                    // Exemplo: https://api.the-odds-api.com/v4/sports/soccer/scores/?apiKey=...&eventIds=...
                    var response = await _httpClient.GetAsync($"https://api.the-odds-api.com/v4/sports/soccer/scores/?apiKey={_apiKey}&eventIds={matchId}");

                    if (!response.IsSuccessStatusCode) continue;

                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var eventData = doc.RootElement.EnumerateArray().FirstOrDefault();

                    if (eventData.ValueKind != JsonValueKind.Undefined && eventData.GetProperty("completed").GetBoolean())
                    {
                        var scores = eventData.GetProperty("scores");
                        var homeScore = scores[0].GetProperty("score").GetInt32();
                        var awayScore = scores[1].GetProperty("score").GetInt32();
                        string homeTeam = scores[0].GetProperty("name").GetString() ?? "";
                        string awayTeam = scores[1].GetProperty("name").GetString() ?? "";

                        string finalResult = "";
                        if (homeScore > awayScore) finalResult = homeTeam;
                        else if (awayScore > homeScore) finalResult = awayTeam;
                        else finalResult = "Draw"; // Ou "Empate" conforme sua SelectionName

                        // 3. Atualiza todas as seleções deste jogo
                        var selectionsForThisMatch = pendingSelections.Where(s => s.MatchId == matchId);
                        foreach (var sel in selectionsForThisMatch)
                        {
                            sel.FinalScore = $"{homeScore}-{awayScore}";
                            // Lógica simples: Se o nome da seleção for igual ao vencedor ou se for empate e deu empate
                            sel.IsWinner = (sel.SelectionName.Contains(finalResult) ||
                                           (sel.SelectionName.ToLower().Contains("empate") && finalResult == "Draw"));

                            sel.Status = sel.IsWinner.Value ? "won" : "lost";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao liquidar match {matchId}: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();

            // 4. Após atualizar seleções, checa se a Aposta Pai (Bet) deve mudar para Won ou Lost
            await UpdateParentBetsStatusAsync();
        }

        private async Task UpdateParentBetsStatusAsync()
        {
            var activeBets = await _context.Bets
                .Include(b => b.Selections)
                .Where(b => b.Status == "confirmed")
                .ToListAsync();

            foreach (var bet in activeBets)
            {
                // Se todas as seleções foram resolvidas
                if (bet.Selections.All(s => s.Status != "pending"))
                {
                    // Se houver uma perdida, o bilhete está perdido
                    if (bet.Selections.Any(s => s.Status == "lost"))
                    {
                        bet.Status = "lost";
                    }
                    // Se todas ganharam, o bilhete está ganho
                    else if (bet.Selections.All(s => s.Status == "won"))
                    {
                        bet.Status = "won";
                        // TODO: Aqui chamaremos o serviço de pagamento para dar o dinheiro ao usuário!
                    }
                }
            }
            await _context.SaveChangesAsync();
        }
    }
}