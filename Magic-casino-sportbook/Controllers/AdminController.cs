using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Magic_casino_sportbook.Controllers
{
    [ApiController]
    [Route("api/admin")] // A rota agora será /api/admin/...
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // 1. Rota para CARREGAR o menu (GET)
        // URL: GET /api/admin/configuration
        [HttpGet("configuration")]
        public async Task<IActionResult> GetConfiguration()
        {
            try
            {
                // Busca eventos futuros para montar a estrutura
                var events = await _context.SportsEvents
                    .AsNoTracking()
                    .Where(e => e.CommenceTime > DateTime.UtcNow)
                    .ToListAsync();

                // Monta a árvore: Esporte -> Liga -> Times
                var configTree = events
                    .GroupBy(e => e.SportKey)
                    .Select(gSport => new SportConfigDto
                    {
                        Key = gSport.Key,
                        Name = gSport.Key, // Nome técnico por enquanto (ex: soccer_epl)
                        Icon = "⚽",       // Ícone padrão
                        IsActive = true,   // Padrão: Ativo
                        Leagues = gSport
                            .GroupBy(l => l.League)
                            .Select(gLeague => new LeagueConfigDto
                            {
                                Id = gLeague.Key,
                                Name = gLeague.Key,
                                IsActive = true,
                                Teams = gLeague
                                    .SelectMany(t => new[]
                                    {
                                        new TeamConfigDto { Id = t.HomeTeamId, Name = t.HomeTeam, IsActive = true },
                                        new TeamConfigDto { Id = t.AwayTeamId, Name = t.AwayTeam, IsActive = true }
                                    })
                                    .GroupBy(t => t.Id) // Remove duplicados pelo ID
                                    .Select(g => g.First())
                                    .ToList()
                            }).ToList()
                    }).ToList();

                return Ok(configTree);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao gerar menu: " + ex.Message });
            }
        }

        // 2. Rota para SALVAR as alterações (POST)
        // URL: POST /api/admin/configuration
        [HttpPost("configuration")]
        public async Task<IActionResult> UpdateConfiguration([FromBody] List<SportConfigDto> config)
        {
            if (config == null || config.Count == 0)
            {
                return BadRequest(new { message = "Nenhum dado de configuração enviado." });
            }

            // --- LÓGICA DE SALVAMENTO (SIMULAÇÃO) ---
            // Como ainda não criamos uma tabela específica para salvar as configurações ("SportsConfig"),
            // os dados recebidos aqui serão perdidos ao reiniciar. 
            // Mas isso serve para testar se o botão "Salvar" do seu site está funcionando.

            Console.WriteLine($"Recebido comando para atualizar {config.Count} esportes.");

            foreach (var sport in config)
            {
                // Aqui você veria no console do servidor o que mudou
                if (!sport.IsActive)
                {
                    Console.WriteLine($"[ADMIN] Desativou o esporte: {sport.Name}");
                }
            }

            // Retorna sucesso para o Frontend não dar erro
            return Ok(new { message = "Configurações recebidas com sucesso! (Lembrete: persistência no banco pendente)" });
        }
    }
}