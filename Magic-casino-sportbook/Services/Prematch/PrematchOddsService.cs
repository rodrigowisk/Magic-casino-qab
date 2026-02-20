using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Data.Models;
using Magic_casino_sportbook.Hubs;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Services.Gateways;
using Magic_casino_sportbook.Services.Parsers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Magic_casino_sportbook.Services.Prematch
{
    public interface IPrematchOddsService
    {
        Task SyncPrematchOdds();
    }

    public class PrematchOddsService : IPrematchOddsService
    {
        private readonly BetsApiHttpService _api; // Gateway HTTP
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PrematchOddsService> _logger;
        private readonly List<IMarketParser> _parsers;

        private const int REQUEST_CHUNK_SIZE = 50;

        public PrematchOddsService(
            BetsApiHttpService api,
            IServiceScopeFactory scopeFactory,
            ILogger<PrematchOddsService> logger)
        {
            _api = api;
            _scopeFactory = scopeFactory;
            _logger = logger;

            // Inicializa os parsers que você já tem na pasta Services/Parsers
            _parsers = new List<IMarketParser>
            {
                new MainParser(),
                new AsianParser(),
                new GoalsParser(),
                new PropsParser()
            };
        }

        public async Task SyncPrematchOdds()
        {
            _logger.LogInformation("♻️ [PrematchOddsService] Iniciando varredura de odds...");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Busca jogos futuros que estão sem odds ou desatualizados
                var gamesToUpdate = await context.SportsEvents
                    .AsNoTracking()
                    .Where(g => g.CommenceTime > DateTime.UtcNow &&
                               (g.RawOddsHome == 0 || g.LastUpdate < DateTime.UtcNow.AddHours(-1)))
                    .OrderBy(g => g.CommenceTime)
                    .Take(400) // Limite de segurança por ciclo
                    .Select(g => new { g.ExternalId, g.SportKey })
                    .ToListAsync();

                if (gamesToUpdate.Any())
                {
                    _logger.LogInformation($"♻️ [PrematchOddsService] {gamesToUpdate.Count} jogos pendentes de atualização.");

                    // Agrupa por esporte para otimizar logs (opcional, mas mantido da lógica original)
                    var grouped = gamesToUpdate.GroupBy(g => g.SportKey ?? "soccer");

                    foreach (var grp in grouped)
                    {
                        var ids = grp.Select(x => x.ExternalId).ToList();
                        await FetchOddsList(ids, grp.Key);
                    }
                }
            }
        }

        private async Task FetchOddsList(List<string> allIds, string sportKey)
        {
            var chunks = allIds.Chunk(REQUEST_CHUNK_SIZE).ToList();

            foreach (var chunk in chunks)
            {
                await ProcessBatchWithRetries(chunk.ToList(), sportKey);
            }
        }

        private async Task ProcessBatchWithRetries(List<string> batchIds, string sportKey, int retryLevel = 0)
        {
            if (batchIds.Count == 0) return;

            var idsStr = string.Join(",", batchIds);
            var url = $"/v1/bet365/prematch?FI={idsStr}";

            // Delay exponencial simples em caso de retentativa
            if (retryLevel > 0) await Task.Delay(500 + (retryLevel * 1000));

            try
            {
                // ✅ Usa o Gateway Novo
                var response = await _api.GetAsync(url);

                if ((int)response.StatusCode == 429)
                {
                    _logger.LogWarning($"⛔ [ODDS 429] Lote bloqueado. Nível {retryLevel}. Dividindo...");
                    if (batchIds.Count > 1) await SplitAndRetry(batchIds, sportKey, retryLevel + 1);
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    if (batchIds.Count > 1) await SplitAndRetry(batchIds, sportKey, retryLevel + 1);
                    return;
                }

                var jsonString = await response.Content.ReadAsStringAsync();

                // Lógica de "Contaminação" da BetsAPI (jogos inválidos quebram o lote inteiro)
                if (jsonString.Contains("failure") || jsonString.Contains("PARAM_INVALID"))
                {
                    _logger.LogWarning($"⚠️ [API] Lote contaminado. Dividindo para isolar o erro...");
                    if (batchIds.Count > 1) await SplitAndRetry(batchIds, sportKey, retryLevel + 1);
                    return;
                }

                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    try
                    {
                        using var doc = JsonDocument.Parse(jsonString);
                        if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                        {
                            await ProcessOddsResults(context, results, sportKey);
                        }
                    }
                    catch (JsonException)
                    {
                        // JSON quebrado também força divisão
                        if (batchIds.Count > 1) await SplitAndRetry(batchIds, sportKey, retryLevel + 1);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Erro no lote: {ex.Message}");
                if (batchIds.Count > 1) await SplitAndRetry(batchIds, sportKey, retryLevel + 1);
            }
        }

        private async Task SplitAndRetry(List<string> ids, string sportKey, int nextLevel)
        {
            // Estratégia Dividir e Conquistar para isolar IDs ruins
            int mid = ids.Count / 2;
            await ProcessBatchWithRetries(ids.Take(mid).ToList(), sportKey, nextLevel);
            await ProcessBatchWithRetries(ids.Skip(mid).ToList(), sportKey, nextLevel);
        }

        private async Task ProcessOddsResults(AppDbContext context, JsonElement results, string sportKey)
        {
            var ids = new List<string>();
            foreach (var item in results.EnumerateArray())
            {
                string fi = GetStringSafe(item, "FI");
                if (!string.IsNullOrEmpty(fi)) ids.Add(fi);
            }

            var dbEvents = await context.SportsEvents
                .Include(e => e.Odds)
                .Where(e => ids.Contains(e.ExternalId))
                .ToListAsync();

            foreach (var item in results.EnumerateArray())
            {
                string fi = GetStringSafe(item, "FI");
                var game = dbEvents.FirstOrDefault(e => e.ExternalId == fi);
                if (game == null) continue;

                ApplyOddsToGame(item, game, context);
                game.LastUpdate = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();
        }

        private void ApplyOddsToGame(JsonElement eventNode, SportsEvent game, AppDbContext context)
        {
            var moneyLineMarkets = new List<MarketDto>();

            // 1. Extrai todos os mercados usando os Parsers especialistas
            foreach (var parser in _parsers)
            {
                var parsedMarkets = parser.Parse(eventNode, game.SportKey ?? "soccer");

                foreach (var m in parsedMarkets)
                {
                    // Tenta achar a odd existente na memória do EF ou no banco
                    var existing = game.Odds.FirstOrDefault(o => o.MarketName == m.MarketName && o.OutcomeName == m.OutcomeName);

                    if (existing == null)
                    {
                        // Busca no Local Tracker caso tenha sido adicionada em outro contexto recentemente
                        existing = context.EventMarkets.Local
                            .FirstOrDefault(x => x.SportsEventId == game.Id && x.MarketName == m.MarketName && x.OutcomeName == m.OutcomeName);
                    }

                    if (existing != null)
                    {
                        existing.Price = m.Price;
                        existing.LastUpdate = DateTime.UtcNow;
                    }
                    else
                    {
                        var newOdd = new EventMarket
                        {
                            SportsEventId = game.Id,
                            ExternalId = m.ExternalId,
                            MarketName = m.MarketName,
                            OutcomeName = m.OutcomeName,
                            Price = m.Price,
                            Handicap = m.Handicap,
                            LastUpdate = DateTime.UtcNow
                        };
                        game.Odds.Add(newOdd);
                    }

                    // Filtra mercados para atualizar as colunas rápidas (RawOdds)
                    string mName = m.MarketName?.Trim().ToUpper() ?? "";
                    if (mName.Contains("MONEY LINE") || mName.Contains("WINNER") || mName.Contains("VENCEDOR") ||
                        mName.Contains("RESULT") || mName.Contains("FULL") || mName.Contains("1X2") || mName == "12")
                    {
                        moneyLineMarkets.Add(m);
                    }
                }
            }

            // 2. Atualiza as colunas de acesso rápido (RawOddsHome/Draw/Away)
            bool foundByLabel = false;
            foreach (var m in moneyLineMarkets)
            {
                if (IsHome(m.OutcomeName, game.HomeTeam)) { game.RawOddsHome = m.Price; foundByLabel = true; }
                else if (IsDraw(m.OutcomeName)) { game.RawOddsDraw = m.Price; foundByLabel = true; }
                else if (IsAway(m.OutcomeName, game.AwayTeam)) { game.RawOddsAway = m.Price; foundByLabel = true; }
            }

            // Fallback (Ordem de aparição) se não casar os nomes
            if (!foundByLabel && moneyLineMarkets.Count >= 2)
            {
                game.RawOddsHome = moneyLineMarkets[0].Price;
                game.RawOddsAway = moneyLineMarkets[1].Price;
                if (moneyLineMarkets.Count == 3)
                {
                    game.RawOddsDraw = moneyLineMarkets[1].Price;
                    game.RawOddsAway = moneyLineMarkets[2].Price;
                }
            }
        }

        // ==============================================================================
        // Helpers (Privados para não vazar lógica)
        // ==============================================================================
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

        private string? GetStringSafe(JsonElement el, string key) =>
            el.TryGetProperty(key, out var p) ? (p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString()) : null;
    }
}