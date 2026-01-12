using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Magic_casino.Data;
using Magic_Casino_Core.DTOs;

namespace Magic_Casino_Core.Controllers
{
    [ApiController]
    [Route("api/internal/wallet")]
    public class InternalWalletController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InternalWalletController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 🆕 NOVO: CONSULTA DE SALDO (Para o F5 funcionar)
        // ============================================================
        [HttpGet("balance/{cpf}")]
        public async Task<IActionResult> GetBalance(string cpf)
        {
            // Busca a carteira pelo CPF
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserCpf == cpf);

            if (wallet == null)
            {
                // Se não achar carteira (usuário novo?), retorna 0
                return Ok(0m);
            }

            // Retorna o saldo da carteira de Esportes (BalanceQab)
            return Ok(wallet.BalanceQab);
        }

        // ============================================================
        // 💸 DEDUÇÃO (Cobrar Aposta)
        // ============================================================
        [HttpPost("deduct")]
        public async Task<IActionResult> DeductBalance([FromBody] WalletTransactionDto request)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserCpf == request.UserCpf);

            if (wallet == null) return NotFound("Carteira não encontrada para este CPF");

            // Verifica saldo na carteira de Esportes (Qab)
            if (wallet.BalanceQab < request.Amount)
            {
                return BadRequest("Saldo insuficiente");
            }

            // Deduz o valor
            wallet.BalanceQab -= request.Amount;

            await _context.SaveChangesAsync();

            return Ok(new { newBalance = wallet.BalanceQab });
        }

        // ============================================================
        // 💰 REEMBOLSO (Ganhou Aposta / Cancelamento)
        // ============================================================
        [HttpPost("refund")]
        public async Task<IActionResult> RefundBalance([FromBody] WalletTransactionDto request)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserCpf == request.UserCpf);
            if (wallet == null) return NotFound();

            wallet.BalanceQab += request.Amount;
            await _context.SaveChangesAsync();

            return Ok(new { newBalance = wallet.BalanceQab });
        }
    }
}