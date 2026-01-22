using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Magic_casino_sportbook.Services
{
    public class LiveSportService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiToken;
        private const string BASE_URL = "https://api.b365api.com";
        private readonly JsonSerializerOptions _jsonOptions;

        // 🔒 O PORTEIRO GLOBAL (SEMAPHORE):
        // Isso obriga o sistema a fazer apenas 1 chamada por vez, 
        // não importa quantos Workers estejam rodando.
        private static readonly SemaphoreSlim _apiGate = new SemaphoreSlim(1, 1);

        public LiveSportService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _apiToken = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "";

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
        }

        // ==========================================================================================
        // 🚀 ATUALIZAR LIVE (Com Fila Global _apiGate)
        // ==========================================================================================
        public async Task<List<string>> UpdateLiveGamesAsync(List<SportsEvent> liveGames, AppDbContext context)
        {
            var endedGameIds = new List<string>();
            if (!liveGames.Any()) return endedGameIds;

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var batches = liveGames.Chunk(10).ToList();

            foreach (var batch in batches)
            {
                // Heartbeat
                foreach (var game in batch)
                {
                    game.LastUpdate = DateTime.UtcNow;
                    context.Entry(game).Property(x => x.LastUpdate).IsModified = true;
                }

                var gameIds = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
                var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={gameIds}";

                // 🔒 ENTRA NA FILA (Aguarda a vez)
                await _apiGate.WaitAsync();
                try
                {
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();

                        if (jsonString.Contains("PARAM_INVALID"))
                        {
                            Console.WriteLine($"⚠️ [RESGATE] Lote contaminado. Verificando individualmente...");
                            // Passa para o método de resgate (que tem seus próprios delays)
                            await ProcessIndividualFallbackAsync(client, batch, context, endedGameIds);
                        }
                        else if (!string.IsNullOrWhiteSpace(jsonString))
                        {
                            var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
                            if (data != null && data.Results != null && data.Success == 1)
                            {
                                DispatchUpdates(data.Results, batch.ToList(), context, endedGameIds);
                            }
                        }
                    }
                    else if ((int)response.StatusCode == 429)
                    {
                        Console.WriteLine($"⛔ [LIMIT] 429 Detectado. Pausando 2s...");
                        await Task.Delay(2000);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro Update: {ex.Message}");
                }
                finally
                {
                    // 🔓 LIBERA A FILA
                    _apiGate.Release();
                }

                // Delay OBRIGATÓRIO fora do lock para dar respiro à API
                await Task.Delay(1100);
            }
            return endedGameIds;
        }

        // ==========================================================================================
        // 🚑 MÉTODO DE RESGATE (1 por 1 - Lento para segurança)
        // ==========================================================================================
        private async Task ProcessIndividualFallbackAsync(HttpClient client, SportsEvent[] batch, AppDbContext context, List<string> endedGameIds)
        {
            foreach (var game in batch)
            {
                var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={game.ExternalId}";
                try
                {
                    var response = await client.GetAsync(url);
                    var jsonString = await response.Content.ReadAsStringAsync();

                    if (jsonString.Contains("PARAM_INVALID"))
                    {
                        Console.WriteLine($"🗑️ [LIMPEZA] Jogo inválido removido: {game.ExternalId}");
                        game.Status = "Ended";
                        context.Entry(game).State = EntityState.Modified;
                        endedGameIds.Add(game.ExternalId);
                    }
                    else
                    {
                        var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
                        if (data != null && data.Results != null)
                        {
                            DispatchUpdates(data.Results, new List<SportsEvent> { game }, context, endedGameIds);
                        }
                    }
                }
                catch { }

                // Delay generoso (1.5s) no resgate para evitar travar tudo com 429
                await Task.Delay(1500);
            }
        }

        // ==========================================================================================
        // 🕵️ HOTZONE (Com Fila Global _apiGate)
        // ==========================================================================================
        public async Task<List<string>> VerifyKickoffWithApiAsync(List<SportsEvent> candidates, IServiceProvider sp)
        {
            var newLiveIds = new List<string>();
            if (!candidates.Any()) return newLiveIds;

            using var scope = sp.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.AttachRange(candidates);

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var batches = candidates.Chunk(10).ToList();

            foreach (var batch in batches)
            {
                var gameIds = string.Join(",", batch.Select(g => g.ExternalId.Trim()));
                var url = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={gameIds}";

                // 🔒 ENTRA NA FILA
                await _apiGate.WaitAsync();
                try
                {
                    var response = await client.GetAsync(url);
                    var jsonString = await response.Content.ReadAsStringAsync();

                    if (jsonString.Contains("PARAM_INVALID"))
                    {
                        Console.WriteLine($"⚠️ [HOTZONE] Lote com ID ruim. Verificando individualmente...");

                        // Fallback interno do HotZone
                        foreach (var g in batch)
                        {
                            var indUrl = $"{BASE_URL}/v1/bet365/event?token={_apiToken}&FI={g.ExternalId}";
                            var indResp = await client.GetAsync(indUrl);
                            var indJson = await indResp.Content.ReadAsStringAsync();

                            if (!indJson.Contains("PARAM_INVALID"))
                            {
                                ProcessHotZonePacket(indJson, new List<SportsEvent> { g }, context, newLiveIds);
                            }
                            else
                            {
                                g.Status = "Cancelled";
                                context.Entry(g).State = EntityState.Modified;
                            }
                            // Delay interno para segurança
                            await Task.Delay(1500);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(jsonString))
                    {
                        ProcessHotZonePacket(jsonString, batch.ToList(), context, newLiveIds);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ HotZone Check Erro: {ex.Message}");
                }
                finally
                {
                    // 🔓 LIBERA A FILA
                    _apiGate.Release();
                }

                await context.SaveChangesAsync();
                // Delay OBRIGATÓRIO
                await Task.Delay(1100);
            }

            return newLiveIds;
        }

        private void ProcessHotZonePacket(string jsonString, List<SportsEvent> games, AppDbContext context, List<string> newLiveIds)
        {
            var data = JsonSerializer.Deserialize<B365LiveResponse>(jsonString, _jsonOptions);
            if (data != null && data.Results != null)
            {
                foreach (var packetList in data.Results)
                {
                    var ev = packetList.FirstOrDefault(p => p.Type == "EV");
                    if (ev == null) continue;

                    var gameDb = games.FirstOrDefault(g => g.ExternalId == ev.Fi);
                    if (gameDb == null) continue;

                    // --- 🛡️ BLINDAGEM 106' (Anti-Travamento no Início) ---
                    bool tempoExagerado = false;
                    if (int.TryParse(ev.Tm, out int minutos))
                    {
                        if (minutos > 100 && ev.Tt == "0") tempoExagerado = true;
                    }

                    bool isLive = (ev.TimeStatus == "1") || (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0");

                    // Só deixa virar Live se NÃO estiver travado com tempo absurdo
                    if (isLive && !tempoExagerado)
                    {
                        gameDb.Status = "Live";
                        if (string.IsNullOrEmpty(gameDb.Score)) gameDb.Score = "0-0";
                        if (string.IsNullOrEmpty(gameDb.GameTime)) gameDb.GameTime = "0'";
                        gameDb.LastUpdate = DateTime.UtcNow.AddMinutes(-10);
                        context.Entry(gameDb).State = EntityState.Modified;
                        newLiveIds.Add(gameDb.ExternalId);
                    }
                }
            }
        }

        // ==========================================================================================
        // 🚦 DISPATCHER (Com Blindagem de Fim de Jogo)
        // ==========================================================================================
        private void DispatchUpdates(List<List<B365Packet>> results, List<SportsEvent> batchGames, AppDbContext context, List<string> endedGameIds)
        {
            foreach (var packets in results)
            {
                if (packets == null || !packets.Any()) continue;

                string targetId = packets.FirstOrDefault(p => p.Type == "EV")?.Fi
                               ?? packets.FirstOrDefault(p => !string.IsNullOrEmpty(p.Fi))?.Fi;

                if (string.IsNullOrEmpty(targetId)) continue;

                var gameDb = batchGames.FirstOrDefault(g => g.ExternalId.Trim() == targetId.Trim());
                if (gameDb == null) continue;

                var ev = packets.FirstOrDefault(p => p.Type == "EV");
                var logs = packets.Where(p => p.Type == "ST").ToList();

                if (ev != null)
                {
                    bool mudou = false;
                    string sportKey = (gameDb.SportKey ?? "").ToLower();

                    // --- 🛡️ DETECÇÃO DE JOGO TRAVADO (BLINDAGEM 106') ---
                    bool tempoExagerado = false;
                    if (int.TryParse(ev.Tm, out int minutos))
                    {
                        // Se passou de 100 min e o relógio (TT) parou (0), consideramos ENCERRADO
                        if (minutos > 100 && ev.Tt == "0") tempoExagerado = true;
                    }

                    // Encerra se a API mandar OU se nossa blindagem detectar
                    if (ev.TimeStatus == "3" || ev.Status == "3" || tempoExagerado)
                    {
                        bool temDados = (!string.IsNullOrEmpty(gameDb.Score) && gameDb.Score != "0-0") ||
                                        (!string.IsNullOrEmpty(gameDb.GameTime) && gameDb.GameTime != "0'");

                        if (gameDb.Status != "Ended" && temDados)
                        {
                            Console.WriteLine($"🏁 [FIM DETECTADO] {gameDb.HomeTeam} -> Ended. (Motivo: {(tempoExagerado ? "Tempo > 100" : "API")})");
                            gameDb.Status = "Ended";
                            gameDb.GameTime = "FT";
                            if (!string.IsNullOrEmpty(ev.Ss)) gameDb.Score = ev.Ss;
                            endedGameIds.Add(gameDb.ExternalId);
                            mudou = true;
                        }
                    }
                    else
                    {
                        if (sportKey.Contains("soccer") || sportKey.Contains("futebol")) mudou = UpdateSoccer(gameDb, ev, logs);
                        else if (sportKey.Contains("basket")) mudou = UpdateBasketball(gameDb, ev);
                        else if (sportKey.Contains("tennis")) mudou = UpdateTennis(gameDb, ev);
                        else if (sportKey.Contains("volley")) mudou = UpdateVolleyball(gameDb, ev);
                        else mudou = UpdateGenericSport(gameDb, ev);
                    }

                    if (mudou) context.Entry(gameDb).State = EntityState.Modified;
                }
                ProcessOdds(gameDb, packets, ref context);
            }
        }

        // ==========================================================================================
        // MÉTODOS DE PARSE
        // ==========================================================================================

        private bool UpdateSoccer(SportsEvent game, B365Packet ev, List<B365Packet> logs)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(ev.Ss) && ev.Ss.Contains("-"))
            {
                if (game.Score != ev.Ss) { Console.WriteLine($"⚽ {game.HomeTeam}: {ev.Ss}"); game.Score = ev.Ss; changed = true; }
            }
            else if (string.IsNullOrEmpty(game.Score)) { game.Score = "0-0"; changed = true; }

            string novoTempo = "0'";
            if (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0") novoTempo = ev.Tm + "'";
            else
            {
                foreach (var log in logs)
                {
                    if (!string.IsNullOrEmpty(log.La))
                    {
                        var match = Regex.Match(log.La, @"(\d+)'");
                        if (match.Success) { novoTempo = match.Groups[1].Value + "'"; break; }
                    }
                }
            }
            if (game.GameTime != novoTempo && novoTempo != "0'") { game.GameTime = novoTempo; changed = true; }
            return changed;
        }

        private bool UpdateBasketball(SportsEvent game, B365Packet ev)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(ev.Ss) && game.Score != ev.Ss) { game.Score = ev.Ss; changed = true; }
            if (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0") { string t = ev.Tm + "'"; if (game.GameTime != t) { game.GameTime = t; changed = true; } }
            return changed;
        }

        private bool UpdateTennis(SportsEvent game, B365Packet ev)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(ev.Ss) && game.Score != ev.Ss) { game.Score = ev.Ss; changed = true; }
            return changed;
        }

        private bool UpdateVolleyball(SportsEvent game, B365Packet ev)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(ev.Ss) && game.Score != ev.Ss) { game.Score = ev.Ss; changed = true; }
            return changed;
        }

        private bool UpdateGenericSport(SportsEvent game, B365Packet ev)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(ev.Ss) && game.Score != ev.Ss) { game.Score = ev.Ss; changed = true; }
            if (!string.IsNullOrEmpty(ev.Tm) && ev.Tm != "0") { string t = ev.Tm + "'"; if (game.GameTime != t) { game.GameTime = t; changed = true; } }
            return changed;
        }

        private void ProcessOdds(SportsEvent game, List<B365Packet> packets, ref AppDbContext context)
        {
            var odds = packets.Where(p => (p.Type == "PA" || p.Type == "MA") && !string.IsNullOrEmpty(p.Od));
            bool changed = false;
            foreach (var odd in odds)
            {
                decimal valor = ConvertFraction(odd.Od);
                if (valor <= 1.0m) continue;
                string n2 = (odd.N2 ?? "").Trim();
                string name = (odd.Na ?? "").ToLower().Trim();
                bool isHome = n2 == "1" || name == "1" || name.Contains("home") || name.Contains("casa");
                bool isAway = n2 == "2" || name == "2" || name.Contains("away") || name.Contains("fora");
                bool isDraw = n2 == "x" || name == "x" || name.Contains("draw") || name.Contains("empate");

                if (isHome && game.RawOddsHome != valor) { game.RawOddsHome = valor; changed = true; }
                else if (isAway && game.RawOddsAway != valor) { game.RawOddsAway = valor; changed = true; }
                else if (isDraw && game.RawOddsDraw != valor) { game.RawOddsDraw = valor; changed = true; }
            }
            if (changed) context.Entry(game).State = EntityState.Modified;
        }

        public async Task<List<string>> CheckForKickoffAsync(List<SportsEvent> games, AppDbContext context)
        {
            return await Task.FromResult(new List<string>());
        }

        private decimal ConvertFraction(string fraction)
        {
            if (string.IsNullOrEmpty(fraction) || fraction == "0/0") return 0m;
            try
            {
                if (fraction.Contains("/"))
                {
                    var parts = fraction.Split('/');
                    if (parts.Length == 2) return (decimal.Parse(parts[0]) / decimal.Parse(parts[1])) + 1;
                }
                return decimal.Parse(fraction, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return 0m; }
        }
    }
}