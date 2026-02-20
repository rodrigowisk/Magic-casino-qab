using Magic_casino.Data;
using Magic_casino.Services;
using Magic_casino.Consumers; // ✅ Adicionado para o EmailConsumer
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using MassTransit; // ✅ Adicionado para RabbitMQ
using System.Text;
using Magic_Casino_Core.Hubs; // ✅ Necessário para o UserHub
using Magic_casino.Middleware; // ✅ Necessário para achar o arquivo do Middleware
using System.Security.Claims; // ✅ NECESSÁRIO PARA CORRIGIR O SIGNALR (ClaimTypes)
using Microsoft.AspNetCore.SignalR; // ✅ Necessário para IUserIdProvider

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("#############################################################");
Console.WriteLine(">>>>> MAGIC CASINO CORE API (WALLET/USERS) <<<<<");
Console.WriteLine("#############################################################");

// =============================================================
// 1. DATABASE (Prioridade para Variável de Ambiente)
// =============================================================
// Nome da variável específico para o CORE
var connectionString = Environment.GetEnvironmentVariable("DB_CONN_CORE")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    // Fallback de emergência ou erro crítico
    Console.WriteLine("CRITICAL: DB_CONN_CORE not found. Check your .env or AWS Variables.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .EnableSensitiveDataLogging()
           .EnableDetailedErrors());

// =============================================================
// 2. JWT CONFIGURATION 🔐 (COMPARTILHADO COM SPORTBOOK)
// =============================================================
// A chave deve ser EXATAMENTE a mesma do Sportbook
var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET")
             ?? builder.Configuration["Jwt:Key"]
             ?? "ChaveSecretaDoCassino2026SuperSeguraNaoMudeIsso";

var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

        // Em microserviços, validação de Issuer/Audience pode dar dor de cabeça
        // se os containers tiverem nomes diferentes. Desligamos para garantir fluxo.
        ValidateIssuer = false,
        ValidateAudience = false,

        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,

        // ✅ CORREÇÃO CRÍTICA PARA O SIGNALR:
        // Isso ensina o SignalR a usar o Claim 'NameIdentifier' (que contém o CPF) como ID de conexão.
        // Sem isso, o comando Clients.User(cpf) não encontra ninguém e o logout não acontece.
        NameClaimType = ClaimTypes.NameIdentifier
    };

    // ✅ ALTERAÇÃO CRÍTICA PARA O SIGNALR FUNCIONAR
    // O SignalR envia o token pela URL (?access_token=...) e não pelo Header.
    // Sem esse bloco, a conexão WebSocket dá erro 401 e o logout não chega.
    x.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];

            // Se a rota começar com /hubs ou /core/hubs, pegamos o token da URL
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/hubs") || path.StartsWithSegments("/core/hubs")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();


// =============================================================
// 3. CORS (Igual ao Sportbook)
// =============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:8080", // Docker Frontend
                "https://quebrandoabanca.bet",
                "https://www.quebrandoabanca.bet"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Importante se usar Cookies/Auth Headers
    });
});

// =============================================================
// 4. REDIS CONFIGURATION (CACHE) 🚀
// =============================================================
// Mesmo que o Core use menos, é vital ter para Cache de Sessão futura
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
                            ?? "localhost:6379,abortConnect=false";

Console.WriteLine($">>>>> REDIS TARGET: {redisConnectionString}");

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse(redisConnectionString);
    config.AbortOnConnectFail = false;

    if (redisConnectionString.Contains("ssl=true"))
    {
        config.Ssl = true;
    }
    return ConnectionMultiplexer.Connect(config);
});

// =============================================================
// ✅ 5. RABBITMQ (MASSTRANSIT) - NOVO!
// =============================================================
builder.Services.AddMassTransit(x =>
{
    // Registra o consumidor de email
    x.AddConsumer<EmailConsumer>();
    x.AddConsumer<SendMessageConsumer>(); 

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
        var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "admin";
        var rabbitPass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "admin";

        Console.WriteLine($">>>>> RABBITMQ CORE CONFIG: Host={rabbitHost}, User={rabbitUser}");

        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        // Cria as filas automaticamente
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// =============================================================
// ✅ SERVICES DE NEGÓCIO (DI)
// =============================================================
builder.Services.AddHttpClient(); // Habilita chamadas HTTP genéricas

// ⬇️ ADICIONADO: REGISTRO DO VELANA SERVICE
builder.Services.AddHttpClient<VelanaService>();

// =============================================================
// ✅ CONFIGURAÇÃO DO SIGNALR (IMPORTANTE PARA O LOGOUT)
// =============================================================
builder.Services.AddSignalR();

// 👇 ESTA LINHA É A CHAVE DO SUCESSO 👇
// Ela ensina o SignalR a identificar o usuário pelo CPF que está no Token.
// Sem isso, Clients.User("cpf") envia para o nada.
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();


// =============================================================
// 6. SWAGGER
// =============================================================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Magic Casino CORE API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT assim: Bearer seu_token_aqui"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// =============================================================
// AUTO-MIGRATE (Igual ao Sportbook)
// =============================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        if (context.Database.CanConnect())
        {
            context.Database.Migrate();
            Console.WriteLine(">>>>> [BANCO CORE] Migrações aplicadas com sucesso!");
        }
        else
        {
            Console.WriteLine(">>>>> [ALERTA] Banco CORE ainda não está acessível.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($">>>>> [ERRO BANCO] Falha ao migrar Core: {ex.Message}");
    }
}

// Configuração do Pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Magic Casino CORE API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowFrontend"); // Usa a política específica

app.UseAuthentication();
app.UseAuthorization();

// ✅ ORDEM CRÍTICA: O Middleware deve ficar AQUI.
// Depois de Auth (para saber quem é o user) e antes dos Controllers/Hubs.
app.UseMiddleware<SecurityStampMiddleware>();

// ✅ MAPEAMENTO DO HUB SIGNALR
// Nota: Certifique-se que o arquivo Hubs/UserHub.cs existe.
app.MapHub<UserHub>("/hubs/user");

app.MapControllers();

app.Run();

// =============================================================
// CLASSE AUXILIAR PARA O SIGNALR (Pode ficar aqui no final)
// =============================================================
public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        // O SignalR usará o CPF (que está no claim "cpf" ou "NameIdentifier") como ID único.
        // Assim, quando o Controller chamar Clients.User("12345678900"), ele saberá quem é.
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? connection.User?.FindFirst("cpf")?.Value;
    }
}