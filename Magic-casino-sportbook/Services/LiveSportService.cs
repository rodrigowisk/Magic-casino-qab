using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.DTOs.Live;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Magic_casino_sportbook.Services
{
    public class LiveSportService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiToken;
        private const string BASE_URL = "https://api.b365api.com";

        public LiveSportService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _apiToken = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";
        }

        // ==============================================================================
        // 🛠️ ATUALIZA DADOS DE JOGOS QUE JÁ ESTÃO AO VIVO (Odds, Placar, Tempo)
        // ==============================================================================
        public async Task<List<string>> UpdateLiveGamesAsync(List<SportsEvent> liveGames, AppDbContext context)
        {
            var endedGameIds = new List<string>();
            if (!liveGames.Any()) return endedGameIds;

            // Agrupa IDs para fazer uma única requisição (Batching)
            var gameIds = string.Join(",", liveGames.Select(g => g.ExternalId));
            var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={gameIds}";

            var client = _httpClientFactory.CreateClient();

            try
            {
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return endedGameIds;

                var jsonString = await response.Content.ReadAsStringAsync();

                // Parse usando os DTOs criados
                var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString);

                if (data?.Results == null) return endedGameIds;

                foreach (var gamePackets in data.Results)
                {
                    await ProcessSingleGamePackets(gamePackets, context, endedGameIds);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao atualizar jogos ao vivo: {ex.Message}");
            }

            return endedGameIds;
        }

        private async Task ProcessSingleGamePackets(List<B365Packet> packets, AppDbContext context, List<string> endedIds)
        {
            // 1. Identificar o Evento Principal ("EV")
            var ev = packets.FirstOrDefault(p => p.Type == "EV");
            if (ev == null) return;

            var game = await context.SportsEvents.FirstOrDefaultAsync(g => g.ExternalId == ev.Id);
            if (game == null) return; // Jogo não está no nosso banco

            // 2. Atualizar Tabela Principal (SportsEvents)
            game.LastUpdate = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(ev.ScoreString))
                game.Score = ev.ScoreString;

            // ✅ CORREÇÃO CRÍTICA: Atualizar o tempo na tabela principal para não ficar NULL
            if (!string.IsNullOrEmpty(ev.Time))
            {
                game.GameTime = ev.Time;
            }

            // Verifica Fim de Jogo
            if (ev.Status == "3" || ev.Time == "FT" || ev.Time == "Ended")
            {
                game.Status = "Ended";
                endedIds.Add(game.ExternalId);
            }

            // 3. Atualizar/Criar LiveGameStat (Tabela Auxiliar)
            var liveStat = await context.LiveGameStat.FirstOrDefaultAsync(l => l.GameId == game.Id);

            if (liveStat == null)
            {
                liveStat = new LiveGameStat
                {
                    GameId = game.Id,
                    Period = "1H",
                    IsFinished = false
                };
                context.LiveGameStat.Add(liveStat);
            }

            // Parse do Placar (Ex: "2-1")
            if (!string.IsNullOrEmpty(ev.ScoreString) && ev.ScoreString.Contains("-"))
            {
                var parts = ev.ScoreString.Split('-');
                if (int.TryParse(parts[0], out int h)) liveStat.HomeScore = h;
                if (int.TryParse(parts[1], out int a)) liveStat.AwayScore = a;
            }

            liveStat.CurrentMinute = ev.Time ?? liveStat.CurrentMinute;
            liveStat.LastUpdated = DateTime.UtcNow;

            // 4. Atualizar Odds (Buscando nos pacotes "PA")
            var oddsPackets = packets.Where(p => p.Type == "PA" && !string.IsNullOrEmpty(p.Odds)).ToList();

            foreach (var odd in oddsPackets)
            {
                decimal valorDecimal = ConvertFraction(odd.Odds);
                if (valorDecimal <= 1.0m) continue;

                // Mapeamento simples baseada no Nome (1, X, 2)
                if (odd.Name == "1" || odd.Name == game.HomeTeam)
                    game.RawOddsHome = valorDecimal;
                else if (odd.Name == "X" || odd.Name == "Draw")
                    game.RawOddsDraw = valorDecimal;
                else if (odd.Name == "2" || odd.Name == game.AwayTeam)
                    game.RawOddsAway = valorDecimal;
            }
        }

        // ==============================================================================
        // 🛠️ ROTINA DE KICKOFF: VERIFICA QUEM VIROU LIVE E RETORNA A LISTA
        // ==============================================================================
        public async Task<List<string>> CheckForKickoffAsync(List<SportsEvent> games, AppDbContext context)
        {
            var transitionedIds = new List<string>();

            foreach (var g in games)
            {
                g.LastUpdate = DateTime.UtcNow;

                // Regra: Se passou do horário de início
                if (DateTime.UtcNow >= g.CommenceTime)
                {
                    // Só marca a transição se ele AINDA NÃO era Live
                    if (g.Status != "Live")
                    {
                        g.Status = "Live";
                        transitionedIds.Add(g.ExternalId); // Guarda o ID para o SignalR remover do pré-jogo
                    }
                }
            }

            // Se houve mudanças, salva no banco
            if (transitionedIds.Any())
            {
                await context.SaveChangesAsync();
            }

            return transitionedIds;
        }

        private decimal ConvertFraction(string fraction)
        {
            if (string.IsNullOrEmpty(fraction) || fraction == "0/0") return 0m;
            try
            {
                if (!fraction.Contains("/")) return decimal.Parse(fraction);
                var parts = fraction.Split('/');
                if (parts.Length == 2)
                    return (decimal.Parse(parts[0]) / decimal.Parse(parts[1])) + 1;
            }
            catch { }
            return 0m;
        }
    }
}