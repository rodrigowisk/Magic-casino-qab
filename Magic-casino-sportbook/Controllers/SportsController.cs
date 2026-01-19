using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.DTOs;
using Magic_casino_sportbook.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Magic_casino_sportbook.Controllers
{
    [ApiController]
    [Route("api/sports")]
    public class SportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;

        public SportsController(AppDbContext context, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _scopeFactory = scopeFactory;
        }

        // ... (Os métodos ForceUpdate, GetRawJson, GetEventById continuam iguais) ...
        [HttpPost("force-update")]
        public IActionResult ForceUpdate()
        {
            _ = Task.Run(async () => {
                try { using (var scope = _scopeFactory.CreateScope()) { await scope.ServiceProvider.GetRequiredService<PreMatchService>().SyncUpcomingGames(); } }
                catch (Exception ex) { Console.WriteLine($"🔥 Erro: {ex.Message}"); }
            });
            return Ok(new { message = "Atualização iniciada." });
        }

        [HttpGet("debug-json/{eventId}")]
        public async Task<IActionResult> GetRawJson(string eventId)
        {
            var token = Environment.GetEnvironmentVariable("BETSAPI_TOKEN") ?? "137460-kSCpac5sbNtyVZ";
            var url = $"https://api.betsapi.com/v1/bet365/prematch?token={token}&FI={eventId}";
            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            return Content(await response.Content.ReadAsStringAsync(), "application/json");
        }

        [HttpGet("event/{id}")]
        public async Task<IActionResult> GetEventById(string id)
        {
            var sportsEvent = await _context.SportsEvents.Include(e => e.Odds).AsNoTracking().FirstOrDefaultAsync(e => e.ExternalId == id);
            if (sportsEvent == null) return NotFound(new { error = "Evento não encontrado." });
            return Ok(sportsEvent);
        }
        // ... (Fim dos métodos auxiliares) ...


        // =================================================================================
        // 🚀 MÉTODOS PÚBLICOS (MODO ESTRITO: SÓ FUTURO)
        // =================================================================================

        [HttpGet("active-sports")]
        public async Task<IActionResult> GetActiveSports()
        {
            try
            {
                var agora = DateTime.UtcNow;
                var limiteFuturo = agora.AddDays(4);

                var stats = await _context.SportsEvents
                    .AsNoTracking()
                    // 🔒 FILTRO: Conta apenas jogos PREMATCH que AINDA NÃO ACONTECERAM
                    .Where(e =>
                        e.Status == "Prematch" &&
                        e.CommenceTime > agora // Tolerância ZERO
                    )
                    .Where(e => e.CommenceTime <= limiteFuturo)
                    .GroupBy(e => e.SportKey)
                    .Select(g => new { Key = g.Key, Count = g.Count() })
                    .ToListAsync();

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao carregar menu." });
            }
        }

        [HttpGet("events")]
        public async Task<ActionResult<IEnumerable<SportsEventDto>>> GetEvents(
            [FromQuery] string? sport = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var agora = DateTime.UtcNow;

                var query = _context.SportsEvents
                    .Include(e => e.Odds)
                    .AsNoTracking()
                    // 🛑 MUDANÇA CRÍTICA AQUI 🛑
                    // Removemos "OR e.Status == Live".
                    // Agora, só retorna se for PREMATCH e estiver NO FUTURO.
                    // Se virou Live? Some daqui.
                    // Se passou do horário e não virou Live? Some daqui (Limbo).
                    .Where(e =>
                        e.Status == "Prematch" &&
                        e.CommenceTime > agora // Exatamente agora. 1 segundo depois já some.
                    )
                    .Where(e => e.CommenceTime <= agora.AddDays(4))
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(sport))
                {
                    string sportTerm = sport.Trim().ToLower();
                    query = query.Where(e => e.SportKey.ToLower() == sportTerm);
                }

                // Ordenação: Apenas por horário, já que não tem mais Live misturado
                var rawEvents = await query
                    .OrderBy(e => e.CommenceTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var dtoList = new List<SportsEventDto>();

                foreach (var e in rawEvents)
                {
                    // Placar
                    int? hScore = null, aScore = null;
                    if (!string.IsNullOrEmpty(e.Score) && e.Score.Contains("-"))
                    {
                        var parts = e.Score.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int a))
                        {
                            hScore = h; aScore = a;
                        }
                    }

                    // Odds
                    decimal oddHome = 0, oddDraw = 0, oddAway = 0;
                    var mainMarkets = e.Odds.Where(o =>
                        o.MarketName == "Resultado Final" ||
                        o.MarketName == "Vencedor da Partida" ||
                        o.MarketName == "Money Line"
                    ).ToList();

                    foreach (var odd in mainMarkets)
                    {
                        if (IsHome(odd.OutcomeName, e.HomeTeam)) oddHome = odd.Price;
                        else if (IsDraw(odd.OutcomeName)) oddDraw = odd.Price;
                        else if (IsAway(odd.OutcomeName, e.AwayTeam)) oddAway = odd.Price;
                    }

                    if (oddHome == 0 && e.RawOddsHome > 0) oddHome = e.RawOddsHome;
                    if (oddDraw == 0 && e.RawOddsDraw > 0) oddDraw = e.RawOddsDraw;
                    if (oddAway == 0 && e.RawOddsAway > 0) oddAway = e.RawOddsAway;

                    dtoList.Add(new SportsEventDto
                    {
                        ExternalId = e.ExternalId,
                        SportKey = e.SportKey,
                        HomeTeam = e.HomeTeam,
                        AwayTeam = e.AwayTeam,
                        CommenceTime = DateTime.SpecifyKind(e.CommenceTime, DateTimeKind.Utc),
                        League = e.League,
                        HomeTeamLogo = (!string.IsNullOrEmpty(e.HomeTeamId) && e.HomeTeamId != "0") ? $"https://assets.b365api.com/images/team/m/{e.HomeTeamId}.png" : null,
                        AwayTeamLogo = (!string.IsNullOrEmpty(e.AwayTeamId) && e.AwayTeamId != "0") ? $"https://assets.b365api.com/images/team/m/{e.AwayTeamId}.png" : null,
                        LeagueLogo = (!string.IsNullOrEmpty(e.LeagueId) && e.LeagueId != "0") ? $"https://assets.b365api.com/images/league/s/{e.LeagueId}.png" : null,
                        FlagUrl = !string.IsNullOrEmpty(e.CountryCode) ? $"https://assets.b365api.com/images/flags/{e.CountryCode}.svg" : null,
                        CountryCode = e.CountryCode,
                        HomeScore = hScore,
                        AwayScore = aScore,
                        GameTime = e.GameTime,
                        Status = e.Status,
                        RawOddsHome = oddHome,
                        RawOddsDraw = oddDraw,
                        RawOddsAway = oddAway
                    });
                }

                return Ok(dtoList);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERRO CRÍTICO EVENTS: {ex.Message}");
                return StatusCode(500, new { error = "Erro interno." });
            }
        }

        // 👇 Comparadores
        private bool IsHome(string outcomeName, string homeTeam)
        {
            if (string.IsNullOrEmpty(outcomeName)) return false;
            var n = outcomeName.ToLower().Trim();
            var h = homeTeam?.ToLower().Trim();
            if (n == "1" || n == "casa" || n == "home") return true;
            if (!string.IsNullOrEmpty(h) && (n == h || n.Contains(h) || h.Contains(n))) return true;
            return false;
        }

        private bool IsAway(string outcomeName, string awayTeam)
        {
            if (string.IsNullOrEmpty(outcomeName)) return false;
            var n = outcomeName.ToLower().Trim();
            var a = awayTeam?.ToLower().Trim();
            if (n == "2" || n == "fora" || n == "away") return true;
            if (!string.IsNullOrEmpty(a) && (n == a || n.Contains(a) || a.Contains(n))) return true;
            return false;
        }

        private bool IsDraw(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var n = name.ToLower().Trim();
            return n == "x" || n == "empate" || n == "draw";
        }
    }
}