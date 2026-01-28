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
                var newTransaction = new Transaction
                {
                    UserCpf = cleanCpf,
                    Amount = (decimal)request.Amount,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Transactions.Add(newTransaction);
                await _context.SaveChangesAsync();

                var response = await _velanaService.CreatePixDepositAsync(
                    amountDouble: request.Amount,
                    userCpf: request.Cpf,
                    userName: request.Name,
                    userEmail: request.Email
                );

                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // =================================================================================
        // 2. WEBHOOK INTELIGENTE (Lê dentro do 'data')
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

                // 1. Tenta deserializar como WRAPPER (Formato Real da Velana)
                // A Velana manda tudo dentro de uma propriedade "data"
                VelanaWebhookDto payload = null;

                try
                {
                    var rootObject = JsonSerializer.Deserialize<VelanaWebhookRoot>(rawBody, options);
                    if (rootObject?.Data != null)
                    {
                        payload = rootObject.Data;
                        Console.WriteLine("[DEBUG] Payload extraído da propriedade 'data' (Formato Velana)");
                    }
                }
                catch { }

                // 2. Se falhar ou não tiver 'data', tenta deserializar DIRETO (Formato Postman Antigo)
                if (payload == null || payload.Status == null)
                {
                    payload = JsonSerializer.Deserialize<VelanaWebhookDto>(rawBody, options);
                    Console.WriteLine("[DEBUG] Payload lido da raiz (Formato Postman)");
                }

                if (payload == null) return BadRequest("Falha ao ler JSON");

                // --- LÓGICA DE PAGAMENTO ---

                var status = payload.Status?.ToUpper() ?? "";
                Console.WriteLine($"[DEBUG] Status Lido: '{status}'");

                bool isPaid = (status == "PAID" || status == "COMPLETED" || status == "CONFIRMED" || status == "APPROVED");

                if (!isPaid)
                {
                    Console.WriteLine($"[WEBHOOK IGNORADO] Status não é pago: {status}");
                    return Ok(new { message = "Status ignorado" });
                }

                // Busca CPF
                var cpfUsuario = payload.ExternalReference ?? payload.Reference;
                if (string.IsNullOrEmpty(cpfUsuario) && payload.Customer?.Document?.Number != null)
                {
                    cpfUsuario = payload.Customer.Document.Number;
                }

                if (string.IsNullOrEmpty(cpfUsuario))
                {
                    Console.WriteLine("[WEBHOOK ERRO] CPF não encontrado no JSON.");
                    return Ok(new { message = "Erro de dados: CPF ausente" });
                }

                cpfUsuario = cpfUsuario.Replace(".", "").Replace("-", "").Trim();

                // --- AJUSTE DE VALOR ---
                decimal amountVal = payload.Amount;

                // Se vier 1000 (centavos) converte para 10.00
                if (amountVal > 200 && amountVal % 1 == 0)
                {
                    amountVal = amountVal / 100m;
                }

                Console.WriteLine($"[WEBHOOK PROCESSANDO] CPF: {cpfUsuario}, Valor Lido: {payload.Amount}, Valor Ajustado: {amountVal}");

                var transaction = await _context.Transactions
                    .Where(t => t.UserCpf == cpfUsuario && t.Status == "pending")
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (transaction == null)
                {
                    // Verifica se já processou
                    bool jaPago = await _context.Transactions
                       .AnyAsync(t => t.UserCpf == cpfUsuario && t.Status == "paid" && t.CreatedAt > DateTime.UtcNow.AddMinutes(-60));

                    if (jaPago)
                    {
                        Console.WriteLine("[WEBHOOK] Pagamento já processado anteriormente.");
                        return Ok(new { message = "Já processado" });
                    }

                    Console.WriteLine($"[WEBHOOK AVISO] Nenhuma transação pendente para {cpfUsuario}.");
                    return NotFound("Nenhuma transação pendente encontrada.");
                }

                transaction.Status = "paid";
                transaction.PaidAt = DateTime.UtcNow;
                if (payload.Id != null) transaction.ExternalReference = payload.Id.ToString();

                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserCpf == cpfUsuario);

                if (wallet != null)
                {
                    // Credita o valor da transação original (segurança contra erro de centavos)
                    wallet.BalanceQab += transaction.Amount;
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"[SUCESSO ABSOLUTO] Creditado R$ {transaction.Amount} para {cpfUsuario}");
                    return Ok(new { message = "Saldo creditado" });
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Wallet não encontrada" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERRO CRITICO WEBHOOK]: {ex}");
                return StatusCode(500, "Erro interno");
            }
        }
    }

    // ==========================================
    // NOVOS DTOs (Adaptados para a Velana)
    // ==========================================

    // 1. Wrapper (A caixa "data")
    public class VelanaWebhookRoot
    {
        public string Type { get; set; }
        public VelanaWebhookDto Data { get; set; }
    }

    // 2. Conteúdo Real
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