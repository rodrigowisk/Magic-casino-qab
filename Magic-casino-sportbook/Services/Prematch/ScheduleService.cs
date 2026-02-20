using System.Text.Json;
using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Data.Models;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Services.Gateways; // Usa o Gateway que criamos
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Magic_casino_sportbook.Services.Prematch
{
    public interface IScheduleService
    {
        Task SyncEventsSchedule();
    }

    public class ScheduleService : IScheduleService
    {
        private readonly BetsApiHttpService _api; // ✅ Usa o Gateway Novo
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScheduleService> _logger;

        private const int MAX_PAGES_PER_SPORT = 20;

        // Mapeamento de Esportes (Mantido do original)
        private readonly Dictionary<int, string> _sportMap = new() {
            { 1, "soccer" }, { 13, "tennis" }, { 18, "basketball" }, { 91, "volleyball" },
            { 12, "american-football" }, { 16, "baseball" }, { 17, "ice-hockey" },
            { 78, "handball" }, { 8, "rugby-union" }, { 9, "boxing" }, { 3, "cricket" },
            { 92, "table-tennis" }, { 94, "badminton" }, { 95, "futsal" },
            { 19, "snooker" }, { 14, "darts" }, { 36, "australian-rules" }, { 151, "esports" }
        };

        private readonly string[] _excludedTerms = new[]
        {
            "ESOCCER", "E-SOCCER", "E SOCCER", "EBASKETBALL", "E-BASKETBALL",
            "E BASKETBALL", "EVOLLEYBALL", "E-VOLLEYBALL", "E-HOCKEY", "EHOCKEY",
            "E-CRICKET", "ECRICKET", "VIRTUAL", "CYBER", "SIMULATED",
            "GT LEAGUE", "BATTLE", "2X2", "4X4", "PENALTY", "E-SPORT", "ESPORT", "FIFA", "NBA 2K"
        };

        public ScheduleService(
            BetsApiHttpService api,
            IServiceScopeFactory scopeFactory,
            ILogger<ScheduleService> logger)
        {
            _api = api;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task SyncEventsSchedule()
        {
            _logger.LogInformation("📅 [ScheduleService] Iniciando varredura de grade...");

            using (var configScope = _scopeFactory.CreateScope())
            {
                var configContext = configScope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Carrega configurações de bloqueio
                var disabledConfigs = await configContext.SportConfigurations.AsNoTracking().Where(c => c.IsEnabled == false).ToListAsync();
                var disabledSports = new HashSet<string>(disabledConfigs.Where(c => c.Type == "SPORT").Select(c => c.Identifier));
                var disabledLeagues = new HashSet<string>(disabledConfigs.Where(c => c.Type == "LEAGUE").Select(c => c.Identifier));
                var disabledTeams = new HashSet<string>(disabledConfigs.Where(c => c.Type == "TEAM").Select(c => c.Identifier));

                foreach (var sport in _sportMap)
                {
                    if (disabledSports.Contains(sport.Value)) continue;

                    int totalGamesFound = 0;

                    for (int page = 1; page <= MAX_PAGES_PER_SPORT; page++)
                    {
                        try
                        {
                            // Chama o método interno de processamento da página
                            var gamesCount = await ProcessSportPageAsync(sport.Key, sport.Value, page, disabledLeagues, disabledTeams);

                            if (gamesCount == 0) break; // Se a página veio vazia, paramos de paginar este esporte

                            totalGamesFound += gamesCount;

                            // Delay leve para não bombardear o Gateway, embora ele já tenha controle de fila
                            await Task.Delay(500);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"⚠️ [ScheduleService] Erro pág {page} {sport.Value}: {ex.Message}");
                            break;
                        }
                    }

                    if (totalGamesFound > 0)
                    {
                        _logger.LogInformation($"✅ [ScheduleService] {sport.Value}: {totalGamesFound} jogos processados.");
                    }
                }
            }

            _logger.LogInformation("🏁 [ScheduleService] Varredura finalizada.");
        }

        private async Task<int> ProcessSportPageAsync(int sportId, string sportKey, int page, HashSet<string> disabledLeagues, HashSet<string> disabledTeams)
        {
            var url = $"/v1/bet365/upcoming?sport_id={sportId}&page={page}";

            // ✅ Usa o Gateway centralizado (ele gerencia o Token e o Rate Limit)
            var response = await _api.GetAsync(url);

            if (!response.IsSuccessStatusCode) return 0;

            try
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                JsonElement results = default;

                if (doc.RootElement.ValueKind == JsonValueKind.Array) results = doc.RootElement;
                else if (doc.RootElement.TryGetProperty("results", out var res)) results = res;

                if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return 0;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var pageItems = results.EnumerateArray().ToList();

                    // Extrai IDs para consulta em lote (Performance)
                    var pageIds = pageItems
                        .Select(item => GetStringSafe(item, "id") ?? "")
                        .Where(id => !string.IsNullOrEmpty(id))
                        .ToList();

                    var existingGamesDict = await context.SportsEvents
                        .Where(e => pageIds.Contains(e.ExternalId))
                        .ToDictionaryAsync(e => e.ExternalId);

                    var newEvents = new List<SportsEvent>();
                    int validGamesOnPage = 0;

                    foreach (var item in pageItems)
                    {
                        string id = GetStringSafe(item, "id") ?? "";
                        if (string.IsNullOrEmpty(id)) continue;

                        string home = "", away = "", leagueName = "Outros";
                        string? rawHomeId = null, rawAwayId = null, rawLeagueId = null, rawCc = null;

                        if (item.TryGetProperty("league", out var lObj))
                        {
                            leagueName = GetStringSafe(lObj, "name") ?? "Outros";
                            rawLeagueId = GetStringSafe(lObj, "id");
                            rawCc = GetStringSafe(lObj, "cc");
                        }

                        if (item.TryGetProperty("home", out var hObj))
                        {
                            home = GetStringSafe(hObj, "name") ?? "";
                            rawHomeId = GetStringSafe(hObj, "id");
                        }
                        if (item.TryGetProperty("away", out var aObj))
                        {
                            away = GetStringSafe(aObj, "name") ?? "";
                            rawAwayId = GetStringSafe(aObj, "id");
                        }

                        // Filtros de qualidade
                        if (IsFakeSport(leagueName, home, away)) continue;
                        if (disabledLeagues.Contains(leagueName)) continue;
                        if ((rawHomeId != null && disabledTeams.Contains(rawHomeId)) || (rawAwayId != null && disabledTeams.Contains(rawAwayId))) continue;

                        validGamesOnPage++;

                        long.TryParse(GetStringSafe(item, "time"), out long timeUnix);
                        var commenceTime = DateTimeOffset.FromUnixTimeSeconds(timeUnix).UtcDateTime;

                        string apiStatus = GetStringSafe(item, "time_status") ?? "0";
                        string dbStatus = (apiStatus == "1") ? "Live" : "Prematch";

                        // Lógica de Update ou Insert
                        if (existingGamesDict.TryGetValue(id, out var existingEvent))
                        {
                            // Se mudou o horário
                            if (existingEvent.CommenceTime != commenceTime)
                            {
                                existingEvent.CommenceTime = commenceTime;
                                existingEvent.LastUpdate = DateTime.UtcNow;
                            }

                            // Preenche IDs faltantes se a API mandar agora
                            if (string.IsNullOrEmpty(existingEvent.LeagueId) && IsValidImageId(rawLeagueId))
                            {
                                existingEvent.LeagueId = rawLeagueId;
                                existingEvent.LeagueExternalId = rawLeagueId;
                            }
                            if (string.IsNullOrEmpty(existingEvent.HomeTeamId) && IsValidImageId(rawHomeId)) existingEvent.HomeTeamId = rawHomeId;
                            if (string.IsNullOrEmpty(existingEvent.AwayTeamId) && IsValidImageId(rawAwayId)) existingEvent.AwayTeamId = rawAwayId;

                            // Se virou Live no meio do scan, atualiza
                            if (dbStatus == "Live" && existingEvent.Status != "Live") existingEvent.Status = "Live";
                        }
                        else
                        {
                            // Novo jogo
                            newEvents.Add(new SportsEvent
                            {
                                ExternalId = id,
                                HomeTeam = home,
                                AwayTeam = away,
                                League = leagueName,
                                CommenceTime = commenceTime,
                                SportKey = sportKey,
                                SportCategory = sportKey,
                                OddsSource = "BetsAPI_Premium",
                                LastUpdate = DateTime.UtcNow,
                                HomeTeamId = IsValidImageId(rawHomeId) ? rawHomeId : null,
                                AwayTeamId = IsValidImageId(rawAwayId) ? rawAwayId : null,
                                LeagueId = IsValidImageId(rawLeagueId) ? rawLeagueId : null,
                                LeagueExternalId = IsValidImageId(rawLeagueId) ? rawLeagueId : null,
                                CountryCode = !string.IsNullOrEmpty(rawCc) ? rawCc : null,
                                // URLs de imagem já preenchidas (O MetadataService pode corrigir depois se falhar)
                                HomeTeamLogo = IsValidImageId(rawHomeId) ? $"https://assets.b365api.com/images/team/m/{rawHomeId}.png" : null,
                                AwayTeamLogo = IsValidImageId(rawAwayId) ? $"https://assets.b365api.com/images/team/m/{rawAwayId}.png" : null,
                                Status = dbStatus,
                                Odds = new List<EventMarket>()
                            });
                        }
                    }

                    if (newEvents.Any())
                    {
                        context.SportsEvents.AddRange(newEvents);
                    }

                    if (newEvents.Any() || context.ChangeTracker.HasChanges())
                    {
                        await context.SaveChangesAsync();
                    }

                    return validGamesOnPage;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no processamento da página.");
                return 0;
            }
        }

        // ==============================================================================
        // Helpers
        // ==============================================================================
        private string? GetStringSafe(JsonElement el, string key) =>
            el.TryGetProperty(key, out var p) ? (p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString()) : null;

        private bool IsValidImageId(string? id) => !string.IsNullOrEmpty(id) && id != "0";

        private bool IsFakeSport(string l, string h, string a)
        {
            var text = $"{l} {h} {a}".ToUpper();
            return _excludedTerms.Any(term => text.Contains(term));
        }
    }
}