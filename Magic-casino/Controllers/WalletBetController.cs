using Magic_casino.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net.Http.Json;

namespace Magic_Casino_Core.Controllers
{
    [ApiController]
    [Route("api/wallet")]
    public class WalletBetController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public WalletBetController(AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        [HttpPost("place-bet")]
        [Authorize]
        public async Task<IActionResult> PlaceBet([FromBody] PlaceBetRequestDto request)
        {
            if (request == null) return BadRequest(new { error = "Body inválido" });
            if (request.Amount <= 0) return BadRequest(new { error = "Amount inválido" });
            if (request.Selections == null || request.Selections.Count == 0) return BadRequest(new { error = "Seleções inválidas" });

            // ✅ CPF vem do token
            var cpf =
                User?.FindFirst("cpf")?.Value ??
                User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                User?.FindFirst("unique_name")?.Value ??
                User?.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(cpf))
                return Unauthorized(new { error = "CPF não encontrado no token" });

            // ✅ carteira é pelo CPF (você disse que userId = cpf)
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserCpf == cpf);
            if (wallet == null)
                return NotFound(new { error = "Carteira não encontrada" });

            if (wallet.BalanceQab < request.Amount)
                return BadRequest(new { error = "Saldo insuficiente" });

            // 1) Debita no CORE
            wallet.BalanceQab -= request.Amount;
            await _context.SaveChangesAsync();

            var newBalance = wallet.BalanceQab;

            // 2) Confirma no SPORTBOOK (interno)
            // ✅ Em docker-compose use nome do service, ex:
            // "Sportbook:BaseUrl": "http://magic-casino-sportbook:8080"
            var sportbookBaseUrl =
                _config["Sportbook:BaseUrl"]
                ?? "http://localhost:8090"; // fallback local

            var coreTxnId = string.IsNullOrWhiteSpace(request.RequestId)
                ? Guid.NewGuid().ToString("N")
                : request.RequestId;

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(sportbookBaseUrl);

                var payload = new
                {
                    userCpf = cpf,
                    amount = request.Amount,
                    totalOdd = request.TotalOdd,
                    potentialReturn = request.PotentialReturn,
                    coreTxnId = coreTxnId,
                    selections = request.Selections
                };

                var resp = await client.PostAsJsonAsync("/api/bets/confirm", payload);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();

                    // estorna
                    wallet.BalanceQab += request.Amount;
                    await _context.SaveChangesAsync();

                    return StatusCode(502, new
                    {
                        error = "Sportbook falhou. Débito estornado no CORE.",
                        sportbookStatus = (int)resp.StatusCode,
                        sportbookBody = body
                    });
                }

                var ok = await resp.Content.ReadFromJsonAsync<SportbookConfirmResponseDto>();

                return Ok(new
                {
                    status = 1,
                    betId = ok?.BetId ?? 0,
                    newBalance = newBalance,
                    message = "Aposta confirmada"
                });
            }
            catch (Exception ex)
            {
                // estorna
                wallet.BalanceQab += request.Amount;
                await _context.SaveChangesAsync();

                return StatusCode(502, new
                {
                    error = "Falha ao chamar sportbook. Débito estornado no CORE.",
                    details = ex.Message
                });
            }
        }
    }

    public class PlaceBetRequestDto
    {
        public string? RequestId { get; set; }

        public decimal Amount { get; set; }
        public decimal TotalOdd { get; set; }
        public decimal PotentialReturn { get; set; }

        public List<BetSelectionDto> Selections { get; set; } = new();
    }

    public class BetSelectionDto
    {
        public string? MatchId { get; set; }
        public string? MatchName { get; set; }
        public string? SelectionName { get; set; }
        public string? MarketName { get; set; }
        public decimal Odd { get; set; }
    }

    public class SportbookConfirmResponseDto
    {
        public int Status { get; set; }
        public int BetId { get; set; }
        public string? Message { get; set; }
    }
}
