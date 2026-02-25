using System.Security.Claims;
using System.Text.RegularExpressions;
using Magic_casino_tournament.Hubs;
using Magic_casino_tournament.Models;
using Magic_casino_tournament.Services;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Magic_casino_tournament.Controllers
{
    // DTO para receber Nome e Avatar do Front
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
        private readonly IHubContext<TournamentHub> _hubContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public TournamentsController(ITournamentService service, IHubContext<TournamentHub> hubContext, IPublishEndpoint publishEndpoint)
        {
            _service = service;
            _hubContext = hubContext;
            _publishEndpoint = publishEndpoint;
        }

        // ===================================================================================
        // 🔍 LEITURA (PÚBLICO / MISTO)
        // ===================================================================================

        [HttpGet]
        public async Task<ActionResult<List<Tournament>>> GetAll()
        {
            // 1. CORREÇÃO PRINCIPAL DOS FAVORITOS:
            // Tenta identificar o usuário pelo Token. Se ele estiver logado, pegamos o ID.
            // Se não estiver (anônimo), retorna null.
            string? userId = GetUserIdFromToken();

            // 2. Passa esse ID para o serviço. 
            // O serviço vai usar isso para preencher "IsFavorite = true" nos torneios corretos.
            var tournaments = await _service.GetActiveTournamentsAsync(userId);

            return Ok(tournaments);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Tournament>> GetById(int id)
        {
            // Também pegamos o ID aqui para saber se esse torneio específico é favorito ou se o user já entrou
            string? userId = GetUserIdFromToken();

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
        // ✅ Apostas do Usuário
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

        [HttpGet("history/{userId}")]
        public async Task<ActionResult> GetUserHistory(string userId)
        {
            try
            {
                var history = await _service.GetUserHistoryAsync(userId);
                return Ok(history);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao buscar histórico", error = ex.Message });
            }
        }

        // -----------------------------------------------------------
        // ✅ Apostas de Outros Usuários
        // -----------------------------------------------------------
        [HttpGet("{id}/participants/{targetUserId}/bets")]
        public async Task<ActionResult> GetPlayerBets(int id, string targetUserId)
        {
            var bets = await _service.GetUserBetsAsync(id, targetUserId);
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
            if (created != null)
            {
                await _hubContext.Clients.All.SendAsync("TournamentListUpdate", created);
            }

            return Ok(created);
        }

        // ===================================================================================
        // 🎮 AÇÕES DO JOGADOR (INSCRIÇÃO, FAVORITO E APOSTA)
        // ===================================================================================

        [HttpPost("{id}/join")]
        [Authorize]
        public async Task<IActionResult> Join(int id, [FromBody] JoinTournamentRequest request)
        {
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Usuário não identificado.");

            string token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            // 🔥 CORREÇÃO: Pega o Login real do Token (unique_name ou Name)
            // Isso impede que o 'Nome Completo' seja gravado como 'UserName
            // '
            string realUserName = User.FindFirst("preferred_username")?.Value
                                              ?? User.FindFirst("username")?.Value
                                              ?? request.UserName // << AGORA O FRONTEND TEM PRIORIDADE SOBRE O NOME COMPLETO
                                              ?? "Jogador";

            // Limpeza extra: Se vier com @, remove (opcional, mas bom para padronizar)
            // if (realUserName.StartsWith("@")) realUserName = realUserName.Substring(1);

            string avatar = string.IsNullOrEmpty(request.Avatar) ? "" : request.Avatar;

            var result = await _service.JoinTournamentAsync(id, userId, token, realUserName, avatar);

            if (result == "Success")
                return Ok(new { message = "Inscrição realizada com sucesso!" });

            return BadRequest(new { error = result });
        }

        // 👇 CORREÇÃO DA AÇÃO DE FAVORITAR
        [HttpPost("{id}/favorite")]
        [Authorize] // Obrigatório estar logado para favoritar
        public async Task<IActionResult> ToggleFavorite(int id)
        {
            try
            {
                var userId = GetUserIdFromToken();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                // Chama o método correto no Serviço (que já trata Adicionar/Remover no banco)
                bool isFavorited = await _service.ToggleFavoriteAsync(id, userId);

                // Retorna o novo estado para o Front atualizar o ícone visualmente
                return Ok(new { IsFavorite = isFavorited });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

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
            // Tenta pegar o ID de várias Claims possíveis para garantir compatibilidade
            var rawCpf = User.FindFirst("cpf")?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value
                      ?? User.FindFirst("id")?.Value
                      ?? User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(rawCpf)) return null;

            // Limpa formatação caso seja CPF (ex: remove pontos e traços)
            return Regex.Replace(rawCpf, "[^0-9]", "");
        }
    }

    // DTOs
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
        public DateTime? CommenceTime { get; set; }
    }
}