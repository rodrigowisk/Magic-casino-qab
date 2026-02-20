using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Magic_Casino_Bot;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    // URL da API
    private const string API_BASE_URL = "http://quebrandoabanca.bet";
    // private const string API_BASE_URL = "http://localhost:8888"; 

    private const string BOT_PASSWORD = "SenhaPadraoBot123!";

    private List<BotAgent> _agents = new();

    public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(API_BASE_URL);
        client.Timeout = TimeSpan.FromSeconds(15);

        _logger.LogInformation("🤖 Bot Worker v7 (Rules Parser Fixed) iniciando...");

        await LoadAndConfigureBots(client, stoppingToken);

        _logger.LogInformation($"🚀 Exército pronto! {_agents.Count} bots ativos.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                int maxConcurrency = 5;

                var botsToAct = _agents
                    .Where(a => a.NextActionCheck <= now)
                    .OrderBy(x => Guid.NewGuid())
                    .Take(maxConcurrency)
                    .ToList();

                if (botsToAct.Any())
                {
                    var tasks = new List<Task>();
                    foreach (var bot in botsToAct)
                    {
                        bot.ScheduleNextAction();
                        tasks.Add(ProcessBotCycle(client, bot));
                    }
                    await Task.WhenAll(tasks);
                    await Task.Delay(1000, stoppingToken);
                }
                else
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro no loop principal: {ex.Message}");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task LoadAndConfigureBots(HttpClient client, CancellationToken token)
    {
        List<BotDto> botsData = new();
        while (!botsData.Any() && !token.IsCancellationRequested)
        {
            try
            {
                var response = await client.GetAsync("/api/bots/list");
                if (response.IsSuccessStatusCode)
                    botsData = await response.Content.ReadFromJsonAsync<List<BotDto>>() ?? new();
            }
            catch { }

            if (!botsData.Any()) await Task.Delay(5000, token);
        }

        botsData = botsData.OrderBy(x => Guid.NewGuid()).Take(1500).ToList();
        int total = botsData.Count;
        int groupSize = total / 3;

        for (int i = 0; i < total; i++)
        {
            BotGroup group;
            if (i < groupSize) group = BotGroup.Grupo1_Elite;
            else if (i < groupSize * 2) group = BotGroup.Grupo2_Medio;
            else group = BotGroup.Grupo3_Casual;

            var agent = new BotAgent(botsData[i].Cpf, botsData[i].UserName, botsData[i].Avatar, group);
            agent.ScheduleNextAction(firstRun: true);
            _agents.Add(agent);
        }
    }

    private async Task ProcessBotCycle(HttpClient client, BotAgent bot)
    {
        try
        {
            await EnsureLogin(client, bot);

            var tournaments = await GetActiveTournamentsAsync(client);
            if (!tournaments.Any()) return;

            // Entra em torneios aleatórios
            int numTournamentsToJoin = Random.Shared.Next(1, Math.Min(4, tournaments.Count + 1));
            var selectedTournaments = tournaments.OrderBy(x => Guid.NewGuid()).Take(numTournamentsToJoin).ToList();
            var joinedTournamentIds = new List<int>();

            foreach (var t in selectedTournaments)
            {
                int tId = t.GetProperty("id").GetInt32();
                var payload = new { UserName = bot.UserName, Avatar = bot.Avatar };
                await client.PostAsJsonAsync($"/api/tournament/{tId}/join", payload);
                joinedTournamentIds.Add(tId);
            }

            if (!joinedTournamentIds.Any()) return;

            // Busca TODOS os jogos disponíveis na API
            var allGames = await GetAvailableGamesAsync(client);
            if (!allGames.Any()) return;

            int numApostas = Random.Shared.Next(1, 6);

            for (int apostaAtuais = 1; apostaAtuais <= numApostas; apostaAtuais++)
            {
                int tId = joinedTournamentIds[Random.Shared.Next(joinedTournamentIds.Count)];

                // 1. Pega o torneio selecionado
                var currentTournament = selectedTournaments.FirstOrDefault(t => t.GetProperty("id").GetInt32() == tId);

                // 2. Extrai DATA DE TÉRMINO
                DateTime tournamentEndDate = DateTime.MaxValue;
                if (currentTournament.ValueKind != JsonValueKind.Undefined && currentTournament.TryGetProperty("endDate", out var dateProp))
                {
                    tournamentEndDate = dateProp.GetDateTime();
                }

                // 3. Extrai LIGAS PERMITIDAS (Lendo o JSON FilterRules)
                var allowedLeagues = ParseTournamentRules(currentTournament);

                // 4. APLICA OS FILTROS NOS JOGOS
                var validGames = allGames.Where(g =>
                {
                    // Regra A: Hora (Jogo deve começar antes do torneio acabar)
                    bool timeOk = false;
                    if (g.TryGetProperty("commenceTime", out var timeProp))
                    {
                        timeOk = timeProp.GetDateTime() < tournamentEndDate;
                    }

                    // Regra B: Liga (Nome da liga deve bater com o JSON)
                    bool leagueOk = true;
                    if (allowedLeagues.Any()) // Se a lista estiver vazia, assume que libera tudo (ou bloqueia, depende da sua regra de negócio)
                    {
                        string gameLeague = "";
                        if (g.TryGetProperty("league", out var lProp)) gameLeague = lProp.GetString() ?? "";
                        if (string.IsNullOrEmpty(gameLeague) && g.TryGetProperty("leagueName", out var lnProp)) gameLeague = lnProp.GetString() ?? "";

                        // Verifica se existe na lista (Ignorando maiúsculas/minúsculas)
                        leagueOk = allowedLeagues.Contains(gameLeague.Trim().ToLower());
                    }

                    return timeOk && leagueOk;
                }).ToList();

                // Se não sobrou nenhum jogo válido após os filtros, pula essa aposta
                if (!validGames.Any()) continue;

                decimal balance = await GetTournamentBalanceAsync(client, tId);
                if (balance <= 0) continue;

                // Lógica de Aposta (Valor)
                decimal betAmount = 0;
                bool isAllIn = false;
                if (balance < 5000)
                {
                    if (Random.Shared.Next(0, 100) < 60) { betAmount = balance; isAllIn = true; }
                    else continue;
                }
                else
                {
                    betAmount = GenerateSmartAmount();
                    if (betAmount > balance) betAmount = balance;
                }

                var strat = GenerateSelectionStrategy();
                var selections = PickSelections(validGames, strat.Count, strat.MinOdd, strat.MaxOdd);

                if (selections.Any())
                {
                    var betPayload = new { Amount = betAmount, Selections = selections };
                    var res = await client.PostAsJsonAsync($"/api/tournament/{tId}/bet", betPayload);
                    if (res.IsSuccessStatusCode)
                    {
                        _logger.LogInformation($"✅ [G{(int)bot.Group}] {bot.UserName} apostou R${betAmount} no Torneio #{tId} (Jogos filtrados: {validGames.Count})");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Erro ciclo bot: {ex.Message}");
        }
    }

    // ============================================================================================
    // 🛠 MÉTODOS AUXILIARES E PARSERS
    // ============================================================================================

    // NOVO PARSER ESPECÍFICO PARA O SEU JSON
    private HashSet<string> ParseTournamentRules(JsonElement tournament)
    {
        var leagues = new HashSet<string>();
        try
        {
            // Tenta encontrar o campo com o JSON. O nome no banco é 'FilterRules', na API pode ser 'filterRules'.
            JsonElement rulesJson = default;
            bool found = false;

            if (tournament.TryGetProperty("filterRules", out var fr)) { rulesJson = fr; found = true; }
            else if (tournament.TryGetProperty("FilterRules", out var FR)) { rulesJson = FR; found = true; }
            else if (tournament.TryGetProperty("rules", out var r)) { rulesJson = r; found = true; }

            if (!found) return leagues; // Retorna vazio se não achar regra

            // Se o campo vier como string (JSON escapado), desserializa primeiro
            if (rulesJson.ValueKind == JsonValueKind.String)
            {
                var text = rulesJson.GetString();
                if (string.IsNullOrEmpty(text)) return leagues;
                rulesJson = JsonSerializer.Deserialize<JsonElement>(text);
            }

            // Agora navega: sports -> [leagues]
            if (rulesJson.TryGetProperty("sports", out var sportsArr) && sportsArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var sport in sportsArr.EnumerateArray())
                {
                    // Opcional: Verificar se sport.key == "soccer". Por enquanto pega de todos os esportes listados.
                    if (sport.TryGetProperty("leagues", out var leaguesArr) && leaguesArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var leagueObj in leaguesArr.EnumerateArray())
                        {
                            // Tenta pegar o Nome da Liga
                            if (leagueObj.TryGetProperty("name", out var nameProp))
                            {
                                string name = nameProp.GetString()?.Trim().ToLower() ?? "";
                                if (!string.IsNullOrEmpty(name)) leagues.Add(name);
                            }
                            // Tenta pegar o ID também por segurança (às vezes o jogo vem com ID em vez do nome)
                            if (leagueObj.TryGetProperty("id", out var idProp))
                            {
                                string id = idProp.GetString()?.Trim().ToLower() ?? "";
                                if (!string.IsNullOrEmpty(id)) leagues.Add(id);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log de erro silencioso para não parar o bot
            // Console.WriteLine("Erro ao ler regras: " + ex.Message); 
        }
        return leagues;
    }

    private async Task EnsureLogin(HttpClient client, BotAgent bot)
    {
        if (!string.IsNullOrEmpty(bot.Token) && bot.TokenExpiration > DateTime.UtcNow)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bot.Token);
            return;
        }
        try
        {
            client.DefaultRequestHeaders.Authorization = null;
            var res = await client.PostAsJsonAsync("/api/user/login", new { Code = bot.Cpf, Password = BOT_PASSWORD });
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadFromJsonAsync<JsonElement>();
                string token = "";
                if (json.TryGetProperty("token", out var t)) token = t.GetString() ?? "";
                else if (json.TryGetProperty("Token", out var t2)) token = t2.GetString() ?? "";

                if (!string.IsNullOrEmpty(token))
                {
                    bot.Token = token;
                    bot.TokenExpiration = DateTime.UtcNow.AddMinutes(55);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }
        }
        catch { }
    }

    private async Task<List<JsonElement>> GetActiveTournamentsAsync(HttpClient client)
    {
        try
        {
            var r = await client.GetAsync("/api/tournament/");
            if (r.IsSuccessStatusCode) return JsonSerializer.Deserialize<List<JsonElement>>(await r.Content.ReadAsStringAsync()) ?? new();
        }
        catch { }
        return new();
    }

    private async Task<List<JsonElement>> GetAvailableGamesAsync(HttpClient client)
    {
        try
        {
            // O ideal seria passar a data na URL, mas filtramos em memória por segurança
            var r = await client.GetAsync("/sportbook/api/sports/events?sport=soccer&page=1&pageSize=100");
            if (r.IsSuccessStatusCode) return JsonSerializer.Deserialize<List<JsonElement>>(await r.Content.ReadAsStringAsync()) ?? new();
        }
        catch { }
        return new();
    }

    private async Task<decimal> GetTournamentBalanceAsync(HttpClient client, int tId)
    {
        try
        {
            var r = await client.GetAsync($"/api/tournament/{tId}/balance");
            if (r.IsSuccessStatusCode)
            {
                var el = await r.Content.ReadFromJsonAsync<JsonElement>();
                if (el.ValueKind == JsonValueKind.Number) return el.GetDecimal();
                if (el.TryGetProperty("balance", out var b)) return b.GetDecimal();
            }
        }
        catch { }
        return 100000m;
    }

    private int GenerateSmartAmount()
    {
        int r = Random.Shared.Next(0, 100);
        int b = r < 40 ? 50 : (r < 80 ? 100 : 1000);
        return Random.Shared.Next(Math.Max(1, 100 / b), 10000 / b + 1) * b;
    }

    private (int Count, decimal MinOdd, decimal MaxOdd) GenerateSelectionStrategy()
    {
        decimal min = 1.80m, max = 2.90m;
        if (Random.Shared.Next(0, 100) >= 75) { min = 4.00m; max = 50.00m; }
        int c = 1; int r = Random.Shared.Next(0, 100);
        if (r >= 70 && r < 85) c = Random.Shared.Next(2, 5); else if (r >= 85) c = Random.Shared.Next(5, 11);
        return (c, min, max);
    }

    private List<object> PickSelections(List<JsonElement> games, int count, decimal min, decimal max)
    {
        var sels = new List<object>();
        var used = new HashSet<string>();
        foreach (var g in games.OrderBy(x => Guid.NewGuid()))
        {
            if (sels.Count >= count) break;
            string id = g.GetProperty("externalId").GetString() ?? "";
            if (string.IsNullOrEmpty(id) || used.Contains(id)) continue;

            decimal h = g.GetProperty("rawOddsHome").GetDecimal();
            decimal d = g.GetProperty("rawOddsDraw").GetDecimal();
            decimal a = g.GetProperty("rawOddsAway").GetDecimal();
            var opts = new List<(string S, decimal O)>();
            if (h >= min && h <= max) opts.Add(("1", h));
            if (d >= min && d <= max) opts.Add(("X", d));
            if (a >= min && a <= max) opts.Add(("2", a));
            if (opts.Any())
            {
                var p = opts[Random.Shared.Next(opts.Count)];
                sels.Add(new { GameId = id, HomeTeam = g.GetProperty("homeTeam").GetString(), AwayTeam = g.GetProperty("awayTeam").GetString(), SelectionName = p.S, MarketName = "Resultado Final", Odds = p.O });
                used.Add(id);
            }
        }
        return sels;
    }
}

public class BotDto
{
    public string Cpf { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Avatar { get; set; } = "";
}

public class BotAgent
{
    public string Cpf { get; }
    public string UserName { get; }
    public string Avatar { get; }
    public BotGroup Group { get; }
    public string Token { get; set; } = "";
    public DateTime TokenExpiration { get; set; } = DateTime.MinValue;
    public DateTime NextActionCheck { get; set; }

    public BotAgent(string cpf, string userName, string avatar, BotGroup group)
    {
        Cpf = cpf;
        UserName = userName;
        Avatar = avatar ?? "";
        Group = group;
    }

    public void ScheduleNextAction(bool firstRun = false)
    {
        int minSec = 0, maxSec = 0;
        switch (Group)
        {
            case BotGroup.Grupo1_Elite: minSec = 30; maxSec = 120; break;
            case BotGroup.Grupo2_Medio: minSec = 300; maxSec = 600; break;
            case BotGroup.Grupo3_Casual: minSec = 600; maxSec = 1800; break;
        }
        int delay = firstRun ? Random.Shared.Next(0, maxSec) : Random.Shared.Next(minSec, maxSec);
        NextActionCheck = DateTime.UtcNow.AddSeconds(delay);
    }
}

public enum BotGroup { Grupo1_Elite = 1, Grupo2_Medio = 2, Grupo3_Casual = 3 }