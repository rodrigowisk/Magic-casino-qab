using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_sportbook.Controllers
{
    [ApiController]
    [Route("api/favorites")]
    public class FavoritesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FavoritesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetFavorites()
        {
            // Retorna apenas os nomes das ligas favoritas para o front saber quais estrelas pintar
            var leagues = await _context.FavoriteLeagues
                .Select(f => f.LeagueName)
                .ToListAsync();
            return Ok(leagues);
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleFavorite([FromBody] FavoriteLeague request)
        {
            var existing = await _context.FavoriteLeagues
                .FirstOrDefaultAsync(f => f.LeagueName == request.LeagueName && f.SportKey == request.SportKey);

            if (existing != null)
            {
                _context.FavoriteLeagues.Remove(existing);
                await _context.SaveChangesAsync();
                return Ok(new { status = "removed" });
            }
            else
            {
                _context.FavoriteLeagues.Add(request);
                await _context.SaveChangesAsync();
                return Ok(new { status = "added" });
            }
        }
    }
}