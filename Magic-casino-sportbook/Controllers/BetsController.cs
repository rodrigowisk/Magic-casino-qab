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

            // ===================================================================================
            // 🛡️ TRAVA DE SEGURANÇA: VALIDAÇÃO DE ODDS (ANTI-LAG)
            // ===================================================================================
            // Antes de tirar dinheiro, verificamos se as odds mudaram no banco
            foreach (var selection in request.Selections)
            {
                // Busca o jogo no banco pelo ID externo (que vem do front)
                var game = await _context.SportsEvents
                    .AsNoTracking() // Mais rápido, apenas leitura
                    .FirstOrDefaultAsync(e => e.ExternalId == selection.MatchId || e.Id == selection.MatchId);

                if (game == null)
                {
                    // Se o jogo não existe mais (foi removido), rejeita
                    return BadRequest(new { error = $"O jogo {selection.MatchName} não está mais disponível para apostas." });
                }

                if (game.Status != "Live" && game.Status != "Prematch" && game.Status != "WaitingLive")
                {
                    return BadRequest(new { error = $"O jogo {selection.MatchName} já encerrou ou está suspenso." });
                }

                // Determina qual é a odd ATUAL no banco baseada na seleção do usuário
                decimal currentOdd = 0;

                // Lógica para Mercado 1x2 (Winner)
                // Normaliza strings para comparar (ignora maiusculas/minusculas)
                string selName = selection.SelectionName?.Trim().ToLower() ?? "";
                string homeName = game.HomeTeam?.Trim().ToLower() ?? "casa";
                string awayName = game.AwayTeam?.Trim().ToLower() ?? "fora";

                // 🔥 CORREÇÃO ERRO CS0019: Removido '?? 0' pois as propriedades já são decimal (não nulas)
                if (selName == homeName || selection.MarketName == "1")
                {
                    currentOdd = game.RawOddsHome;
                }
                else if (selName == awayName || selection.MarketName == "2")
                {
                    currentOdd = game.RawOddsAway;
                }
                else if (selName == "empate" || selName == "x" || selName == "draw")
                {
                    currentOdd = game.RawOddsDraw;
                }
                else
                {
                    // Se for um mercado que não mapeamos a odd simples (ex: over/under), 
                    // por segurança aceitamos ou ignoramos a validação de odd exata.
                    currentOdd = selection.Odd;
                }

                // Se a odd for válida e diferente da enviada (com margem de erro de 0.05)
                if (currentOdd > 1.0m && Math.Abs(selection.Odd - currentOdd) > 0.05m)
                {
                    // 🚨 CONFLITO DETECTADO! Retorna 409 para o Frontend abrir o Popup
                    return Conflict(new
                    {
                        code = "ODDS_CHANGED",
                        message = "A cotação mudou.",
                        matchName = $"{game.HomeTeam} x {game.AwayTeam}",
                        selectionId = selection.MatchId,
                        oldOdd = selection.Odd,
                        newOdd = currentOdd
                    });
                }
            }
            // ===================================================================================


            // 2. Chamar CORE (Débito)
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
                // Recalcula a odd total baseada no request (que agora sabemos que está validado)
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

            // 🔥 CORREÇÃO ERRO CS1061: Removido 'newBalance = result.NewBalance' pois a tupla não tem essa propriedade
            return Ok(new { status = 1, betId = bet.Id, message = "Aposta realizada com sucesso!" });
        }


        [HttpPost("confirm")]
        public Task<IActionResult> Confirm([FromBody] ConfirmBetRequestDto request)
        {
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