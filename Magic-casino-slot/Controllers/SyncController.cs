using Magic_casino_slot.Data;
using Magic_casino_slot.Services;
// using Magic_casino_slot.Data; // Removido duplicata
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json; // Necessário para JsonDocument

namespace Magic_casino_slot.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class SyncController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly FiverService _fiverService;

        public SyncController(AppDbContext context, FiverService fiverService)
        {
            _context = context;
            _fiverService = fiverService;
        }

        // POST: /api/admin/providers/sync
        [HttpPost("providers/sync")]
        public async Task<IActionResult> SyncProviders()
        {
            // TODO: Adicionar verificação de Admin (Senha) igual ao Kotlin

            var jsonString = await _fiverService.GetProviderListAsync();
            if (string.IsNullOrEmpty(jsonString)) return StatusCode(502, "Erro ao contatar Fiver");

            // --- CORREÇÃO AQUI: Parse da string JSON ---
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;
            // -------------------------------------------

            if (!root.TryGetProperty("providers", out var providersArray))
                return Ok(new { status = 0, msg = "Nenhum provedor retornado" });

            int count = 0;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            foreach (var item in providersArray.EnumerateArray())
            {
                string code = item.GetProperty("provider_code").GetString();
                string name = item.GetProperty("provider_name").GetString();
                int status = item.GetProperty("status").GetInt32();

                var existing = await _context.Providers.FindAsync(code);
                if (existing != null)
                {
                    existing.Name = name;
                    existing.LastUpdate = now;
                }
                else
                {
                    _context.Providers.Add(new Provider
                    {
                        Code = code,
                        Name = name,
                        Status = status,
                        LastUpdate = now
                    });
                }
                count++;
            }

            await _context.SaveChangesAsync();
            return Ok(new { status = 1, msg = $"Sincronizados {count} provedores." });
        }

        // POST: /api/admin/games/sync
        [HttpPost("games/sync")]
        public async Task<IActionResult> SyncGames()
        {
            // Busca apenas provedores ativos
            var providers = await _context.Providers.Where(p => p.Status == 1).ToListAsync();
            int totalGames = 0;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            foreach (var prov in providers)
            {
                var jsonString = await _fiverService.GetGameListAsync(prov.Code);
                if (string.IsNullOrEmpty(jsonString)) continue;

                // --- CORREÇÃO AQUI: Parse da string JSON ---
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;
                // -------------------------------------------

                if (root.TryGetProperty("games", out var gamesArray))
                {
                    foreach (var g in gamesArray.EnumerateArray())
                    {
                        string gCode = g.GetProperty("game_code").GetString();
                        string gName = g.GetProperty("game_name").GetString();
                        string? banner = g.TryGetProperty("banner", out var b) ? b.GetString() : null;
                        int status = g.TryGetProperty("status", out var s) ? s.GetInt32() : 1;

                        var existing = await _context.Games
                            .FirstOrDefaultAsync(x => x.GameCode == gCode && x.ProviderCode == prov.Code);

                        if (existing != null)
                        {
                            existing.Name = gName;
                            if (!string.IsNullOrEmpty(banner)) existing.Banner = banner;
                            existing.LastUpdate = now;
                        }
                        else
                        {
                            _context.Games.Add(new Game
                            {
                                GameCode = gCode,
                                ProviderCode = prov.Code,
                                Name = gName,
                                Banner = banner,
                                Status = status,
                                LastUpdate = now
                            });
                        }
                        totalGames++;
                    }
                    // Salva a cada provedor para não estourar a memória
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new { status = 1, msg = $"Sincronização concluída. Total: {totalGames} jogos." });
        }
    }
}