using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Magic_casino_sportbook.Controllers
{
    [ApiController]
    [Route("api/sports")]
    public class SportsController : ControllerBase
    {
        private readonly OddsService _oddsService;
        private readonly AppDbContext _context;

        public SportsController(OddsService oddsService, AppDbContext context)
        {
            _oddsService = oddsService;
            _context = context;
        }

        [HttpPost("sync-pinnacle")]
        public async Task<IActionResult> Sync()
        {
            try
            {
                await _oddsService.SyncAllSportsToDatabase();
                return Ok(new { message = "Sincronização global concluída no banco AWS!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("active-sports")]
        public async Task<IActionResult> GetActiveSports()
        {
            var agoraUtc = DateTime.UtcNow;

            var sports = await _context.SportsEvents
                .Where(e => e.CommenceTime > agoraUtc)
                .GroupBy(e => e.SportKey)
                .Select(g => new
                {
                    Key = g.Key ?? "unknown", // Evita nulo na chave
                    Count = g.Count()
                })
                .ToListAsync();

            return Ok(sports);
        }

        [HttpGet("events")]
        public async Task<IActionResult> GetEvents([FromQuery] string? sport = null, [FromQuery] string? league = null)
        {
            var agoraUtc = DateTime.UtcNow;

            var query = _context.SportsEvents
                .Include(e => e.Odds)
                .Where(e => e.CommenceTime > agoraUtc)
                .AsQueryable();

            if (!string.IsNullOrEmpty(sport))
            {
                var sportLower = sport.ToLower();
                query = query.Where(e => e.SportKey != null && e.SportKey.ToLower() == sportLower);
            }

            if (!string.IsNullOrEmpty(league))
            {
                var leagueLower = league.ToLower();
                query = query.Where(e => e.League != null && e.League.ToLower().Contains(leagueLower));
            }

            var events = await query
                .OrderBy(e => e.CommenceTime)
                .ToListAsync();

            return Ok(events);
        }
    }
}