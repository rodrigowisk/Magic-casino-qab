using Magic_casino_sportbook.Data;
using Magic_casino_sportbook.Models;
using Magic_casino_sportbook.DTOs;
using Magic_casino_sportbook.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Magic_casino_sportbook.Controllers
{
    [ApiController]
    [Route("api/bets")]
    public class BetsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly CoreWalletService _walletService;

        public BetsController(AppDbContext context, CoreWalletService walletService)
        {
            _context = context;
            _walletService = walletService;
        }

        [HttpGet("my-balance")]
        [Authorize]
        public async Task<IActionResult> GetMyBalance()
        {
            // Pega o CPF do token
            var cpf = User.FindFirst("cpf")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(cpf)) return Unauthorized();

            string token = Request.Headers["Authorization"].ToString();

            // Busca o saldo atualizado no Core
            decimal balance = await _walletService.GetBalanceAsync(cpf, token);

            return Ok(new { balance = balance });
        }


        // ============================================================
        // 📜 HISTÓRICO DE APOSTAS - ATUALIZADO COM RESULTADOS
        // ============================================================
        [HttpGet("my-history")]
        [Authorize]
        public async Task<IActionResult> GetMyBets()
        {
            try
            {
                // 1. Pega o CPF do token
                var cpf = User.FindFirst("cpf")?.Value
                        ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(cpf)) return Unauthorized("CPF não identificado no token.");

                // 2. Busca as apostas no banco
                var bets = await _context.Bets
                    .Include(b => b.Selections)
                    .Where(b => b.UserCpf == cpf)
                    .OrderByDescending(b => b.CreatedAt) // Mais recentes primeiro
                    .ToListAsync();

                // 3. Transforma no DTO para enviar pro Frontend
                var response = bets.Select(b => new BetHistoryResponse
                {
                    Id = b.Id.ToString(),
                    Code = b.Id.ToString().PadLeft(8, '0').ToUpper(),
                    Amount = b.Amount,
                    TotalOdd = b.TotalOdd,
                    PotentialReturn = b.PotentialReturn,
                    Status = b.Status ?? "pending",
                    CreatedAt = b.CreatedAt,
                    Selections = b.Selections.Select(s => new BetSelectionResponse
                    {
                        MatchName = s.MatchName,
                        MarketName = s.MarketName,
                        SelectionName = s.SelectionName,
                        Odd = s.Odd,
                        Status = s.Status ?? "pending",
                        CommenceTime = s.CommenceTime,
                        // ✅ NOVOS CAMPOS: Mapeando o resultado do banco para o Frontend
                        FinalScore = s.FinalScore,
                        IsWinner = s.IsWinner
                    }).ToList()
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao buscar histórico", details = ex.Message });
            }
        }


        [HttpPost("place")]
        [Authorize]
        public async Task<IActionResult> PlaceBet([FromBody] BetRequestDto request)
        {
            // 1. Identificar CPF (Lógica blindada)
            var cpf = User.FindFirst("cpf")?.Value
                    ?? User.FindFirst(ClaimTypes.Name)?.Value
                    ?? User.FindFirst("unique_name")?.Value
                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(cpf))
            {
                return Unauthorized(new { error = "CPF não identificado no token." });
            }

            if (request.Amount <= 0) return BadRequest(new { error = "Valor inválido." });

            // 2. Chamar CORE
            string token = Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrEmpty(token)) token = token.Replace("\"", "").Trim();

            var result = await _walletService.DeductFundsAsync(cpf, request.Amount, token);

            if (!result.Success)
            {
                return BadRequest(new { error = result.Message });
            }

            // 3. Salvar Aposta
            var bet = new Bet
            {
                UserCpf = cpf,
                Amount = request.Amount,
                TotalOdd = request.Selections.Any() ? request.Selections.Aggregate(1m, (acc, s) => acc * s.Odd) : 1m,
                CoreTxnId = Guid.NewGuid().ToString(),
                Status = "confirmed",
                CreatedAt = DateTime.UtcNow,
                Selections = request.Selections.Select(s => new BetSelection
                {
                    MatchId = s.MatchId ?? "0",
                    MatchName = s.MatchName ?? "Jogo",
                    SelectionName = s.SelectionName ?? "Seleção",
                    MarketName = s.MarketName ?? "Mercado",
                    Odd = s.Odd,
                    Status = "pending",
                    CommenceTime = s.CommenceTime
                    // FinalScore e IsWinner iniciam como null no banco
                }).ToList()
            };
            bet.PotentialReturn = bet.Amount * bet.TotalOdd;

            try
            {
                _context.Bets.Add(bet);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao salvar bilhete.", details = ex.Message });
            }

            return Ok(new { status = 1, betId = bet.Id, message = "Aposta realizada com sucesso!" });
        }


        [HttpPost("confirm")]
        public Task<IActionResult> Confirm([FromBody] ConfirmBetRequestDto request)
        {
            // ✅ CORREÇÃO: Removido 'async' e retornado Task para evitar aviso CS1998
            return Task.FromResult<IActionResult>(Ok());
        }
    }

    public class ConfirmBetRequestDto
    {
        public string? UserCpf { get; set; }
        public decimal Amount { get; set; }
        public decimal TotalOdd { get; set; }
        public decimal PotentialReturn { get; set; }
        public string? CoreTxnId { get; set; }
        public List<BetSelectionDto> Selections { get; set; } = new();
    }
}