using Magic_casino_tournament.Data;
using Magic_casino_tournament.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Magic_casino_tournament.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TournamentTemplatesController : ControllerBase
    {
        private readonly TournamentDbContext _context;

        public TournamentTemplatesController(TournamentDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<TournamentTemplate>>> GetAll()
        {
            return await _context.TournamentTemplates.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult> Create(TournamentTemplate template)
        {
            _context.TournamentTemplates.Add(template);
            await _context.SaveChangesAsync();
            return Ok(template);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var template = await _context.TournamentTemplates.FindAsync(id);
            if (template == null) return NotFound();

            _context.TournamentTemplates.Remove(template);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}