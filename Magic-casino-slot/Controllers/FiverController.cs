using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Magic_casino_slot.Data;
using Magic_casino_slot.DTOs;

namespace Magic_casino_slot.Controllers
{
    [ApiController]
    [Route("api/fiver")]
    public class FiverController : ControllerBase
    {
        private readonly AppDbContext _context; 

        public FiverController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("callback")]
        public async Task<IActionResult> HandleCallback([FromBody] FiverRequestDto request)
        {
            // 1. Validação básica
            if (request == null || string.IsNullOrEmpty(request.Method))
                return BadRequest(new { status = 0, msg = "INVALID_REQUEST" });

            // 2. Roteamento pelo "method" da Fiver
            switch (request.Method)
            {
                case "user_balance":
                    return await GetUserBalance(request);

                case "transaction":
                    return await ProcessTransaction(request);

                default:
                    return Ok(new { status = 0, msg = "INVALID_METHOD" });
            }
        }

        // --- MÉTODOS AUXILIARES ---

        private async Task<IActionResult> GetUserBalance(FiverRequestDto request)
        {
            // Busca o usuário e inclui a carteira (JOIN)
            var user = await _context.Users
                .Include(u => u.Wallet)
                .FirstOrDefaultAsync(u => u.Code == request.UserCode);

            if (user == null || user.Wallet == null)
            {
                // Se usuário não existe ou não tem carteira
                return Ok(new { status = 0, msg = "USER_NOT_FOUND" });
            }

            // Retorna o saldo específico do Fiver (BalanceFiver)
            return Ok(new
            {
                status = 1,
                user_balance = user.Wallet.BalanceFiver
            });
        }

        private async Task<IActionResult> ProcessTransaction(FiverRequestDto request)
        {
            // A Fiver manda os valores como Decimal ou Int no JSON, 
            // mas seu banco é LONG. Vamos converter seguro.
            long betAmount = (long)request.Slot.BetMoney;
            long winAmount = (long)request.Slot.WinMoney;

            // 1. Busca Usuário e Carteira
            var user = await _context.Users
                .Include(u => u.Wallet)
                .FirstOrDefaultAsync(u => u.Code == request.UserCode);

            if (user == null || user.Wallet == null)
                return Ok(new { status = 0, msg = "USER_NOT_FOUND" });

            // 2. Verifica saldo (só se for aposta, ou seja, bet > 0)
            // Nota: Algumas transações podem ser apenas prêmio, então só checa se Bet > 0
            if (betAmount > 0 && user.Wallet.BalanceFiver < betAmount)
            {
                return Ok(new { status = 0, msg = "INSUFFICIENT_USER_FUNDS" });
            }

            // 3. Atualiza o Saldo
            // Lógica: Saldo Atual - Aposta + Prêmio
            user.Wallet.BalanceFiver = user.Wallet.BalanceFiver - betAmount + winAmount;

            try
            {
                // 4. Salva no Banco
                await _context.SaveChangesAsync();

                // (Opcional) Aqui você salvaria na tabela de Histórico de Jogadas se tiver uma
            }
            catch (Exception ex)
            {
                // Logar o erro real no console para você ver
                Console.WriteLine($"Erro ao salvar: {ex.Message}");
                return Ok(new { status = 0, msg = "INTERNAL_ERROR" });
            }

            // 5. Retorna sucesso com o novo saldo
            return Ok(new
            {
                status = 1,
                user_balance = user.Wallet.BalanceFiver
            });
        }
    }
}