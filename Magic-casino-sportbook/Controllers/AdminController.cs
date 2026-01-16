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
    // --- DTOs (Formatos de Dados) ---
    // Colocamos aqui para garantir que o Controller use EXATAMENTE estes
    public class SportConfigDto
    {
        public string? Key { get; set; }
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public bool IsActive { get; set; } // O Front manda este
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
        public string? Id { get; set; } // ⚠️ AQUI ESTAVA O ERRO: Agora aceita null (string?)
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
                // 1. Busca configs salvas (Regras)
                var configs = await _context.SportConfigurations.AsNoTracking().ToListAsync();

                // 2. Busca eventos futuros (Dados Reais)
                var events = await _context.SportsEvents
                    .AsNoTracking()
                    .Where(e => e.CommenceTime > DateTime.UtcNow)
                    .ToListAsync();

                // Função local para checar status
                // Se IsEnabled for false, retorna false (Inativo)
                bool CheckActive(string type, string? id)
                {
                    if (string.IsNullOrEmpty(id)) return true;
                    var config = configs.FirstOrDefault(c => c.Type == type && c.Identifier == id);
                    return config == null ? true : config.IsEnabled;
                }

                // 3. Monta a árvore
                var configTree = events
                    .GroupBy(e => e.SportKey)
                    .Select(gSport => new SportConfigDto
                    {
                        Key = gSport.Key,
                        Name = gSport.Key,
                        Icon = "⚽",
                        IsActive = CheckActive("SPORT", gSport.Key),
                        Leagues = gSport
                            .GroupBy(l => l.League)
                            .Select(gLeague => new LeagueConfigDto
                            {
                                Id = gLeague.Key,
                                Name = gLeague.Key,
                                IsActive = CheckActive("LEAGUE", gLeague.Key),
                                Teams = gLeague
                                    .SelectMany(t => new[]
                                    {
                                        new TeamConfigDto { Id = t.HomeTeamId, Name = t.HomeTeam, IsActive = CheckActive("TEAM", t.HomeTeamId) },
                                        new TeamConfigDto { Id = t.AwayTeamId, Name = t.AwayTeam, IsActive = CheckActive("TEAM", t.AwayTeamId) }
                                    })
                                    .Where(t => !string.IsNullOrEmpty(t.Name)) // Remove times sem nome
                                    .GroupBy(t => t.Id ?? t.Name) // Agrupa para não duplicar (usa nome se ID for null)
                                    .Select(g => g.First())
                                    .ToList()
                            }).ToList()
                    }).ToList();

                return Ok(configTree);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao ler dados: " + ex.Message });
            }
        }

        // POST: api/admin/configuration
        [HttpPost("configuration")]
        public async Task<IActionResult> UpdateConfiguration([FromBody] List<SportConfigDto> incomingConfig)
        {
            // Validação básica
            if (incomingConfig == null)
                return BadRequest(new { message = "JSON inválido ou vazio." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                Console.WriteLine($"[ADMIN] Recebendo update de {incomingConfig.Count} esportes.");

                // Lista para salvar no banco
                var newConfigs = new List<SportConfiguration>();

                foreach (var sport in incomingConfig)
                {
                    if (string.IsNullOrEmpty(sport.Key)) continue;

                    // Salva Esporte
                    // Nota: Mapeamos 'IsActive' do front para 'IsEnabled' e 'IsVisible' do banco
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

                        // Salva Liga
                        newConfigs.Add(new SportConfiguration
                        {
                            Type = "LEAGUE",
                            Identifier = league.Name, // Usamos Name pois ID pode ser null na BetsAPI
                            IsEnabled = league.IsActive,
                            IsVisible = league.IsActive
                        });

                        foreach (var team in league.Teams)
                        {
                            // Salva Time (Só se tiver ID, senão ignoramos pois não dá pra filtrar)
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

                // Estratégia de Atualização:
                // Removemos todas as configurações anteriores e inserimos as novas.
                // Isso garante que se você reativou algo, a regra de bloqueio antiga suma.

                _context.SportConfigurations.RemoveRange(_context.SportConfigurations);
                await _context.SaveChangesAsync();

                if (newConfigs.Any())
                {
                    await _context.SportConfigurations.AddRangeAsync(newConfigs);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                Console.WriteLine("[ADMIN] Configurações salvas com sucesso no DB.");
                return Ok(new { message = "Configurações salvas com sucesso!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[ADMIN] ERRO AO SALVAR: {ex.Message}");
                // Retorna 500 com a mensagem real para ajudar no debug
                return StatusCode(500, new { error = ex.Message, details = ex.InnerException?.Message });
            }
        }
    }
}