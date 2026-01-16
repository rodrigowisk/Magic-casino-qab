using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.DTOs; // ✅ Importante para usar o DTO
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Magic_casino_sportbook.Controllers
{
    [ApiController]
    [Route("api/sports")]
    public class SportsController : ControllerBase
    {
        private readonly IOddsService _oddsService;
        private readonly AppDbContext _context;

        public SportsController(IOddsService oddsService, AppDbContext context)
        {
            _oddsService = oddsService;
            _context = context;
        }

        // --- MÉTODOS DE DIAGNÓSTICO ---

        [HttpGet("raio-x-score/{sportKey}/{gameId}")]
        public async Task<IActionResult> RaioXScore(string sportKey, string gameId)
        {
            var apiKey = Environment.GetEnvironmentVariable("ODDS_API_KEY")
                         ?? Environment.GetEnvironmentVariable("OddsApiKey");

            var url = $"https://api.the-odds-api.com/v4/sports/{sportKey}/scores/?apiKey={apiKey}&daysFrom=1&eventIds={gameId}";

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            return Ok(new
            {
                Explicao = "Este é o JSON REAL que o Robô lê para atualizar o placar.",
                UrlUsada = url,
                JSON_Scores = json
            });
        }

        [HttpGet("backfill-sportkey")]
        public async Task<IActionResult> BackfillSportKey()
        {
            return Ok(new { message = "Backfill desativado para BetsAPI." });
        }

        [HttpGet("sync-base")]
        public async Task<IActionResult> SyncBase()
        {
            try
            {
                await _oddsService.SyncBaseOddsToDatabase();
                return Ok(new { message = "Sync BASE concluída com sucesso! (Motor: " + _oddsService.GetType().Name + ")" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }


        [HttpGet("sync-images")]
        public async Task<IActionResult> ForceSyncImages()
        {
            await _oddsService.SyncMissingImages();
            return Ok(new { message = "Sincronização de imagens iniciada em segundo plano." });
        }


        [HttpGet("raio-x/{sportKey}/{gameId}")]
        public async Task<IActionResult> RaioX(string sportKey, string gameId)
        {
            var dbGame = await _context.SportsEvents.FirstOrDefaultAsync(e => e.ExternalId == gameId);
            var dbStat = await _context.LiveGameStat.FirstOrDefaultAsync(s => s.GameId == gameId);

            return Ok(new
            {
                StatusBanco = new
                {
                    TimeCasa = dbGame?.HomeTeam,
                    TimeFora = dbGame?.AwayTeam,
                    PlacarBanco = $"{dbStat?.HomeScore} - {dbStat?.AwayScore}",
                    OddsCasa = dbGame?.RawOddsHome,
                    OddsEmpate = dbGame?.RawOddsDraw,
                    OddsFora = dbGame?.RawOddsAway,
                    ImgCasa = dbGame?.HomeTeamId,
                    ImgFora = dbGame?.AwayTeamId
                }
            });
        }

        [HttpGet("diagnostico-liga/{sportKey}")]
        public async Task<IActionResult> DiagnosticoLiga(string sportKey)
        {
            return Ok(new { message = "Diagnóstico não disponível no motor BetsAPI." });
        }

        [HttpGet("sync-pinnacle")]
        public async Task<IActionResult> SyncLegacy()
        {
            return await SyncBase();
        }

        [HttpGet("sync-event/{id}")]
        public async Task<IActionResult> SyncEvent(string id)
        {
            return Ok(new { message = "Sync de evento individual não necessário para BetsAPI." });
        }

        [HttpGet("event/{id}")]
        public async Task<IActionResult> GetEventById(string id)
        {
            try
            {
                var sportsEvent = await _context.SportsEvents
                    .Include(e => e.Odds)
                    .FirstOrDefaultAsync(e => e.ExternalId == id);

                if (sportsEvent == null)
                    return NotFound(new { error = "Evento não encontrado." });

                return Ok(sportsEvent);
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Erro ao buscar detalhes." });
            }
        }

        [HttpGet("active-sports")]
        public async Task<IActionResult> GetActiveSports()
        {
            try
            {
                var agoraAjustado = DateTime.UtcNow;

                var stats = await _context.SportsEvents
                    .Where(e => e.CommenceTime > agoraAjustado)
                    .GroupBy(e => e.SportKey)
                    .Select(g => new { Key = g.Key, Count = g.Count() })
                    .ToListAsync();

                return Ok(stats);
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
        }

        // 🚨 MÉTODO CORRIGIDO: Data, Odds e IMAGENS 🚨
        [HttpGet("events")]
        public async Task<ActionResult<IEnumerable<SportsEventDto>>> GetEvents(
            [FromQuery] string? sport = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                // 🕒 Filtro de Tempo: Mostra apenas jogos que AINDA NÃO COMEÇARAM
                var agora = DateTime.UtcNow;

                var query = _context.SportsEvents
                    .AsNoTracking()
                    .Where(e => e.CommenceTime > agora)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(sport))
                {
                    var sportLower = sport.Trim().ToLower();
                    query = query.Where(e => e.SportKey.ToLower() == sportLower);
                }

                // ✅ Mapeamento Completo
                var events = await query
                    .OrderBy(e => e.CommenceTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new SportsEventDto
                    {
                        ExternalId = e.ExternalId,
                        SportKey = e.SportKey,
                        HomeTeam = e.HomeTeam,
                        AwayTeam = e.AwayTeam,
                        CommenceTime = e.CommenceTime,
                        League = e.League,

                        // 🖼️ LÓGICA DE IMAGENS (Se tiver ID, monta URL da BetsAPI)
                        HomeTeamLogo = !string.IsNullOrEmpty(e.HomeTeamId)
                            ? $"https://assets.b365api.com/images/team/m/{e.HomeTeamId}.png"
                            : null,

                        AwayTeamLogo = !string.IsNullOrEmpty(e.AwayTeamId)
                            ? $"https://assets.b365api.com/images/team/m/{e.AwayTeamId}.png"
                            : null,

                        LeagueLogo = !string.IsNullOrEmpty(e.LeagueId)
                             ? $"https://assets.b365api.com/images/league/s/{e.LeagueId}.png"
                             : null,

                        // 💰 Odds
                        RawOddsHome = e.RawOddsHome,
                        RawOddsDraw = e.RawOddsDraw,
                        RawOddsAway = e.RawOddsAway
                    })
                    .ToListAsync();

                return Ok(events);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}