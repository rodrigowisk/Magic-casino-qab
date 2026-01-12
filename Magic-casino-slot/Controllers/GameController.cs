using Microsoft.AspNetCore.Mvc;
using Magic_casino_slot.DTOs;
using Magic_casino_slot.Services;

namespace Magic_casino_slot.Controllers
{
    [ApiController]
    public class GameController : ControllerBase
    {
        private readonly FiverService _fiverService;

        public GameController(FiverService fiverService)
        {
            _fiverService = fiverService;
        }

        [HttpPost("api/launch")]
        public async Task<IActionResult> LaunchGame([FromBody] GameLaunchRequest request)
        {
            // Validações básicas para não enviar lixo para a Fiver
            if (string.IsNullOrEmpty(request.UserCode) ||
                string.IsNullOrEmpty(request.GameCode) ||
                string.IsNullOrEmpty(request.ProviderCode))
            {
                return BadRequest(new { status = 0, msg = "Campos obrigatórios: user_code, game_code, provider_code" });
            }

            try
            {
                // Chama o serviço
                var url = await _fiverService.LaunchGameAsync(request.UserCode, request.ProviderCode, request.GameCode);

                // Retorna sucesso
                return Ok(new GameLaunchResponse { LaunchUrl = url });
            }
            catch (Exception ex)
            {
                // Se der erro (ex: saldo insuficiente, jogo bloqueado), repassa a mensagem
                return StatusCode(502, new { status = 0, msg = ex.Message });
            }
        }
    }
}