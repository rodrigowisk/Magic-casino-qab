using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.DTOs;
using Magic_casino_sportbook.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_sportbook.Controllers
{
    [ApiController]
    [Route("api/LiveEvents")]
    public class LiveEventsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LiveEventsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetLiveEvents()
        {
            var agora = DateTime.UtcNow;

            var liveGames = await _context.SportsEvents
                .Where(e => e.CommenceTime <= agora
                            && e.CommenceTime > agora.AddHours(-5)
                            && _context.LiveGameStat.Any(s => s.GameId == e.ExternalId && s.LastUpdated > agora.AddMinutes(-10)))
                .Select(e => new LiveGameDto
                {
                    GameId = e.ExternalId,
                    SportKey = e.SportKey,
                    HomeTeam = e.HomeTeam,
                    AwayTeam = e.AwayTeam,
                    League = e.League,

                    // ✅ AQUI PREENCHEMOS AS IMAGENS
                    HomeTeamLogo = e.HomeTeamLogo,
                    AwayTeamLogo = e.AwayTeamLogo,

                    CommenceTime = e.CommenceTime,
                    HomeScore = _context.LiveGameStat.Where(s => s.GameId == e.ExternalId).Select(s => s.HomeScore).FirstOrDefault(),
                    AwayScore = _context.LiveGameStat.Where(s => s.GameId == e.ExternalId).Select(s => s.AwayScore).FirstOrDefault(),
                    CurrentMinute = _context.LiveGameStat.Where(s => s.GameId == e.ExternalId).Select(s => s.CurrentMinute).FirstOrDefault() ?? "0'",
                    Period = _context.LiveGameStat.Where(s => s.GameId == e.ExternalId).Select(s => s.Period).FirstOrDefault() ?? "Live",
                    RawOddsHome = e.RawOddsHome,
                    RawOddsDraw = e.RawOddsDraw,
                    RawOddsAway = e.RawOddsAway
                })
                .ToListAsync();

            return Ok(liveGames);
        }
    }
}