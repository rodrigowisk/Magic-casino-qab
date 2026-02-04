using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Magic_casino.Data;
using Magic_Casino_Core.DTOs;
using Magic_casino.Models;

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
        // 🆕 CONSULTA DE SALDO
        // ============================================================
        [HttpGet("balance/{cpf}")]
        public async Task<IActionResult> GetBalance(string cpf)
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserCpf == cpf);

            if (wallet == null) return Ok(0m);

            return Ok(wallet.BalanceQab);
        }

        // ============================================================
        // 💸 DEDUÇÃO GENÉRICA (Serve para Aposta, Torneio, Slot...)
        // ============================================================
        [HttpPost("deduct")]
        public async Task<IActionResult> DeductBalance([FromBody] WalletTransactionDto request)
        {
            // 1. Busca a carteira
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserCpf == request.UserCpf);

            if (wallet == null) return NotFound("Carteira não encontrada para este CPF");

            // 2. Valida Saldo
            if (wallet.BalanceQab < request.Amount)
            {
                return BadRequest("Saldo insuficiente");
            }

            // 3. Atualiza Saldo (DÉBITO)
            wallet.BalanceQab -= request.Amount;

            // 4. 🔥 CRIA O REGISTRO NA TABELA TRANSACTIONS (CORREÇÃO DINÂMICA) 🔥

            // Verifica duplicidade (Idempotência)
            var existingTx = await _context.Transactions
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ExternalReference == request.ReferenceId); // Removi o filtro de Type para ser mais seguro

            if (existingTx == null)
            {
                var transaction = new Transaction
                {
                    UserCpf = request.UserCpf,
                    Amount = request.Amount,

                    // ✅ DINÂMICO: Usa o que veio no pedido (ex: "tournament", "slot"). 
                    // Se vier nulo, assume "bet" (Sportbook).
                    Type = request.Type?.ToLower() ?? "bet",

                    // ✅ DINÂMICO: Usa a origem enviada.
                    Source = request.Source ?? "Sportbook",

                    Status = "COMPLETED",
                    ExternalReference = request.ReferenceId ?? Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,

                    // ✅ DINÂMICO: Usa a descrição enviada (ex: "Inscrição Torneio").
                    Description = request.Description ?? "Aposta Esportiva (Débito)"
                };

                _context.Transactions.Add(transaction);
            }

            // 5. Salva Wallet e Transaction juntos
            await _context.SaveChangesAsync();

            return Ok(new { newBalance = wallet.BalanceQab });
        }

        // ============================================================
        // 💰 CRÉDITO GENÉRICO (Prêmio Aposta, Prêmio Torneio, Slot...)
        // ============================================================
        [HttpPost("credit")]
        public async Task<IActionResult> CreditBalance([FromBody] WalletTransactionDto request)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserCpf == request.UserCpf);
            if (wallet == null) return NotFound();

            // 1. Atualiza Saldo (CRÉDITO)
            wallet.BalanceQab += request.Amount;

            // 2. 🔥 CRIA O REGISTRO DA TRANSAÇÃO 🔥

            // Define descrição padrão se não vier nada
            string defaultDesc = "Prêmio";
            if (request.Type == "refund") defaultDesc = "Reembolso";
            else if (request.Type == "tournament_prize") defaultDesc = "Prêmio de Torneio";
            else if (request.Type == "slot_win") defaultDesc = "Prêmio Slot";

            var transaction = new Transaction
            {
                UserCpf = request.UserCpf,
                Amount = request.Amount,

                // ✅ DINÂMICO
                Type = request.Type?.ToLower() ?? "win",
                Source = request.Source ?? "Sportbook",
                Description = request.Description ?? defaultDesc,

                Status = "COMPLETED",
                ExternalReference = request.ReferenceId ?? Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);

            await _context.SaveChangesAsync();

            return Ok(new { newBalance = wallet.BalanceQab });
        }

        // Endpoint legado para compatibilidade
        [HttpPost("refund")]
        public async Task<IActionResult> RefundBalance([FromBody] WalletTransactionDto request)
        {
            request.Type = "refund";
            return await CreditBalance(request);
        }
    }
}