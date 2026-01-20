using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
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
        public async Task<List<string>> UpdateLiveGamesAsync(List<SportsEvent> liveGames, AppDbContext context)
        {
            var endedGameIds = new List<string>();
            if (!liveGames.Any() || string.IsNullOrEmpty(_token)) return endedGameIds;

            var chunks = liveGames.Chunk(10).ToList();

            foreach (var chunk in chunks)
            {
                var chunkIds = chunk.Select(g => g.ExternalId).ToList();
                var idsStr = string.Join(",", chunkIds);
                var url = $"{BASE_URL}/v1/bet365/event?token={_token}&FI={idsStr}";

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
                                await ProcessGenericPacket(item, context, endedGameIds, liveGames);
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

        private async Task ProcessGenericPacket(JsonElement packet, AppDbContext context, List<string> endedIds, List<SportsEvent> memoryGames)
        {
            if (packet.ValueKind == JsonValueKind.Array)
                await ProcessGameDataList(packet.EnumerateArray(), context, endedIds, memoryGames);
            else if (packet.ValueKind == JsonValueKind.Object)
            {
                var singleList = new List<JsonElement> { packet };
                await ProcessGameDataList(singleList, context, endedIds, memoryGames);
            }
        }

        private async Task ProcessGameDataList(IEnumerable<JsonElement> items, AppDbContext context, List<string> endedIds, List<SportsEvent> memoryGames)
        {
            string id = "";
            string scoreStr = null;
            string timeStr = null;
            string period = "Live";
            string timerStatus = "";
            bool isFinished = false;

            decimal oddHome = 0, oddDraw = 0, oddAway = 0;
            bool foundOdds = false;

            string scHome = "";
            string scAway = "";
            bool foundScTotal = false;
            string currentSection = "";

            string homeTeamName = "";
            string awayTeamName = "";

            foreach (var item in items)
            {
                string type = GetStringSafe(item, "type");

                if (type == "EV")
                {
                    // Prioriza FI (Fixture ID limpo)
                    id = GetStringSafe(item, "FI");
                    if (string.IsNullOrEmpty(id)) id = GetStringSafe(item, "id");

                    var ss = GetStringSafe(item, "SS");
                    if (!string.IsNullOrEmpty(ss)) scoreStr = ss;
                    if (string.IsNullOrEmpty(scoreStr)) scoreStr = GetStringSafe(item, "ss");

                    string tm = GetStringSafe(item, "TM");
                    if (string.IsNullOrEmpty(tm)) tm = GetStringSafe(item, "tm");

                    timerStatus = GetStringSafe(item, "TS");
                    string tt = GetStringSafe(item, "TT");

                    if (!string.IsNullOrEmpty(tm)) timeStr = tm + "'";

                    if (tt == "FT" || tt == "Ended" || tt == "Finished" || timerStatus == "3")
                    {
                        timeStr = "FT";
                        isFinished = true;
                    }

                    if (item.TryGetProperty("home", out var h)) homeTeamName = GetStringSafe(h, "name");
                    if (item.TryGetProperty("away", out var a)) awayTeamName = GetStringSafe(a, "name");
                }
                else if (type == "SC") currentSection = GetStringSafe(item, "NA");
                else if (type == "SL")
                {
                    if (currentSection == "T" || currentSection == "SCORE")
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
                        string na = GetStringSafe(item, "NA");
                        if (!IsMatchWinnerMarket(na)) continue;
                        if (item.TryGetProperty("PA", out var paArray) && paArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var oddItem in paArray.EnumerateArray())
                                ProcessSingleOdd(oddItem, ref oddHome, ref oddDraw, ref oddAway, ref foundOdds, homeTeamName, awayTeamName);
                        }
                    }
                    if (type == "PA") ProcessSingleOdd(item, ref oddHome, ref oddDraw, ref oddAway, ref foundOdds, homeTeamName, awayTeamName);
                }
            }

            if (string.IsNullOrEmpty(id)) return;

            if (foundScTotal && (string.IsNullOrEmpty(scoreStr) || scoreStr == "0-0")) scoreStr = $"{scHome}-{scAway}";
            if (string.IsNullOrEmpty(scoreStr)) scoreStr = "0-0";

            var sportEvent = memoryGames.FirstOrDefault(g => g.ExternalId == id);
            if (sportEvent == null) return;

            if (isFinished)
            {
                if (!endedIds.Contains(id)) endedIds.Add(id);
                sportEvent.GameTime = "FT";
            }
            else
            {
                if (!string.IsNullOrEmpty(timeStr)) sportEvent.GameTime = timeStr;
            }

            sportEvent.Status = "Live";
            sportEvent.Score = scoreStr;
            sportEvent.LastUpdate = DateTime.UtcNow;

            if (foundOdds)
            {
                if (oddHome > 1) sportEvent.RawOddsHome = oddHome;
                if (oddAway > 1) sportEvent.RawOddsAway = oddAway;
                if (oddDraw > 1) sportEvent.RawOddsDraw = oddDraw;
            }

            var stat = await context.LiveGameStat.FirstOrDefaultAsync(s => s.GameId == id);
            int scoreH = ParseScore(scoreStr, 0);
            int scoreA = ParseScore(scoreStr, 1);

            if (stat == null)
            {
                context.LiveGameStat.Add(new LiveGameStat { GameId = id, HomeScore = scoreH, AwayScore = scoreA, CurrentMinute = timeStr ?? "Live", Period = period, LastUpdated = DateTime.UtcNow });
            }
            else
            {
                stat.HomeScore = scoreH; stat.AwayScore = scoreA; if (timeStr != null) stat.CurrentMinute = timeStr; stat.LastUpdated = DateTime.UtcNow;
            }
        }

        // ==============================================================================
        // 🔥 ZONA 2: ZONA QUENTE (CheckForKickoff) - TOTALMENTE CORRIGIDO
        // ==============================================================================
        // 🔴 Aceita AppDbContext para não quebrar a transação
        public async Task CheckForKickoffAsync(List<SportsEvent> hotGames, AppDbContext context)
        {
            if (!hotGames.Any() || string.IsNullOrEmpty(_token)) return;

            var chunks = hotGames.Chunk(20).ToList();

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
                            var gamesToUpdate = new List<SportsEvent>();
                            var idsToRemoveFromPreMatch = new List<string>();

                            foreach (var item in resultsArray.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var gameData in item.EnumerateArray())
                                        ProcessKickoffItem(gameData, hotGames, gamesToUpdate, idsToRemoveFromPreMatch);
                                }
                                else if (item.ValueKind == JsonValueKind.Object)
                                {
                                    ProcessKickoffItem(item, hotGames, gamesToUpdate, idsToRemoveFromPreMatch);
                                }
                            }

                            if (gamesToUpdate.Any())
                            {
                                // 🛑 AQUI ESTAVA O ERRO: Usamos o context passado, NÃO criamos um novo scope.
                                context.SportsEvents.UpdateRange(gamesToUpdate);
                                await context.SaveChangesAsync();

                                if (idsToRemoveFromPreMatch.Any())
                                {
                                    await _hubContext.Clients.All.SendAsync("RemoveFromPrematch", idsToRemoveFromPreMatch);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro Kickoff Chunk: {ex.Message}");
                }
                await Task.Delay(100);
            }
        }

        private void ProcessKickoffItem(JsonElement gameData, List<SportsEvent> hotGames, List<SportsEvent> gamesToUpdate, List<string> idsToRemove)
        {
            try
            {
                // 🔴 PRIORIDADE DE ID CORRIGIDA
                string id = GetStringSafe(gameData, "FI");
                if (string.IsNullOrEmpty(id)) id = GetStringSafe(gameData, "id");

                if (string.IsNullOrEmpty(id) && GetStringSafe(gameData, "type") == "EV")
                    id = GetStringSafe(gameData, "FI");

                if (string.IsNullOrEmpty(id)) return;

                var game = hotGames.FirstOrDefault(g => g.ExternalId == id);
                if (game == null) return;

                // Leitura dos Sinais
                string timer = GetStringSafe(gameData, "TM");
                if (string.IsNullOrEmpty(timer)) timer = GetStringSafe(gameData, "tm");

                string statusText = GetStringSafe(gameData, "SS");
                if (string.IsNullOrEmpty(statusText)) statusText = GetStringSafe(gameData, "ss");

                string ts = GetStringSafe(gameData, "TS");
                if (string.IsNullOrEmpty(ts)) ts = GetStringSafe(gameData, "time_status");

                string tt = GetStringSafe(gameData, "TT");

                // Regra de Kickoff
                bool hasStarted =
                    (!string.IsNullOrEmpty(timer) && timer != "0") ||
                    (!string.IsNullOrEmpty(statusText) && statusText != "Prematch" && statusText != "NS") ||
                    (ts == "1") ||
                    (!string.IsNullOrEmpty(tt) && tt != "00:00" && tt != "");

                // Log para debug
                Console.WriteLine($"🔍 CHECK {id}: TS={ts}, TM={timer}, SS={statusText} -> Started? {hasStarted}");

                if (hasStarted && game.Status != "Live")
                {
                    Console.WriteLine($"✅ [KICKOFF] {game.HomeTeam} VIROU LIVE! (ID: {id}) | TS: {ts}");

                    game.Status = "Live";
                    game.LastUpdate = DateTime.UtcNow;

                    if (!string.IsNullOrEmpty(timer) && timer != "0") game.GameTime = timer + "'";
                    else if (ts == "1") game.GameTime = "Live";

                    gamesToUpdate.Add(game);
                    idsToRemove.Add(game.ExternalId);
                }
            }
            catch { }
        }

        private void ProcessSingleOdd(JsonElement item, ref decimal home, ref decimal draw, ref decimal away, ref bool found, string hTeam, string aTeam)
        {
            string od = GetStringSafe(item, "OD");
            decimal val = ParseFractionalOrDecimal(od);
            if (val <= 1.0m) return;

            string name = GetStringSafe(item, "NA");
            string n2 = GetStringSafe(item, "N2");

            bool isHome = false, isAway = false, isDraw = false;

            if (n2 == "1") isHome = true;
            else if (n2 == "2") isAway = true;
            else if (n2 == "X") isDraw = true;
            else if (name == "1" || name.Contains("Home")) isHome = true;
            else if (name == "2" || name.Contains("Away")) isAway = true;
            else if (name == "X" || name.Contains("Draw")) isDraw = true;
            else if (!string.IsNullOrEmpty(hTeam) && name.Contains(hTeam)) isHome = true;
            else if (!string.IsNullOrEmpty(aTeam) && name.Contains(aTeam)) isAway = true;

            if (isHome) { home = val; found = true; }
            if (isAway) { away = val; found = true; }
            if (isDraw) { draw = val; found = true; }
        }

        private bool IsMatchWinnerMarket(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var k = name.ToLower();
            return (k.Contains("winner") || k.Contains("line") || k.Contains("match") || k.Contains("1x2") || k.Contains("full time") || k.Contains("resultado"));
        }

        private string GetStringSafe(JsonElement el, string key) { if (el.ValueKind != JsonValueKind.Object) return ""; return el.TryGetProperty(key, out var prop) ? prop.ToString() : ""; }

        private int ParseScore(string s, int index)
        {
            try { if (string.IsNullOrEmpty(s)) return 0; var p = s.Split('-'); return p.Length > index && int.TryParse(p[index], out int v) ? v : 0; } catch { return 0; }
        }

        private decimal ParseFractionalOrDecimal(string v)
        {
            if (string.IsNullOrEmpty(v)) return 0;
            if (v.Contains("/")) { var p = v.Split('/'); if (decimal.TryParse(p[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var n) && decimal.TryParse(p[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return (n / d) + 1; }
            return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : 0;
        }

        public async Task SyncLiveFeed() { await Task.CompletedTask; }
    }
}