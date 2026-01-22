using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.DTOs;
using Magic_casino_sportbook.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Magic_casino_sportbook.Controllers
{
    [ApiController]
    [Route("api/LiveEvents")]
    public class LiveEventsController : ControllerBase
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly AppDbContext _context; // ✅ Voltamos com o Contexto para o Fallback
        private readonly JsonSerializerOptions _jsonOptions;

        public LiveEventsController(IConnectionMultiplexer redis, AppDbContext context)
        {
            _redis = redis;
            _context = context;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
        }

        [HttpGet]
        public async Task<IActionResult> GetLiveEvents()
        {
            try
            {
                var db = _redis.GetDatabase();
                var server = _redis.GetServer(_redis.GetEndPoints().First());

                // 1. TENTA BUSCAR NO REDIS (CAMINHO RÁPIDO ⚡)
                var keys = server.Keys(pattern: "live_game:*").ToArray();

                // 2. SE O REDIS ESTIVER VAZIO (COLD START), BUSCA NO SQL (CAMINHO SEGURO 🛡️)
                if (!keys.Any())
                {
                    // Console.WriteLine("⚠️ Redis vazio. Usando Fallback do SQL.");
                    return await GetFromSqlFallback();
                }

                // --- FLUXO REDIS (NORMAL) ---
                var redisValues = await db.StringGetAsync(keys);
                var liveGamesDto = new List<LiveGameDto>();

                foreach (var json in redisValues)
                {
                    if (!json.HasValue) continue;

                    var e = JsonSerializer.Deserialize<SportsEvent>(json!, _jsonOptions);
                    if (e != null)
                    {
                        liveGamesDto.Add(MapToDto(e));
                    }
                }

                return Ok(liveGamesDto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro Redis: {ex.Message}. Tentando SQL...");
                // Se o Redis cair, tenta buscar no SQL para não derrubar o site
                return await GetFromSqlFallback();
            }
        }

        // ==================================================================================
        // 🛡️ MÉTODO DE RESGATE (SQL)
        // ==================================================================================
        private async Task<IActionResult> GetFromSqlFallback()
        {
            var agora = DateTime.UtcNow;

            // Busca apenas o necessário no banco
            var sqlGames = await _context.SportsEvents
                .AsNoTracking()
                .Where(e => e.Status == "Live")
                .Where(e => e.CommenceTime > agora.AddHours(-48))
                .ToListAsync();

            var dtos = sqlGames.Select(e => MapToDto(e)).ToList();

            return Ok(dtos);
        }

        // ==================================================================================
        // 🔄 MAPPER CENTRALIZADO (Para garantir consistência entre Redis e SQL)
        // ==================================================================================
        private LiveGameDto MapToDto(SportsEvent e)
        {
            // Parse do Placar
            int hScore = 0;
            int aScore = 0;
            if (!string.IsNullOrEmpty(e.Score) && e.Score.Contains("-"))
            {
                var parts = e.Score.Split('-');
                if (parts.Length == 2)
                {
                    int.TryParse(parts[0], out hScore);
                    int.TryParse(parts[1], out aScore);
                }
            }

            return new LiveGameDto
            {
                GameId = e.ExternalId,
                SportKey = e.SportKey,
                HomeTeam = e.HomeTeam,
                AwayTeam = e.AwayTeam,
                League = e.League,

                HomeTeamLogo = !string.IsNullOrEmpty(e.HomeTeamId) ? $"https://assets.b365api.com/images/team/m/{e.HomeTeamId}.png" : null,
                AwayTeamLogo = !string.IsNullOrEmpty(e.AwayTeamId) ? $"https://assets.b365api.com/images/team/m/{e.AwayTeamId}.png" : null,

                CountryCode = e.CountryCode,
                FlagUrl = !string.IsNullOrEmpty(e.CountryCode) ? $"https://assets.b365api.com/images/flags/{e.CountryCode}.svg" : null,

                CommenceTime = e.CommenceTime,

                HomeScore = hScore,
                AwayScore = aScore,
                CurrentMinute = e.GameTime ?? "0'",
                Period = "Live",

                RawOddsHome = e.RawOddsHome,
                RawOddsDraw = e.RawOddsDraw,
                RawOddsAway = e.RawOddsAway
            };
        }
    }
}