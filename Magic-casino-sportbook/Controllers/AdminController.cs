using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Magic_casino_sportbook.Controllers
{
    // --- DTOs ---
    public class SportConfigDto
    {
        public string? Key { get; set; }
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public bool IsActive { get; set; }
        public List<LeagueConfigDto> Leagues { get; set; } = new();
    }

    public class LeagueConfigDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public bool IsActive { get; set; }
        public List<TeamConfigDto> Teams { get; set; } = new();
    }

    public class TeamConfigDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public bool IsActive { get; set; }
    }

    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/admin/configuration
        [HttpGet("configuration")]
        public async Task<IActionResult> GetConfiguration()
        {
            try
            {
                // 1. Busca TODAS as configurações salvas no banco
                var configs = await _context.SportConfigurations.AsNoTracking().ToListAsync();

                // 2. Busca eventos (jogos) para preencher as ligas/times
                var events = await _context.SportsEvents
                    .AsNoTracking()
                    .Where(e => e.CommenceTime > DateTime.UtcNow.AddHours(-24))
                    .ToListAsync();

                var sportConfigs = configs.Where(c => c.Type == "SPORT").ToList();

                // 🔥 CORREÇÃO PARA ECONOMIA: Trava os esportes permitidos no sistema
                var allSportsToProcess = new List<string> { "soccer", "basketball", "tennis" };

                var result = new List<SportConfigDto>();

                // 3. Itera APENAS sobre a lista de esportes permitidos
                foreach (var sportKey in allSportsToProcess)
                {
                    var sConf = sportConfigs.FirstOrDefault(c => c.Identifier == sportKey);
                    var sportEvents = events.Where(e => e.SportKey == sportKey).ToList();

                    var dto = new SportConfigDto
                    {
                        Key = sportKey,
                        Name = char.ToUpper(sportKey[0]) + sportKey.Substring(1), // Capitaliza (soccer -> Soccer)
                        Icon = GetIconForSport(sportKey),
                        // Se for um esporte novo (sem config), vem Ativo por padrão
                        IsActive = sConf?.IsEnabled ?? true,
                        Leagues = sportEvents
                            .GroupBy(l => l.League)
                            .Select(gLeague =>
                            {
                                var leagueConfig = configs.FirstOrDefault(c => c.Type == "LEAGUE" && c.Identifier == gLeague.Key);
                                return new LeagueConfigDto
                                {
                                    Id = gLeague.Key,
                                    Name = gLeague.Key,
                                    IsActive = leagueConfig?.IsEnabled ?? true, // Padrão true se não tiver config específica
                                    Teams = gLeague
                                        .SelectMany(t => new[]
                                        {
                                            new TeamConfigDto { Id = t.HomeTeamId, Name = t.HomeTeam, IsActive = CheckActive(configs, "TEAM", t.HomeTeamId) },
                                            new TeamConfigDto { Id = t.AwayTeamId, Name = t.AwayTeam, IsActive = CheckActive(configs, "TEAM", t.AwayTeamId) }
                                        })
                                        .Where(t => !string.IsNullOrEmpty(t.Name))
                                        .GroupBy(t => t.Id ?? t.Name)
                                        .Select(g => g.First())
                                        .ToList()
                                };
                            }).ToList()
                    };

                    result.Add(dto);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao ler dados: " + ex.Message });
            }
        }

        private bool CheckActive(List<SportConfiguration> configs, string type, string? id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            var config = configs.FirstOrDefault(c => c.Type == type && c.Identifier == id);
            return config == null ? true : config.IsEnabled;
        }

        private string GetIconForSport(string key)
        {
            return key.ToLower() switch
            {
                "soccer" => "⚽",
                "basketball" => "🏀",
                "tennis" => "🎾",
                _ => "🏆"
            };
        }

        // POST: api/admin/configuration
        [HttpPost("configuration")]
        public async Task<IActionResult> UpdateConfiguration([FromBody] List<SportConfigDto> incomingConfig)
        {
            if (incomingConfig == null) return BadRequest(new { message = "JSON inválido." });

            // 🔥 Trava de segurança no POST para aceitar apenas os 3 esportes permitidos
            var allowedSports = new List<string> { "soccer", "basketball", "tennis" };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Remove configs antigas para substituir pelas novas
                _context.SportConfigurations.RemoveRange(_context.SportConfigurations);
                await _context.SaveChangesAsync();

                var newConfigs = new List<SportConfiguration>();

                foreach (var sport in incomingConfig)
                {
                    if (string.IsNullOrEmpty(sport.Key) || !allowedSports.Contains(sport.Key.ToLower())) continue;

                    // Salva Esporte 
                    newConfigs.Add(new SportConfiguration
                    {
                        Type = "SPORT",
                        Identifier = sport.Key,
                        IsEnabled = sport.IsActive,
                        IsVisible = sport.IsActive
                    });

                    foreach (var league in sport.Leagues)
                    {
                        if (string.IsNullOrEmpty(league.Name)) continue;
                        newConfigs.Add(new SportConfiguration
                        {
                            Type = "LEAGUE",
                            Identifier = league.Name,
                            IsEnabled = league.IsActive,
                            IsVisible = league.IsActive
                        });

                        // Times
                        foreach (var team in league.Teams)
                        {
                            if (!string.IsNullOrEmpty(team.Id))
                            {
                                newConfigs.Add(new SportConfiguration
                                {
                                    Type = "TEAM",
                                    Identifier = team.Id,
                                    IsEnabled = team.IsActive,
                                    IsVisible = team.IsActive
                                });
                            }
                        }
                    }
                }

                if (newConfigs.Any())
                {
                    await _context.SportConfigurations.AddRangeAsync(newConfigs);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return Ok(new { message = "Configurações salvas com sucesso!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}