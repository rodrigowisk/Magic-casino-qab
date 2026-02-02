using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Magic_casino.Services;
using Magic_casino.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Magic_casino.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VelanaController : ControllerBase
    {
        private readonly VelanaService _velanaService;
        private readonly AppDbContext _context;

        public VelanaController(VelanaService velanaService, AppDbContext context)
        {
            _velanaService = velanaService;
            _context = context;
        }

        [HttpPost("deposit-pix")]
        public async Task<IActionResult> CreateDeposit([FromBody] TestDepositRequest request)
        {
            try
            {
                var cleanCpf = request.Cpf.Replace(".", "").Replace("-", "").Trim();

                // 1. Cria a transação PENDENTE no banco
                var newTransaction = new Transaction
                {
                    UserCpf = cleanCpf,
                    Amount = (decimal)request.Amount,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow,

                    // ✅ CORREÇÃO: Preenche os campos para o Histórico funcionar corretamente
                    Type = "deposit",
                    Source = "Pix",
                    Description = "Depósito via PIX (Pendente)"
                };

                _context.Transactions.Add(newTransaction);
                await _context.SaveChangesAsync();

                // 2. Chama a Velana
                var response = await _velanaService.CreatePixDepositAsync(
                    amountDouble: request.Amount,
                    userCpf: request.Cpf,
                    userName: request.Name,
                    userEmail: request.Email
                );

                // 3. Atualiza a transação com o ID da Velana para vincular depois
                newTransaction.ExternalReference = response.Id.ToString();
                await _context.SaveChangesAsync();

                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // =================================================================================
        // 2. WEBHOOK INTELIGENTE
        // =================================================================================
        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveWebhook()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var rawBody = await reader.ReadToEndAsync();

                Console.WriteLine("###################################################");
                Console.WriteLine($"[WEBHOOK RAW JSON CHEGOU]: {rawBody}");
                Console.WriteLine("###################################################");

                if (string.IsNullOrWhiteSpace(rawBody)) return BadRequest("Body vazio");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };

                // Lógica de Deserialização (Mantida a sua que já funciona)
                VelanaWebhookDto payload = null;
                try
                {
                    var rootObject = JsonSerializer.Deserialize<VelanaWebhookRoot>(rawBody, options);
                    if (rootObject?.Data != null) payload = rootObject.Data;
                }
                catch { }

                if (payload == null || payload.Status == null)
                {
                    payload = JsonSerializer.Deserialize<VelanaWebhookDto>(rawBody, options);
                }

                if (payload == null) return BadRequest("Falha ao ler JSON");

                // --- LÓGICA DE PAGAMENTO ---
                var status = payload.Status?.ToUpper() ?? "";
                bool isPaid = (status == "PAID" || status == "COMPLETED" || status == "CONFIRMED" || status == "APPROVED");

                if (!isPaid) return Ok(new { message = "Status ignorado" });

                // Busca CPF
                var cpfUsuario = payload.ExternalReference ?? payload.Reference;
                if (string.IsNullOrEmpty(cpfUsuario) && payload.Customer?.Document?.Number != null)
                {
                    cpfUsuario = payload.Customer.Document.Number;
                }

                if (string.IsNullOrEmpty(cpfUsuario)) return Ok(new { message = "Erro de dados: CPF ausente" });

                cpfUsuario = cpfUsuario.Replace(".", "").Replace("-", "").Trim();

                // Busca a transação pendente pelo ExternalReference (ID da Velana) OU pelo CPF + Valor
                // Prioriza o ID se tiver
                Transaction transaction = null;
                string externalId = payload.Id?.ToString() ?? "";

                if (!string.IsNullOrEmpty(externalId))
                {
                    transaction = await _context.Transactions
                        .FirstOrDefaultAsync(t => t.ExternalReference == externalId);
                }

                // Fallback: Se não achou pelo ID, tenta pelo CPF + Pendente (último 10 min)
                if (transaction == null)
                {
                    transaction = await _context.Transactions
                        .Where(t => t.UserCpf == cpfUsuario && t.Status == "pending")
                        .OrderByDescending(t => t.CreatedAt)
                        .FirstOrDefaultAsync();
                }

                if (transaction == null)
                {
                    Console.WriteLine($"[WEBHOOK AVISO] Nenhuma transação encontrada para processar.");
                    // Retorna OK para a Velana parar de mandar, já que não temos o que processar
                    return Ok(new { message = "Transação não encontrada ou já processada" });
                }

                // --- TRAVA DE DUPLICIDADE ---
                if (transaction.Status == "paid" || transaction.Status == "COMPLETED")
                {
                    Console.WriteLine("[WEBHOOK] Pagamento já processado anteriormente.");
                    return Ok(new { message = "Já processado" });
                }

                // --- PROCESSA O PAGAMENTO ---
                transaction.Status = "COMPLETED"; // Padronizado com o sistema
                transaction.PaidAt = DateTime.UtcNow;
                transaction.Type = "deposit";
                transaction.Source = "Pix";
                transaction.Description = "Depósito via PIX (Confirmado)";

                if (!string.IsNullOrEmpty(externalId)) transaction.ExternalReference = externalId;

                // Credita na carteira
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserCpf == cpfUsuario);
                if (wallet != null)
                {
                    wallet.BalanceQab += transaction.Amount;
                    Console.WriteLine($"[SUCESSO] Creditado R$ {transaction.Amount} para {cpfUsuario}");
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Saldo creditado com sucesso" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERRO CRITICO WEBHOOK]: {ex}");
                return StatusCode(500, "Erro interno");
            }
        }
    }

    // ==========================================
    // DTOs (Mantidos)
    // ==========================================
    public class VelanaWebhookRoot
    {
        public string Type { get; set; }
        public VelanaWebhookDto Data { get; set; }
    }

    public class VelanaWebhookDto
    {
        public string? Status { get; set; }
        public object? Id { get; set; }
        public decimal Amount { get; set; }
        public string? ExternalReference { get; set; }
        public string? Reference { get; set; }
        public VelanaCustomerWebhookDto? Customer { get; set; }
    }

    public class VelanaCustomerWebhookDto
    {
        public VelanaDocumentWebhookDto? Document { get; set; }
    }

    public class VelanaDocumentWebhookDto
    {
        public string? Number { get; set; }
    }

    public class TestDepositRequest
    {
        public double Amount { get; set; }
        public string Cpf { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }
}