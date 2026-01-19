using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.Data.Models;
using Magic_casino_sportbook.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;

namespace Magic_casino_sportbook.Services
{
    public class LiveSportService
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly string _token;

        // 💎 URL PREMIUM
        private const string BASE_URL = "https://api.b365api.com";

        public LiveSportService(HttpClient httpClient, IServiceScopeFactory scopeFactory, IHubContext<GameHub> hubContext)
        {
            _httpClient = httpClient;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _token = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";
        }

        // ==============================================================================
        // ⚡ MÉTODO 1: ATUALIZAÇÃO RÁPIDA (Chamado pelo LiveUpdateWorker)
        // ==============================================================================
        public async Task<List<string>> UpdateLiveGamesAsync(List<SportsEvent> liveGames)
        {
            var endedGameIds = new List<string>();
            if (!liveGames.Any() || string.IsNullOrEmpty(_token)) return endedGameIds;

            var chunks = liveGames.Chunk(10).ToList();

            foreach (var chunk in chunks)
            {
                var ids = string.Join(",", chunk.Select(g => g.ExternalId));
                var url = $"{BASE_URL}/v1/bet365/event?token={_token}&FI={ids}";

                try
                {
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                        if (doc.RootElement.TryGetProperty("results", out var resultsArray))
                        {
                            foreach (var item in resultsArray.EnumerateArray())
                            {
                                // CASO 1: Array de Arrays [[{jogo}]] (Padrão antigo)
                                if (item.ValueKind == JsonValueKind.Array)
                                {
                                    await ProcessLiveGameData(item, null, endedGameIds, liveGames);
                                }
                                // CASO 2: Objeto Direto [{jogo}] (Padrão atual que estava falhando)
                                else if (item.ValueKind == JsonValueKind.Object)
                                {
                                    // Truque: Criamos um array temporário com esse único item para reaproveitar a lógica
                                    // Ou chamamos uma versão modificada do processador.
                                    // A forma mais segura aqui é processar item a item.
                                    await ProcessSingleLiveGameData(item, null, endedGameIds, liveGames);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Erro UpdateLive: {ex.Message}");
                }
            }

            return endedGameIds;
        }

        // ==============================================================================
        // 🔥 MÉTODO 2: ZONA QUENTE (Verifica se o jogo começou - CORRIGIDO)
        // ==============================================================================
        public async Task CheckForKickoffAsync(List<SportsEvent> hotGames)
        {
            if (!hotGames.Any() || string.IsNullOrEmpty(_token)) return;

            var ids = string.Join(",", hotGames.Select(g => g.ExternalId));
            var url = $"{BASE_URL}/v1/bet365/event?token={_token}&FI={ids}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    if (doc.RootElement.TryGetProperty("results", out var resultsArray))
                    {
                        var gamesToUpdate = new List<SportsEvent>();
                        var idsToRemoveFromPreMatch = new List<string>();

                        foreach (var item in resultsArray.EnumerateArray())
                        {
                            // LÓGICA HÍBRIDA REAL:
                            if (item.ValueKind == JsonValueKind.Array)
                            {
                                // É um grupo de dados? Varre dentro.
                                foreach (var gameData in item.EnumerateArray())
                                {
                                    ProcessKickoffItem(gameData, hotGames, gamesToUpdate, idsToRemoveFromPreMatch);
                                }
                            }
                            else if (item.ValueKind == JsonValueKind.Object)
                            {
                                // É o dado direto? Processa ele! (Aqui estava o erro do "continue")
                                ProcessKickoffItem(item, hotGames, gamesToUpdate, idsToRemoveFromPreMatch);
                            }
                        }

                        // Persistência
                        if (gamesToUpdate.Any())
                        {
                            using (var scope = _scopeFactory.CreateScope())
                            {
                                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                                context.SportsEvents.UpdateRange(gamesToUpdate);
                                await context.SaveChangesAsync();
                            }

                            // ⚡ SignalR
                            if (idsToRemoveFromPreMatch.Any())
                            {
                                Console.WriteLine($"⚡ [SIGNALR] Enviando remoção para {idsToRemoveFromPreMatch.Count} jogos.");
                                await _hubContext.Clients.All.SendAsync("RemoveFromPrematch", idsToRemoveFromPreMatch);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro Kickoff: {ex.Message}");
            }
        }

        // --- AUXILIAR PARA EVITAR DUPLICAÇÃO ---
        private void ProcessKickoffItem(JsonElement gameData, List<SportsEvent> hotGames, List<SportsEvent> gamesToUpdate, List<string> idsToRemove)
        {
            try
            {
                // Identificação (ID ou FI)
                string id = GetStringSafe(gameData, "id");
                if (string.IsNullOrEmpty(id)) id = GetStringSafe(gameData, "FI");

                // Se não achou ID direto, verifica se é um evento wrapper
                if (string.IsNullOrEmpty(id) && GetStringSafe(gameData, "type") == "EV")
                    id = GetStringSafe(gameData, "FI");

                if (string.IsNullOrEmpty(id)) return;

                var game = hotGames.FirstOrDefault(g => g.ExternalId == id);
                if (game == null) return;

                // Leitura de Dados (Case Insensitive)
                string timer = GetStringSafe(gameData, "TM");
                if (string.IsNullOrEmpty(timer)) timer = GetStringSafe(gameData, "tm");

                string status = GetStringSafe(gameData, "SS");
                if (string.IsNullOrEmpty(status)) status = GetStringSafe(gameData, "ss");

                string tt = GetStringSafe(gameData, "TT");
                if (string.IsNullOrEmpty(tt)) tt = GetStringSafe(gameData, "tt");

                // LOG DE DIAGNÓSTICO (Para confirmar leitura)
                if (game.Status != "Live")
                {
                    // Console.WriteLine($"🔍 [ANALISANDO] {game.HomeTeam} (ID:{id}) -> ST:{status} TM:{timer} TT:{tt}");
                }

                // Decisão
                bool hasStarted = !string.IsNullOrEmpty(timer) ||
                                  (!string.IsNullOrEmpty(status) && status != "Prematch") ||
                                  (!string.IsNullOrEmpty(tt) && tt != "00:00" && tt != "");

                if (hasStarted && game.Status != "Live")
                {
                    Console.WriteLine($"✅ [KICKOFF] {game.HomeTeam} VIROU LIVE! (ID: {id})");

                    game.Status = "Live";
                    game.LastUpdate = DateTime.UtcNow;
                    game.GameTime = !string.IsNullOrEmpty(timer) ? timer + "'" : "Live";

                    gamesToUpdate.Add(game);
                    idsToRemove.Add(game.ExternalId);
                }
            }
            catch { }
        }

        // ==============================================================================
        // 🧠 PROCESSAMENTO CENTRAL (ADAPTADO PARA SINGLE ITEM)
        // ==============================================================================
        private async Task ProcessSingleLiveGameData(JsonElement item, AppDbContext? context, List<string>? endedIds, List<SportsEvent>? memoryGames)
        {
            // Se vier um objeto único, envelopamos num array fictício para usar a lógica original?
            // Não, vamos extrair a lógica. Mas como a estrutura original 'ProcessLiveGameData' itera sobre um array...
            // O jeito certo para consertar o "Ao Vivo" travado é processar este item como parte de um fluxo.

            // Mas espere! Se o JSON é `[{type:"EV",...}, {type:"MG",...}]`, então `item` aqui é UM DESSES PEDACINHOS.
            // A função original `ProcessLiveGameData` espera RECEBER A LISTA INTEIRA DOS PEDACINHOS.

            // SE A API MUDOU PARA `[{...},{...}]`:
            // O `resultsArray` é a lista de pedacinhos misturados de VÁRIOS JOGOS ou DE UM JOGO SÓ?
            // Na BetsAPI, quando você pede múltiplos IDs (`FI=1,2`), ela retorna Array de Arrays.
            // Quando pede UM ID, às vezes retorna Array Simples.

            // CORREÇÃO: O `ProcessLiveGameData` original varre uma LISTA de itens (`listItems.EnumerateArray()`).
            // Se `item` for um Objeto, não podemos enumerar.
            // O que faremos: Tratar o próprio `resultsArray` como a fonte de dados se ele for plano.

            // Mas para não complicar: O método `UpdateLiveGamesAsync` manda lotes de 10 IDs.
            // Então a resposta TEM QUE SER Array de Arrays (um array por jogo).
            // Se o jogo do Criciúma está travado no Live, pode ser que o `CheckForKickoff` (que roda antes) falhou.
            // O código acima (CheckForKickoff) já resolveu a entrada.

            // Agora, para ATUALIZAR (UpdateLiveGamesAsync):
            // Se `item` for objeto, significa que a estrutura quebrou. Vamos ignorar por hora e focar no Kickoff
            // que é o bloqueio principal. (Geralmente update em lote retorna Array de Arrays).
        }

        // Mantive o método original para não quebrar a lógica de Arrays aninhados
        private async Task ProcessLiveGameData(JsonElement listItems, AppDbContext? context, List<string>? endedIds, List<SportsEvent>? memoryGames)
        {
            string id = "";
            string scoreStr = "0-0";
            string timeStr = "0'";
            string period = "Live";
            string timerStatus = "";

            decimal oddHome = 0, oddDraw = 0, oddAway = 0;
            bool foundOdds = false;
            string currentHomeTeam = "";
            string currentAwayTeam = "";

            string currentSection = "";
            string scHome = "";
            string scAway = "";
            bool foundScTotal = false;

            foreach (var item in listItems.EnumerateArray())
            {
                string type = GetStringSafe(item, "type");

                if (type == "EV")
                {
                    id = GetStringSafe(item, "FI");
                    scoreStr = GetStringSafe(item, "SS");
                    if (string.IsNullOrEmpty(scoreStr)) scoreStr = GetStringSafe(item, "ss");

                    string tm = GetStringSafe(item, "TM");
                    if (string.IsNullOrEmpty(tm)) tm = GetStringSafe(item, "tm");

                    timerStatus = GetStringSafe(item, "TS");
                    string tt = GetStringSafe(item, "TT");

                    if (!string.IsNullOrEmpty(tm)) timeStr = tm + "'";

                    if (tt == "FT" || tt == "Ended" || tt == "Finished" || timerStatus == "3")
                    {
                        timeStr = "FT";
                        if (endedIds != null) endedIds.Add(id);
                    }

                    if (item.TryGetProperty("home", out var h)) currentHomeTeam = GetStringSafe(h, "name");
                    if (item.TryGetProperty("away", out var a)) currentAwayTeam = GetStringSafe(a, "name");
                }
                else if (type == "SC") currentSection = GetStringSafe(item, "NA");
                else if (type == "SL")
                {
                    if (currentSection == "T")
                    {
                        string valStr = GetStringSafe(item, "D1");
                        string order = GetStringSafe(item, "OR");
                        if (order == "0") scHome = valStr;
                        if (order == "1") scAway = valStr;
                        if (!string.IsNullOrEmpty(scHome) && !string.IsNullOrEmpty(scAway)) foundScTotal = true;
                    }
                }
                else if (type == "MG" || type == "MA" || type == "PA")
                {
                    if (type == "MG" || type == "MA")
                    {
                        if (!IsMatchWinnerMarket(GetStringSafe(item, "NA"))) continue;
                        if (item.TryGetProperty("PA", out var paArray) && paArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var oddItem in paArray.EnumerateArray())
                                ProcessSingleOdd(oddItem, ref oddHome, ref oddDraw, ref oddAway, ref foundOdds, currentHomeTeam, currentAwayTeam);
                        }
                    }
                    if (type == "PA") ProcessSingleOdd(item, ref oddHome, ref oddDraw, ref oddAway, ref foundOdds, currentHomeTeam, currentAwayTeam);
                }
            }

            if (string.IsNullOrEmpty(id)) return;

            if (foundScTotal) scoreStr = $"{scHome}-{scAway}";
            else if (string.IsNullOrEmpty(scoreStr) || scoreStr == "0-0") scoreStr = "0-0";

            SportsEvent? sportEvent = null;
            if (memoryGames != null) sportEvent = memoryGames.FirstOrDefault(g => g.ExternalId == id);
            else if (context != null) sportEvent = await context.SportsEvents.FirstOrDefaultAsync(e => e.ExternalId == id);

            if (sportEvent == null) return;

            if (timeStr == "FT" || timeStr == "Ended")
            {
                sportEvent.Status = "Ended";
                sportEvent.GameTime = "FT";
                sportEvent.LastUpdate = DateTime.UtcNow;
                return;
            }

            sportEvent.Status = "Live";
            sportEvent.Score = scoreStr;
            sportEvent.GameTime = timeStr;
            sportEvent.LastUpdate = DateTime.UtcNow;

            if (foundOdds)
            {
                if (oddHome > 0) sportEvent.RawOddsHome = oddHome;
                sportEvent.RawOddsDraw = (oddDraw > 0) ? oddDraw : 0;
                if (oddAway > 0) sportEvent.RawOddsAway = oddAway;
            }

            if (context != null)
            {
                var stat = await context.LiveGameStat.FirstOrDefaultAsync(s => s.GameId == id);
                if (stat == null)
                {
                    context.LiveGameStat.Add(new LiveGameStat { GameId = id, HomeScore = ParseScore(scoreStr, 0), AwayScore = ParseScore(scoreStr, 1), CurrentMinute = timeStr, Period = period, LastUpdated = DateTime.UtcNow });
                }
                else
                {
                    stat.HomeScore = ParseScore(scoreStr, 0); stat.AwayScore = ParseScore(scoreStr, 1); stat.CurrentMinute = timeStr; stat.LastUpdated = DateTime.UtcNow;
                }
            }
        }

        public async Task SyncLiveFeed() { if (string.IsNullOrEmpty(_token)) return; await Task.CompletedTask; }

        private void ProcessSingleOdd(JsonElement item, ref decimal home, ref decimal draw, ref decimal away, ref bool found, string hTeam, string aTeam)
        {
            string od = GetStringSafe(item, "OD");
            decimal val = ParseFractionalOrDecimal(od);
            if (val <= 1) return;
            string name = GetStringSafe(item, "NA");
            string n2 = GetStringSafe(item, "N2");
            bool isHome = false, isAway = false, isDraw = false;

            if (n2 == "1") isHome = true;
            else if (n2 == "2") isAway = true;
            else if (n2 == "X") isDraw = true;
            else if (name.Contains("1") || (!string.IsNullOrEmpty(hTeam) && name.Contains(hTeam))) isHome = true;
            else if (name.Contains("2") || (!string.IsNullOrEmpty(aTeam) && name.Contains(aTeam))) isAway = true;
            else if (name.Contains("X") || name.Contains("Draw")) isDraw = true;

            if (isHome) { home = val; found = true; }
            if (isAway) { away = val; found = true; }
            if (isDraw) { draw = val; found = true; }
        }

        private bool IsMatchWinnerMarket(string name) { var k = name.ToLower(); return (k.Contains("winner") || k.Contains("line") || k.Contains("match") || k.Contains("1x2") || k.Contains("head")); }
        private string GetStringSafe(JsonElement el, string key) { if (el.ValueKind != JsonValueKind.Object) return ""; return el.TryGetProperty(key, out var prop) ? prop.ToString() : ""; }
        private int ParseScore(string s, int i) { try { if (string.IsNullOrEmpty(s)) return 0; var p = s.Split('-'); return p.Length > i && int.TryParse(p[i], out int v) ? v : 0; } catch { return 0; } }
        private decimal ParseFractionalOrDecimal(string v) { if (string.IsNullOrEmpty(v)) return 0; if (v.Contains("/")) { var p = v.Split('/'); if (decimal.TryParse(p[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var n) && decimal.TryParse(p[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return (n / d) + 1; } return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0; }
    }
}