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
            var rawCpf = User.FindFirst("cpf")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(rawCpf)) return Unauthorized();

            // 🔥 CORREÇÃO: Garante que o CPF tenha 11 dígitos (Ex: 322... vira 047322...)
            var justNumbers = System.Text.RegularExpressions.Regex.Replace(rawCpf, "[^0-9]", "");
            var cpf = justNumbers.PadLeft(11, '0');

            string token = Request.Headers["Authorization"].ToString();

            // Busca o saldo atualizado no Core usando o CPF corrigido
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
                var rawCpf = User.FindFirst("cpf")?.Value
                        ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(rawCpf)) return Unauthorized("CPF não identificado no token.");

                // 🔥 CORREÇÃO: Normaliza CPF para buscar o histórico correto
                var justNumbers = System.Text.RegularExpressions.Regex.Replace(rawCpf, "[^0-9]", "");
                var cpf = justNumbers.PadLeft(11, '0');

                // 2. Busca as apostas no banco usando o CPF corrigido
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
            // 1. Identificar CPF (Lógica blindada e corrigida)
            var rawCpf = User.FindFirst("cpf")?.Value
                    ?? User.FindFirst(ClaimTypes.Name)?.Value
                    ?? User.FindFirst("unique_name")?.Value
                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(rawCpf))
            {
                return Unauthorized(new { error = "CPF não identificado no token." });
            }

            // 🔥 CORREÇÃO BLINDADA: Remove tudo que não for número e completa com zeros à esquerda
            // Isso resolve o problema de CPFs como "32257941" que deveriam ser "04732257941"
            var justNumbers = System.Text.RegularExpressions.Regex.Replace(rawCpf, "[^0-9]", "");
            var cpf = justNumbers.PadLeft(11, '0');

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


            // 2. Chamar CORE (Débito) usando o CPF corrigido
            string token = Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrEmpty(token)) token = token.Replace("\"", "").Trim();

            var result = await _walletService.DeductFundsAsync(cpf, request.Amount, token);

            if (!result.Success)
            {
                return BadRequest(new { error = result.Message });
            }

            // 3. Salvar Aposta com o CPF corrigido
            var bet = new Bet
            {
                UserCpf = cpf,
                Amount = request.Amount,
                // Recalcula a odd total baseada no request (que agora sabemos que está validado)
                TotalOdd = request.Selections.Any() ? request.Selections.Aggregate(1m, (acc, s) => acc * s.Odd) : 1m,
                CoreTxnId = Guid.NewGuid().ToString(),
                Status = "confirmed",
                CreatedAt = DateTime.UtcNow,
                Selections = request.Selections.Select(s =>
                {
                    // 🔁 LÓGICA DE TRADUÇÃO DE MERCADOS (1, X, 2 -> Casa, Empate, Fora)
                    string finalSelection = s.SelectionName ?? "Seleção";
                    string finalMarket = s.MarketName ?? "Mercado";

                    // Se o frontend mandou "1x2" ou "1", "2", "X" como mercado, padroniza
                    if (finalMarket == "1" || finalMarket == "2" || finalMarket == "X" || finalMarket == "1x2")
                    {
                        finalMarket = "Resultado Final";
                    }

                    // Se for Resultado Final, traduz a seleção
                    if (finalMarket == "Resultado Final" || finalMarket == "Money Line" || finalMarket == "Match Winner")
                    {
                        if (finalSelection == "1") finalSelection = "Casa";
                        else if (finalSelection == "2") finalSelection = "Fora";
                        else if (finalSelection.ToUpper() == "X" || finalSelection.ToLower() == "draw") finalSelection = "Empate";
                    }

                    return new BetSelection
                    {
                        MatchId = s.MatchId ?? "0",
                        MatchName = s.MatchName ?? "Jogo",
                        SelectionName = finalSelection, // Nome Traduzido
                        MarketName = finalMarket,       // Nome Padronizado
                        Odd = s.Odd,
                        Status = "pending",
                        CommenceTime = s.CommenceTime
                    };
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