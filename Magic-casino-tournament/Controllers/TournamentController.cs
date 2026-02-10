using Magic_casino_tournament.Models;
using Magic_casino_tournament.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Magic_casino_tournament.Controllers
{
    // ✅ DTO para receber Nome e Avatar do Front
    public class JoinTournamentRequest
    {
        public string UserName { get; set; }
        public string Avatar { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class TournamentsController : ControllerBase
    {
        private readonly ITournamentService _service;

        public TournamentsController(ITournamentService service)
        {
            _service = service;
        }

        // ===================================================================================
        // 🔍 LEITURA (PÚBLICO / MISTO)
        // ===================================================================================

        [HttpGet]
        public async Task<ActionResult<List<Tournament>>> GetAll([FromQuery] string? userId)
        {
            return Ok(await _service.GetActiveTournamentsAsync(userId));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Tournament>> GetById(int id, [FromQuery] string? userId)
        {
            var tournament = await _service.GetTournamentByIdAsync(id, userId);

            if (tournament == null)
                return NotFound(new { message = "Torneio não encontrado" });

            return Ok(tournament);
        }

        [HttpGet("{id}/ranking")]
        public async Task<ActionResult<List<TournamentParticipant>>> GetRanking(int id)
        {
            var ranking = await _service.GetTournamentRankingAsync(id);
            return Ok(ranking);
        }

        // -----------------------------------------------------------
        // ✅ bets users
        // -----------------------------------------------------------
        [HttpGet("{id}/bets")]
        [Authorize]
        public async Task<ActionResult> GetMyBets(int id)
        {
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bets = await _service.GetUserBetsAsync(id, userId);
            return Ok(bets);
        }


        // -----------------------------------------------------------
        // ✅ bets outros users
        // -----------------------------------------------------------
        [HttpGet("{id}/participants/{targetUserId}/bets")]
        public async Task<ActionResult> GetPlayerBets(int id, string targetUserId)
        {
            // Reutilizamos o mesmo serviço, pois ele busca pelo ID passado
            // Nota: Não colocamos [Authorize] obrigatório se quiser que seja público, 
            // ou coloque se apenas logados podem ver.

            var bets = await _service.GetUserBetsAsync(id, targetUserId);

            // Se quiser privacidade extra, você pode filtrar os dados aqui antes de retornar,
            // mas como o Front já aplica o Blur, retornar os dados crus está ok.
            return Ok(bets);
        }


        [HttpGet("prize-rules")]
        public ActionResult<List<PrizeRuleDefinition>> GetPrizeRules()
        {
            var rules = PrizeCalculator.GetAvailableRules();
            return Ok(rules);
        }

        // ===================================================================================
        // 🛠️ ADMIN
        // ===================================================================================

        [HttpPost]
        public async Task<ActionResult> Create(Tournament tournament)
        {
            var created = await _service.CreateTournamentAsync(tournament);
            return Ok(created);
        }

        // ===================================================================================
        // 🎮 AÇÕES DO JOGADOR (INSCRIÇÃO E APOSTA)
        [HttpPost("{id}/join")]
        [Authorize]
        public async Task<IActionResult> Join(int id, [FromBody] JoinTournamentRequest request)
        {
            // 1. Extrai CPF/User do Token
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Usuário não identificado.");

            // 2. Pega o Token puro para repassar ao Core
            string token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            // 3. Garante valores padrão caso venha vazio
            string name = string.IsNullOrEmpty(request.UserName) ? "Jogador" : request.UserName;
            string avatar = string.IsNullOrEmpty(request.Avatar) ? "" : request.Avatar;

            // 4. Chama o serviço passando os dados
            var result = await _service.JoinTournamentAsync(id, userId, token, name, avatar);

            if (result == "Success")
                return Ok(new { message = "Inscrição realizada com sucesso!" });

            return BadRequest(new { error = result });
        }

        // ✅ APOSTA EM LOTE
        [HttpPost("{id}/bet")]
        [Authorize]
        public async Task<IActionResult> PlaceBet(int id, [FromBody] PlaceTournamentBetRequest request)
        {
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Usuário não identificado.");

            if (request.Selections == null || !request.Selections.Any())
                return BadRequest(new { error = "Nenhuma aposta enviada." });

            var result = await _service.PlaceBatchBetsAsync(id, userId, request);

            if (result.Success)
                return Ok(new { message = result.Message, count = request.Selections.Count });

            return BadRequest(new { error = result.Message });
        }

        // ===================================================================================
        // 🔧 HELPERS
        // ===================================================================================
        private string? GetUserIdFromToken()
        {
            var rawCpf = User.FindFirst("cpf")?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(rawCpf)) return null;

            // Limpa formatação do CPF (ex: 123.456 -> 123456)
            return Regex.Replace(rawCpf, "[^0-9]", "");
        }
    }

    // ===================================================================================
    // 📦 DTOs (Data Transfer Objects)
    // ===================================================================================

    public class PlaceTournamentBetRequest
    {
        public string? UserId { get; set; }
        public decimal Amount { get; set; }
        public List<TournamentBetSelection> Selections { get; set; } = new();
    }

    public class TournamentBetSelection
    {
        public string GameId { get; set; }
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public string SelectionName { get; set; }
        public string MarketName { get; set; }
        public decimal Odds { get; set; }
    }
}