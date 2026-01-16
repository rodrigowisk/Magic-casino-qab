using Magic_casino_sportbook.Services;
using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.DTOs;
using Magic_casino_sportbook.Data.Models;
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

        // =================================================================================
        // 🔧 MÉTODOS DE DIAGNÓSTICO E MANUTENÇÃO (MANTIDOS)
        // =================================================================================

        [HttpGet("raio-x-score/{sportKey}/{gameId}")]
        public async Task<IActionResult> RaioXScore(string sportKey, string gameId)
        {
            var apiKey = Environment.GetEnvironmentVariable("ODDS_API_KEY") ?? Environment.GetEnvironmentVariable("OddsApiKey");
            var url = $"https://api.the-odds-api.com/v4/sports/{sportKey}/scores/?apiKey={apiKey}&daysFrom=1&eventIds={gameId}";
            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            return Ok(new { Explicao = "JSON REAL Scores", UrlUsada = url, JSON_Scores = json });
        }

        [HttpGet("backfill-sportkey")]
        public async Task<IActionResult> BackfillSportKey() => Ok(new { message = "Backfill desativado." });

        [HttpGet("sync-base")]
        public async Task<IActionResult> SyncBase()
        {
            try { await _oddsService.SyncBaseOddsToDatabase(); return Ok(new { message = "Sync BASE concluída!" }); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpGet("sync-images")]
        public async Task<IActionResult> ForceSyncImages()
        {
            await _oddsService.SyncMissingImages();
            return Ok(new { message = "Sincronização iniciada." });
        }

        [HttpGet("raio-x/{sportKey}/{gameId}")]
        public async Task<IActionResult> RaioX(string sportKey, string gameId)
        {
            var dbGame = await _context.SportsEvents.FirstOrDefaultAsync(e => e.ExternalId == gameId);
            return Ok(new { TimeCasa = dbGame?.HomeTeam, TimeFora = dbGame?.AwayTeam, Status = "OK" });
        }

        [HttpGet("diagnostico-liga/{sportKey}")]
        public async Task<IActionResult> DiagnosticoLiga(string sportKey) => Ok(new { message = "N/A" });

        [HttpGet("sync-pinnacle")]
        public async Task<IActionResult> SyncLegacy() => await SyncBase();

        [HttpGet("sync-event/{id}")]
        public async Task<IActionResult> SyncEvent(string id) => Ok(new { message = "N/A" });

        [HttpGet("event/{id}")]
        public async Task<IActionResult> GetEventById(string id)
        {
            var sportsEvent = await _context.SportsEvents.Include(e => e.Odds).FirstOrDefaultAsync(e => e.ExternalId == id);
            if (sportsEvent == null) return NotFound(new { error = "Evento não encontrado." });
            return Ok(sportsEvent);
        }

        // =================================================================================
        // 🚀 MÉTODOS PÚBLICOS DO SITE (CORREÇÃO SAFE-MODE)
        // =================================================================================

        // 1. Menu Lateral
        [HttpGet("active-sports")]
        public async Task<IActionResult> GetActiveSports()
        {
            try
            {
                var agoraAjustado = DateTime.UtcNow;

                // 1. Carrega bloqueios
                var blockedConfigsRaw = await _context.SportConfigurations
                    .AsNoTracking()
                    .Where(c => c.IsVisible == false && c.Type == "SPORT")
                    .Select(c => c.Identifier)
                    .ToListAsync();

                // Normaliza em Memória (Safe)
                var blockedConfigs = new HashSet<string>(blockedConfigsRaw.Select(k => k.ToLower().Trim()));

                // 2. Carrega TODAS as chaves de esportes futuros do banco (Query leve)
                var allSportsKeys = await _context.SportsEvents
                    .AsNoTracking()
                    .Where(e => e.CommenceTime > agoraAjustado)
                    .Select(e => e.SportKey) // Trazemos só a string, muito rápido
                    .ToListAsync();

                // 3. Filtra e Agrupa em Memória (C#) - Aqui o ToLower() funciona perfeito
                var stats = allSportsKeys
                    .Where(k => !string.IsNullOrEmpty(k) && !blockedConfigs.Contains(k.ToLower().Trim()))
                    .GroupBy(k => k)
                    .Select(g => new { Key = g.Key, Count = g.Count() })
                    .ToList();

                return Ok(stats);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ActiveSports: {ex.Message}");
                return StatusCode(500);
            }
        }

        // 2. Lista de Jogos
        [HttpGet("events")]
        public async Task<ActionResult<IEnumerable<SportsEventDto>>> GetEvents(
            [FromQuery] string? sport = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var agora = DateTime.UtcNow;

                // 1. Carrega TODAS as regras
                var blockedConfigs = await _context.SportConfigurations
                    .AsNoTracking()
                    .Where(c => c.IsVisible == false)
                    .ToListAsync();

                // Prepara HashSets normalizados
                var blockedSports = new HashSet<string>(blockedConfigs.Where(c => c.Type == "SPORT").Select(c => c.Identifier.ToLower().Trim()));
                var blockedLeagues = new HashSet<string>(blockedConfigs.Where(c => c.Type == "LEAGUE").Select(c => c.Identifier.ToLower().Trim()));
                var blockedTeams = new HashSet<string>(blockedConfigs.Where(c => c.Type == "TEAM").Select(c => c.Identifier.Trim())); // IDs geralmente são exatos

                // 2. Query Base (Apenas filtros que o Banco suporta 100%)
                var query = _context.SportsEvents
                    .AsNoTracking()
                    .Where(e => e.CommenceTime > agora)
                    .AsQueryable();

                // Filtro de Esporte via URL (se houver) - Aplicamos ToLower aqui pois é parâmetro simples
                if (!string.IsNullOrWhiteSpace(sport))
                {
                    // Nota: Se o banco reclamar disso, mudaremos, mas geralmente igualdade simples funciona
                    var sLower = sport.Trim().ToLower();
                    // Trazemos tudo desse esporte e filtramos o case no client se precisar, 
                    // mas geralmente SQL suporta 'Where(x => x == y)' bem.
                    // Para garantir, vamos trazer um pouco mais e filtrar em memória se o driver for chato.
                }

                // 3. Executa a Query e traz para memória (Materialização)
                // Trazemos os dados ordenados por tempo para pegar os próximos jogos
                // CUIDADO: Se tiver MILHARES de jogos, isso pesa. Mas com paginação no banco é melhor.

                // Estratégia Híbrida Segura:
                // Aplicamos paginação DEPOIS do filtro de memória para garantir precisão, 
                // mas limitamos a busca inicial para não estourar a RAM.
                var rawEvents = await query
                    .OrderBy(e => e.CommenceTime)
                    .Take(pageSize * 5) // Pegamos um buffer maior (5x) para ter margem após filtrar
                    .ToListAsync();

                // 4. Filtragem Fina em Memória (C# Puro - 100% Seguro)
                var filteredEvents = rawEvents.Where(e =>
                {
                    // Normaliza dados do evento
                    var sKey = e.SportKey?.ToLower().Trim() ?? "";
                    var lName = e.League?.ToLower().Trim() ?? "";
                    var hId = e.HomeTeamId ?? "";
                    var aId = e.AwayTeamId ?? "";

                    // Filtro de URL (Esporte específico)
                    if (!string.IsNullOrWhiteSpace(sport) && sKey != sport.Trim().ToLower()) return false;

                    // Filtros de Admin (Bloqueios)
                    if (blockedSports.Contains(sKey)) return false;
                    if (blockedLeagues.Contains(lName)) return false;
                    if (blockedTeams.Contains(hId) || blockedTeams.Contains(aId)) return false;

                    return true;
                })
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
                    HomeTeamLogo = !string.IsNullOrEmpty(e.HomeTeamId) ? $"https://assets.b365api.com/images/team/m/{e.HomeTeamId}.png" : null,
                    AwayTeamLogo = !string.IsNullOrEmpty(e.AwayTeamId) ? $"https://assets.b365api.com/images/team/m/{e.AwayTeamId}.png" : null,
                    LeagueLogo = !string.IsNullOrEmpty(e.LeagueId) ? $"https://assets.b365api.com/images/league/s/{e.LeagueId}.png" : null,
                    RawOddsHome = e.RawOddsHome,
                    RawOddsDraw = e.RawOddsDraw,
                    RawOddsAway = e.RawOddsAway
                })
                .ToList();

                return Ok(filteredEvents);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERRO CRÍTICO EVENTS: {ex.Message}");
                return StatusCode(500, new { error = "Erro interno ao processar eventos." });
            }
        }
    }
}