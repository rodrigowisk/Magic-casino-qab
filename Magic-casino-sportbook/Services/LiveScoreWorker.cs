using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Magic_casino_sportbook.Services
{
    public class LiveScoreWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;

        // ⚠️ LEMBRE-SE: Substitua pela sua chave real da The Odds API
        private readonly string _apiKey = "SUA_API_KEY_AQUI";

        public LiveScoreWorker(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // O loop roda enquanto o servidor estiver ligado
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await UpdateLiveScores(context);
                    }
                }
                catch (Exception ex)
                {
                    // Log de erro para evitar que uma falha na API trave o serviço inteiro
                    Console.WriteLine($"Erro no LiveScoreWorker: {ex.Message}");
                }

                // Aguarda 1 minuto antes da próxima verificação
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task UpdateLiveScores(AppDbContext context)
        {
            // 1. Busca MatchIds de apostas que estão com status "pending"
            var activeMatchIds = await context.BetSelections
                .Where(s => s.Status == "pending")
                .Select(s => s.MatchId)
                .Distinct()
                .ToListAsync();

            if (!activeMatchIds.Any()) return;

            var client = _httpClientFactory.CreateClient();

            // 2. Consulta a API de Scores filtrando pelos IDs ativos no seu banco
            var url = $"https://api.the-odds-api.com/v4/sports/soccer/scores/?apiKey={_apiKey}&eventIds={string.Join(",", activeMatchIds)}";
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();

                // ✅ Agora o C# reconhecerá esta classe pois a definimos abaixo
                var liveData = JsonSerializer.Deserialize<List<OddsApiScoreResponse>>(json);

                if (liveData == null) return;

                foreach (var game in liveData)
                {
                    // 3. Localiza as seleções desse jogo no banco
                    var selections = await context.BetSelections
                        .Where(s => s.MatchId == game.id && s.Status == "pending")
                        .ToListAsync();

                    foreach (var sel in selections)
                    {
                        // Atualiza o placar em tempo real no banco se houver dados disponíveis
                        if (game.scores != null && game.scores.Count >= 2)
                        {
                            // Grava no formato "HomeScore-AwayScore"
                            sel.FinalScore = $"{game.scores[0].score}-{game.scores[1].score}";
                        }
                    }
                }

                // Salva as alterações de placar no banco de dados
                await context.SaveChangesAsync();
            }
        }
    }

    // ✅ CLASSES DE APOIO PARA O DESERIALIZER (Resolvem o erro vermelho)
    public class OddsApiScoreResponse
    {
        public string id { get; set; } = string.Empty;
        public string sport_key { get; set; } = string.Empty;
        public DateTime? commence_time { get; set; }
        public bool completed { get; set; }
        public List<OddsApiScoreItem>? scores { get; set; }
    }

    public class OddsApiScoreItem
    {
        public string name { get; set; } = string.Empty;
        public string score { get; set; } = string.Empty;
    }
}