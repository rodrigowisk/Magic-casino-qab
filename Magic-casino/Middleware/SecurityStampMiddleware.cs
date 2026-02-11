using Magic_casino.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis; // ✅ Necessário para o Redis
using System.Security.Claims;

namespace Magic_casino.Middleware
{
    public class SecurityStampMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConnectionMultiplexer _redis; // ✅ Injeção do Redis

        public SecurityStampMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
        {
            _next = next;
            _redis = redis;
        }

        public async Task Invoke(HttpContext context, AppDbContext dbContext)
        {
            // 1. Verifica se o usuário está autenticado
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userCpf = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? context.User.FindFirst("cpf")?.Value;

                var tokenStamp = context.User.FindFirst("SecurityStamp")?.Value;

                if (!string.IsNullOrEmpty(userCpf) && !string.IsNullOrEmpty(tokenStamp))
                {
                    string? currentStamp = null;
                    var redisDb = _redis.GetDatabase();
                    string redisKey = $"auth:security_stamp:{userCpf}";

                    try
                    {
                        // 🚀 2. TENTA LER DO REDIS (Muito Rápido - 0.5ms)
                        currentStamp = await redisDb.StringGetAsync(redisKey);
                    }
                    catch (Exception ex)
                    {
                        // Se o Redis falhar, apenas loga e segue para o Banco (Fallback)
                        Console.WriteLine($"[Redis Error] {ex.Message}");
                    }

                    // 3. SE NÃO ACHOU NO REDIS, VAI NO BANCO (Lento - 20ms)
                    if (string.IsNullOrEmpty(currentStamp))
                    {
                        try
                        {
                            // ✅ PROTEÇÃO CRÍTICA CONTRA TIMEOUT
                            // Adicionado AsNoTracking() para performance e try/catch para não quebrar o site
                            var dbUser = await dbContext.Users
                                .AsNoTracking()
                                .Where(u => u.Cpf == userCpf)
                                .Select(u => new { u.SecurityStamp })
                                .FirstOrDefaultAsync();

                            if (dbUser != null)
                            {
                                currentStamp = dbUser.SecurityStamp;

                                // ✅ SALVA NO REDIS PARA A PRÓXIMA REQUISIÇÃO SER RÁPIDA
                                // Define expiração de 1 hora (renova se precisar)
                                try
                                {
                                    await redisDb.StringSetAsync(redisKey, currentStamp, TimeSpan.FromHours(1));
                                }
                                catch { }
                            }
                        }
                        catch (Exception dbEx)
                        {
                            // 🛑 EVITA O ERRO 500 (TimeoutException)
                            // Se o banco de dados cair ou demorar, logamos o erro,
                            // mas permitimos que o usuário continue navegando com o token atual.
                            Console.WriteLine($"[DB Critical] Falha ao validar sessão no banco: {dbEx.Message}");
                        }
                    }

                    // 4. COMPARAÇÃO DE SEGURANÇA
                    // Se conseguimos obter o carimbo atual e ele for diferente do token -> BLOQUEIA
                    // Se currentStamp for null (por erro no banco), deixamos passar (Fail Open) para não travar o usuário.
                    if (!string.IsNullOrEmpty(currentStamp) && currentStamp != tokenStamp)
                    {
                        context.Response.StatusCode = 401; // Unauthorized
                        await context.Response.WriteAsJsonAsync(new { message = "Sessão inválida. Outro dispositivo conectou." });
                        return; // 🛑 BLOQUEIA AQUI
                    }
                }
            }

            await _next(context);
        }
    }
}