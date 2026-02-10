using BCrypt.Net;
using Magic_casino.Data;
using Magic_casino.DTOs;
using Magic_casino.Events;
using MassTransit;
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
        private readonly IPublishEndpoint _publishEndpoint;

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

            // Verifica se o UserName foi enviado e se já existe
            if (!string.IsNullOrEmpty(req.UserName))
            {
                if (await _context.Users.AnyAsync(u => u.UserName == req.UserName))
                    return BadRequest(new { error = "Nome de usuário indisponível." });
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

            // Se não enviou UserName (legado), usa o CPF como UserName
            var finalUserName = !string.IsNullOrEmpty(req.UserName) ? req.UserName : cpfLimpo;

            // ✅ GERA TOKEN DE ATIVAÇÃO
            var verificationToken = Guid.NewGuid().ToString();

            var newUser = new User
            {
                Cpf = cpfLimpo,
                Name = req.Name,
                UserName = finalUserName,
                Email = req.Email,
                Phone = req.Phone,
                Password = passwordHash,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,

                // ✅ NOVOS CAMPOS PARA NÍVEL E VERIFICAÇÃO
                Level = "bronze", // Inicia como Bronze
                EmailVerified = false,
                VerificationToken = verificationToken,
                TournamentsPlayed = 0
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

                // ✅ ENVIA O TOKEN NO EVENTO PARA O EMAIL CONSUMER
                await _publishEndpoint.Publish(new UserRegisteredEvent
                {
                    Name = newUser.Name,
                    Email = newUser.Email ?? "sem-email@sistema",
                    Cpf = newUser.Cpf,
                    RegisteredAt = DateTime.UtcNow,
                    VerificationToken = newUser.VerificationToken // Passa o token para o email
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro ao salvar no banco", details = ex.Message });
            }

            return Ok(new { msg = "Usuário criado com sucesso!", name = newUser.Name, user_name = newUser.UserName });
        }

        // --- LOGIN (HÍBRIDO: USERNAME OU CPF) ---
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrEmpty(req.Code) || string.IsNullOrEmpty(req.Password))
                return BadRequest(new { error = "Usuário e senha são obrigatórios." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == req.Code);

            if (user == null)
            {
                var codeLimpo = req.Code.Trim().Replace(".", "").Replace("-", "");
                if (codeLimpo.All(char.IsDigit) && codeLimpo.Length == 11)
                {
                    user = await _context.Users.FirstOrDefaultAsync(u => u.Cpf == codeLimpo);
                }
            }

            if (user != null && BCrypt.Net.BCrypt.Verify(req.Password, user.Password))
            {
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserCpf == user.Cpf);
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtKey = _config["Jwt:Key"];

                if (string.IsNullOrEmpty(jwtKey))
                {
                    return StatusCode(500, new { error = "ERRO CRÍTICO: Configuração de JWT ausente." });
                }

                var key = Encoding.UTF8.GetBytes(jwtKey);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[] {
                        new Claim(ClaimTypes.Name, user.Cpf),
                        new Claim("cpf", user.Cpf),
                        new Claim("username", user.UserName),
                        new Claim("admin", user.IsAdmin.ToString())
                    }),
                    Expires = DateTime.UtcNow.AddDays(7),
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
                    Code = user.UserName,
                    Cpf = user.Cpf,
                    Name = user.Name,
                    Email = user.Email,
                    avatar = user.Avatar,
                    Level = user.Level,
                    EmailVerified = user.EmailVerified,
                    Balance_fiver = wallet?.BalanceFiver ?? 0,
                    Balance_qab = wallet?.BalanceQab ?? 0,
                    Balance_bonus = wallet?.BalanceBonus ?? 0
                });
            }

            return Unauthorized(new { error = "Usuário ou senha inválidos" });
        }

        // --- NOVO ENDPOINT: ATIVAR CONTA (LINK DO E-MAIL) ---
        [HttpGet("activate-account")]
        [AllowAnonymous]
        public async Task<IActionResult> ActivateAccount([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest(new { error = "Token inválido." });

            // Busca usuário com esse token
            var user = await _context.Users.FirstOrDefaultAsync(u => u.VerificationToken == token);

            if (user == null)
                // Retorna um HTML simples de erro ou redireciona para página de erro do front
                return BadRequest(new { error = "Link de ativação inválido ou já utilizado." });

            // Ativa o e-mail
            user.EmailVerified = true;
            user.VerificationToken = null; // Invalida o token para não ser usado de novo

            // Lógica de Nível: Se for Bronze, sobe para Prata
            if (user.Level == "bronze")
            {
                user.Level = "prata";
            }

            await _context.SaveChangesAsync();

            // Redireciona para o Front (ajuste a URL conforme seu domínio real)
            return Redirect("https://quebrandoabanca.bet/?activated=true");
        }

        // --- VERIFICA DISPONIBILIDADE ---
        [HttpGet("check-availability/{username}")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckAvailability(string username)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
                return BadRequest(new { error = "Nome muito curto" });

            bool exists = await _context.Users.AnyAsync(u => u.UserName == username);
            return Ok(new { available = !exists });
        }

        // --- ALTERAR SENHA ---
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userCpf = User.FindFirst("cpf")?.Value ?? User.Identity?.Name;
                if (string.IsNullOrEmpty(userCpf)) return Unauthorized(new { message = "Sessão inválida." });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Cpf == userCpf);
                if (user == null) return NotFound(new { message = "Usuário não encontrado." });

                if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.Password))
                    return BadRequest(new { message = "A senha atual está incorreta." });

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

        // --- TRANSAÇÕES ---
        [Authorize]
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            try
            {
                var userCpf = User.FindFirst("cpf")?.Value ?? User.Identity?.Name;
                if (string.IsNullOrEmpty(userCpf)) return Unauthorized();

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
                    method = t.Description ?? t.Source ?? "Transação",
                    type = !string.IsNullOrEmpty(t.Type) ? t.Type.ToLower() : DetermineType(t.Description ?? "")
                });

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Erro ao carregar histórico." });
            }
        }

        // --- PERFIL ---
        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userCpf = User.FindFirst("cpf")?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(userCpf)) return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Cpf == userCpf);
            if (user == null) return NotFound("Usuário não encontrado.");

            return Ok(new
            {
                cpf = user.Cpf,
                user_name = user.UserName,
                name = user.Name,
                email = user.Email,
                phone = user.Phone,
                avatar = user.Avatar,
                level = user.Level,
                email_verified = user.EmailVerified
            });
        }

        // --- ATUALIZAR DADOS ---
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
            if (!string.IsNullOrWhiteSpace(req.Avatar)) user.Avatar = req.Avatar;

            await _context.SaveChangesAsync();

            try
            {
                await _publishEndpoint.Publish(new UserUpdatedEvent
                {
                    UserId = user.Cpf,
                    Name = user.Name,
                    Avatar = user.Avatar
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao enviar evento de update: {ex.Message}");
            }

            return Ok(new { msg = "Dados atualizados!", user_name = user.UserName, name = user.Name, email = user.Email });
        }
    }
}