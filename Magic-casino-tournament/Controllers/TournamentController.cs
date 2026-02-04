using Magic_casino_tournament.Models;
using Magic_casino_tournament.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.RegularExpressions;

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

        [HttpGet("{id}/bets")]
        [Authorize]
        public async Task<ActionResult> GetMyBets(int id)
        {
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var bets = await _service.GetUserBetsAsync(id, userId);
            return Ok(bets);
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
        // ===================================================================================

        // ✅ INSCRIÇÃO PAGA (COM TOKEN DO CORE)
        [HttpPost("{id}/join")]
        [Authorize] // Protege a rota
        public async Task<IActionResult> Join(int id)
        {
            // 1. Extrai CPF/User do Token
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Usuário não identificado.");

            // 2. Pega o Token puro para repassar ao Core (para validar o débito lá)
            string token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            // 3. Chama o serviço
            var result = await _service.JoinTournamentAsync(id, userId, token);

            if (result == "Success")
                return Ok(new { message = "Inscrição realizada com sucesso!" });

            return BadRequest(new { error = result });
        }

        // ✅ APOSTA EM LOTE (CORRIGIDO PARA RECEBER LISTA)
        [HttpPost("{id}/bet")]
        [Authorize] // Protege a rota
        public async Task<IActionResult> PlaceBet(int id, [FromBody] PlaceTournamentBetRequest request)
        {
            // 1. Extrai CPF/User do Token
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Usuário não identificado.");

            // 2. Validações Básicas
            if (request.Selections == null || !request.Selections.Any())
                return BadRequest(new { error = "Nenhuma aposta enviada." });

            // 3. Chama o serviço de Lote (Batch)
            // OBS: Certifique-se de ter atualizado a Interface ITournamentService para ter este método
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
    // 📦 DTOs (Data Transfer Objects) para o Controller
    // ===================================================================================

    public class PlaceTournamentBetRequest
    {
        // O UserId vem do Token, mas se o frontend mandar no body, ignoramos ou usamos para validação cruzada
        public string? UserId { get; set; }

        public decimal Amount { get; set; } // Valor base da aposta (se for fixa) ou total

        public List<TournamentBetSelection> Selections { get; set; } = new();
    }

    public class TournamentBetSelection
    {
        public string GameId { get; set; }
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public string SelectionName { get; set; } // "1", "X", "2"
        public string MarketName { get; set; }
        public decimal Odds { get; set; }
    }
}