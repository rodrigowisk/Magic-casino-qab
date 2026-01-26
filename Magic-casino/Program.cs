using Magic_casino.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using StackExchange.Redis; // ✅ Necessário para Cache Distribuído

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
        ClockSkew = TimeSpan.Zero
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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient(); // Habilita chamadas HTTP para outros serviços (ex: Sportbook)

// =============================================================
// 5. SWAGGER
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

app.MapControllers();

app.Run();