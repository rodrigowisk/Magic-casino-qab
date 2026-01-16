using Magic_casino_tournament.Models;
using Magic_casino_tournament.Services;
using Microsoft.AspNetCore.Mvc;

namespace Magic_casino_tournament.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TournamentsController : ControllerBase
    {
        private readonly ITournamentService _service;

        public TournamentsController(ITournamentService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<Tournament>>> GetAll()
        {
            return Ok(await _service.GetActiveTournamentsAsync());
        }

        [HttpPost]
        public async Task<ActionResult> Create(Tournament tournament)
        {
            var created = await _service.CreateTournamentAsync(tournament);
            return Ok(created);
        }

        [HttpPost("{id}/join")]
        public async Task<IActionResult> Join(int id, [FromBody] JoinRequest request)
        {
            var result = await _service.JoinTournamentAsync(id, request.UserId);

            if (result == "Success")
                return Ok(new { message = "Inscrição realizada!" });

            return BadRequest(new { error = result });
        }
    }

    public class JoinRequest { public string UserId { get; set; } }
}