using BCrypt.Net;
using Magic_casino.Data;
using Magic_casino.DTOs;
using Magic_casino.Events; // ✅ Necessário para o evento
using MassTransit; // ✅ Necessário para IPublishEndpoint
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Magic_casino.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IPublishEndpoint _publishEndpoint; // ✅ Injeção do RabbitMQ

        // ✅ Construtor atualizado recebendo IPublishEndpoint
        public UsersController(AppDbContext context, IConfiguration config, IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _config = config;
            _publishEndpoint = publishEndpoint;
        }

        // --- CADASTRO ---
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var cpfLimpo = req.Cpf.Trim().Replace(".", "").Replace("-", "");

            if (await _context.Users.AnyAsync(u => u.Cpf == cpfLimpo))
                return BadRequest(new { error = "CPF já cadastrado." });

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

            var newUser = new User
            {
                Cpf = cpfLimpo,
                Name = req.Name,
                Email = req.Email,
                Phone = req.Phone,
                Password = passwordHash,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow
            };

            var newWallet = new Wallet
            {
                UserCpf = cpfLimpo,
                BalanceFiver = 0,
                BalanceQab = 0,
                BalanceBonus = 0
            };

            try
            {
                _context.Users.Add(newUser);
                _context.Wallets.Add(newWallet);
                await _context.SaveChangesAsync();

                // =========================================================
                // 🚀 PUBLICAR EVENTO DE BOAS-VINDAS NO RABBITMQ
                // =========================================================
                await _publishEndpoint.Publish(new UserRegisteredEvent
                {
                    Name = newUser.Name,
                    Email = newUser.Email ?? "sem-email@sistema",
                    Cpf = newUser.Cpf,
                    RegisteredAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao salvar no banco", details = ex.Message });
            }

            return Ok(new { msg = "Usuário criado com sucesso!", name = newUser.Name, email = newUser.Email });
        }

        // --- LOGIN ---
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var cpfBusca = req.Code.Trim().Replace(".", "").Replace("-", "");
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Cpf == cpfBusca);

            if (user != null && BCrypt.Net.BCrypt.Verify(req.Password, user.Password))
            {
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserCpf == user.Cpf);

                // --- GERAÇÃO DO TOKEN ---
                var tokenHandler = new JwtSecurityTokenHandler();

                // 🔐 ALTERAÇÃO DEFINITIVA: 
                // Removemos qualquer texto fixo. Pegamos DIRETAMENTE da configuração.
                // Se a variável 'Jwt:Key' não existir no .env ou appsettings, a variável será nula.
                var jwtKey = _config["Jwt:Key"];

                // >>>>> LOG DEBUG INICIO <<<<<
                Console.WriteLine("#############################################################");
                Console.WriteLine($"[DEBUG CONTROLLER - LOGIN] Chave lida de _config['Jwt:Key']: '{jwtKey}'");
                Console.WriteLine("#############################################################");
                // >>>>> LOG DEBUG FIM <<<<<

                // Trava de segurança: Se o .env falhou, paramos aqui com erro 500.
                if (string.IsNullOrEmpty(jwtKey))
                {
                    Console.WriteLine("[DEBUG CONTROLLER] ERRO CRITICO: A chave esta vazia ou nula!");
                    return StatusCode(500, new { error = "ERRO CRÍTICO: Variável 'Jwt:Key' não encontrada no ambiente do servidor." });
                }

                var key = Encoding.UTF8.GetBytes(jwtKey);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[] {
                        new Claim(ClaimTypes.Name, user.Cpf),
                        new Claim("cpf", user.Cpf),
                        new Claim("admin", user.IsAdmin.ToString())
                    }),
                    Expires = DateTime.UtcNow.AddDays(7),

                    // Garante compatibilidade com o Sportbook
                    Issuer = _config["Jwt:Issuer"] ?? "MagicCasinoServer",
                    Audience = _config["Jwt:Audience"] ?? "MagicCasinoApp",

                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

                return Ok(new
                {
                    Status = 1,
                    Msg = "Sucesso",
                    Token = token,
                    Code = user.Cpf,
                    Name = user.Name,
                    Email = user.Email,
                    Balance_fiver = wallet?.BalanceFiver ?? 0,
                    Balance_qab = wallet?.BalanceQab ?? 0,
                    Balance_bonus = wallet?.BalanceBonus ?? 0
                });
            }

            return Unauthorized(new { error = "CPF ou senha inválidos" });
        }


        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                // 1. Pega o CPF do Token (igual ao Update e Profile)
                var userCpf = User.FindFirst("cpf")?.Value ?? User.Identity?.Name;

                if (string.IsNullOrEmpty(userCpf))
                {
                    return Unauthorized(new { message = "Sessão inválida. Faça login novamente." });
                }

                // 2. Busca o usuário no banco pelo CPF
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Cpf == userCpf);

                if (user == null)
                    return NotFound(new { message = "Usuário não encontrado." });

                // 3. Verifica se a SENHA ATUAL está correta
                bool senhaConfere = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.Password);

                if (!senhaConfere)
                    return BadRequest(new { message = "A senha atual está incorreta." });

                // 4. Gera o Hash da NOVA SENHA e salva
                user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Senha alterada com sucesso!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao trocar senha: {ex.Message}");
                return StatusCode(500, new { message = "Erro interno ao atualizar senha." });
            }
        }




        // --- SALDO ---
        [Authorize]
        [HttpGet("my-balance")]
        public async Task<IActionResult> GetMyBalance()
        {
            var cpf = User.FindFirst("cpf")?.Value ?? User.Identity?.Name;

            if (string.IsNullOrEmpty(cpf)) return Unauthorized();

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserCpf == cpf);
            return Ok(new { balance = wallet?.BalanceQab ?? 0 });
        }



        private string DetermineType(string desc)
        {
            if (string.IsNullOrEmpty(desc)) return "sportbook";

            desc = desc.ToLower();
            if (desc.Contains("depósito") || desc.Contains("deposito")) return "deposit";
            if (desc.Contains("saque") || desc.Contains("withdraw")) return "withdraw";
            if (desc.Contains("aposta") && desc.Contains("prêmio")) return "win";
            if (desc.Contains("aposta")) return "bet";
            if (desc.Contains("torneio")) return "tournament";
            return "sportbook";
        }


        [Authorize]
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            try
            {
                // 1. Pega CPF limpo (sem pontos/traços se no banco estiver apenas números)
                var userCpf = User.FindFirst("cpf")?.Value ?? User.Identity?.Name;
                if (string.IsNullOrEmpty(userCpf)) return Unauthorized();

                // 2. Busca no banco
                var dbTransactions = await _context.Transactions
                    .Where(t => t.UserCpf == userCpf)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(50)
                    .ToListAsync();

                var result = dbTransactions.Select(t => new
                {
                    id = "#" + t.Id,
                    amount = t.Amount,
                    date = t.CreatedAt,
                    status = t.Status.ToLower(),
                    method = t.Description ?? t.Source ?? "Transação", // Prioriza descrição, depois source

                    // ✅ LÓGICA HÍBRIDA: Usa o Type do banco. Se vier nulo, tenta descobrir pela descrição.
                    type = !string.IsNullOrEmpty(t.Type) ? t.Type.ToLower() : DetermineType(t.Description ?? "")
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao carregar histórico." });
            }
        }


        // --- PERFIL (NOVO) ---
        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            // Pega o CPF do token
            var userCpf = User.FindFirst("cpf")?.Value ?? User.Identity?.Name;

            if (string.IsNullOrEmpty(userCpf)) return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Cpf == userCpf);

            if (user == null) return NotFound("Usuário não encontrado.");

            // Retorna todos os dados editáveis + CPF
            return Ok(new
            {
                cpf = user.Cpf,
                name = user.Name,
                email = user.Email,
                phone = user.Phone
            });
        }


        // --- UPDATE ---
        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequest req)
        {
            var userCpf = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Cpf == userCpf);

            if (user == null) return NotFound("Usuário não encontrado.");

            if (!string.IsNullOrWhiteSpace(req.Name)) user.Name = req.Name;
            if (!string.IsNullOrWhiteSpace(req.Email)) user.Email = req.Email;
            if (!string.IsNullOrWhiteSpace(req.Phone)) user.Phone = req.Phone;

            await _context.SaveChangesAsync();
            return Ok(new { msg = "Dados atualizados!", user_name = user.Name, user_email = user.Email });
        }
    }
}